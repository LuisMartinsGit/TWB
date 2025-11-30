using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Simple UDP-based LAN game discovery.
    /// This is a minimal MonoBehaviour for network discovery only.
    /// The actual networking is handled by Netcode for Entities.
    /// </summary>
    public class LanNetworkDiscovery : MonoBehaviour
    {
        private const int BROADCAST_PORT = 47777;
        private const float BROADCAST_INTERVAL = 1.0f;
        private const float DISCOVERY_TIMEOUT = 5.0f;

        private UdpClient _broadcastClient;
        private UdpClient _listenClient;
        private readonly Dictionary<string, LanDiscoveredGame> _discoveredGames = new Dictionary<string, LanDiscoveredGame>();
        
        private bool _isBroadcasting;
        private float _lastBroadcastTime;

        private LanGameInfo _hostInfo;

        public event Action<LanDiscoveredGame> OnGameDiscovered;
        public event Action<string> OnGameLost;


        void OnDestroy()
        {
            StopBroadcasting();
            StopDiscovery();
        }

        #region Broadcasting

        public void StartBroadcasting(string gameName, string hostName, ushort gamePort)
        {
            StopBroadcasting();

            _hostInfo = new LanGameInfo
            {
                GameName = gameName,
                HostName = hostName,
                GamePort = gamePort,
                CurrentPlayers = 1,
                MaxPlayers = 8
            };

            try
            {
                _broadcastClient = new UdpClient();
                _broadcastClient.EnableBroadcast = true;
                _isBroadcasting = true;
                _lastBroadcastTime = Time.time;
                Debug.Log($"[LanNetworkDiscovery] Started broadcasting on port {BROADCAST_PORT} for game \"{gameName}\"");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanNetworkDiscovery] Failed to start broadcasting: {e.Message}");
            }
        }

        public void StopBroadcasting()
        {
            if (_broadcastClient != null)
            {
                _broadcastClient.Close();
                _broadcastClient = null;
            }
            _isBroadcasting = false;
        }

        void Update()
        {
            if (_isBroadcasting && Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
            {
                SendBroadcast();
                _lastBroadcastTime = Time.time;
            }

            CleanupStaleGames();
        }

        private void SendBroadcast()
        {
            if (_broadcastClient == null || _hostInfo == null) return;

            try
            {
                string message = $"TWB_GAME|{_hostInfo.GameName}|{_hostInfo.HostName}|{_hostInfo.GamePort}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
                _broadcastClient.Send(data, data.Length, endpoint);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanNetworkDiscovery] Broadcast failed: {e.Message}");
            }
        }

        #endregion

        #region Discovery

        public void StartDiscovery()
        {
            StopDiscovery();

            try
            {
                _listenClient = new UdpClient(BROADCAST_PORT);
                _listenClient.BeginReceive(OnReceiveBroadcast, null);
                Debug.Log("[LanNetworkDiscovery] Started listening for games");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanNetworkDiscovery] Failed to start discovery: {e.Message}");
            }
        }

        public void StopDiscovery()
        {
            if (_listenClient != null)
            {
                _listenClient.Close();
                _listenClient = null;
            }
            _discoveredGames.Clear();
        }

        private void OnReceiveBroadcast(IAsyncResult result)
        {
            if (_listenClient == null) return;

            try
            {
                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _listenClient.EndReceive(result, ref remoteEndpoint);
                string message = Encoding.UTF8.GetString(data);

                if (message.StartsWith("TWB_GAME|"))
                {
                    string[] parts = message.Split('|');
                    if (parts.Length >= 4)
                    {
                        var gameInfo = new LanGameInfo
                        {
                            GameName = parts[1],
                            HostName = parts[2],
                            GamePort = ushort.Parse(parts[3]),
                            CurrentPlayers = 0,
                            MaxPlayers = 8
                        };

                        string gameId = remoteEndpoint.Address.ToString();
                        var discoveredGame = new LanDiscoveredGame(gameId, gameInfo);

                        bool isNewGame = !_discoveredGames.ContainsKey(gameId);
                        _discoveredGames[gameId] = discoveredGame;

                        if (isNewGame)
                        {
                            Debug.Log($"[LanNetworkDiscovery] Found game: {gameInfo.GameName} at {gameId}");
                            OnGameDiscovered?.Invoke(discoveredGame);
                        }
                    }
                }

                _listenClient.BeginReceive(OnReceiveBroadcast, null);
            }
            catch (ObjectDisposedException)
            {
                // Client was closed
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanNetworkDiscovery] Error receiving broadcast: {e.Message}");
                try
                {
                    if (_listenClient != null)
                        _listenClient.BeginReceive(OnReceiveBroadcast, null);
                }
                catch { }
            }
        }

        private void CleanupStaleGames()
        {
            if (_discoveredGames.Count == 0) return;

            var staleGames = new List<string>();
            foreach (var kvp in _discoveredGames)
            {
                if ((DateTime.Now - kvp.Value.LastSeen).TotalSeconds > DISCOVERY_TIMEOUT)
                {
                    staleGames.Add(kvp.Key);
                }
            }

            foreach (var gameId in staleGames)
            {
                _discoveredGames.Remove(gameId);
                OnGameLost?.Invoke(gameId);
            }
        }

        public List<LanDiscoveredGame> GetDiscoveredGames()
        {
            return new List<LanDiscoveredGame>(_discoveredGames.Values);
        }

        #endregion
    }
}
