using UnityEngine;
using Unity.NetCode;
using System;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Persistent manager for LAN network discovery that survives scene loads.
    /// Provides a singleton instance and forwards discovery events.
    /// </summary>
    public class MultiplayerDiscoveryManager : MonoBehaviour
    {
        public static MultiplayerDiscoveryManager Instance { get; private set; }

        private LanNetworkDiscovery _networkDiscovery;

        // Expose events for external listeners (e.g., MultiplayerLobby)
        public event Action<LanDiscoveredGame> OnGameDiscovered
        {
            add => _networkDiscovery.OnGameDiscovered += value;
            remove => _networkDiscovery.OnGameDiscovered -= value;
        }

        public event Action<string> OnGameLost
        {
            add => _networkDiscovery.OnGameLost += value;
            remove => _networkDiscovery.OnGameLost -= value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log("[MultiplayerDiscoveryManager] Awake – Instance set");

            // Create the underlying LanNetworkDiscovery component
            var go = new GameObject("NetworkDiscovery");
            DontDestroyOnLoad(go);
            _networkDiscovery = go.AddComponent<LanNetworkDiscovery>();
        }

        public LanNetworkDiscovery GetDiscovery()
        {
            if (_networkDiscovery == null)
            {
                // Create the underlying LanNetworkDiscovery component if it hasn't been created yet
                var go = new GameObject("NetworkDiscovery");
                DontDestroyOnLoad(go);
                _networkDiscovery = go.AddComponent<LanNetworkDiscovery>();
                Debug.Log("[MultiplayerDiscoveryManager] Created LanNetworkDiscovery lazily");
            }
            return _networkDiscovery;
        }

        public void StartBroadcasting(string gameName, string hostName, ushort port)
        {
            _networkDiscovery.StartBroadcasting(gameName, hostName, port);
        }

        public void StartDiscovery()
        {
            _networkDiscovery.StartDiscovery();
        }

        public void StopAll()
        {
            // Stop both broadcasting and discovery if they are active
            try { _networkDiscovery.StopBroadcasting(); } catch { }
            try { _networkDiscovery.StopDiscovery(); } catch { }
        }
    }
}
