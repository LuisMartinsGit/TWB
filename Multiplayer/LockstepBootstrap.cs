// Assets/Scripts/Multiplayer/LockstepBootstrap.cs
// Initializes lockstep networking when game scene loads in multiplayer mode
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Bootstraps the lockstep system when entering a multiplayer game.
    /// This is created by MultiplayerLobbyUI before loading the game scene.
    /// </summary>
    public class LockstepBootstrap : MonoBehaviour
    {
        public static LockstepBootstrap Instance { get; private set; }

        // Configuration set by lobby before scene load
        public bool IsHost { get; set; }
        public int LocalPlayerIndex { get; set; }
        public Faction LocalFaction { get; set; }
        public int LocalPort { get; set; }
        public string HostIP { get; set; }
        public int HostPort { get; set; }
        public List<RemotePlayerInfo> RemotePlayers { get; set; } = new List<RemotePlayerInfo>();

        // State
        private bool _initialized = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Game") return;
            if (_initialized) return;
            if (!GameSettings.IsMultiplayer) return;

            StartCoroutine(InitializeLockstep());
        }

        private IEnumerator InitializeLockstep()
        {
            // Wait a frame for all entities to be created
            yield return null;
            yield return null;

            Debug.Log($"[LockstepBootstrap] Initializing lockstep - IsHost: {IsHost}, LocalPlayer: {LocalPlayerIndex}, Faction: {LocalFaction}");

            // Create LockstepManager if it doesn't exist
            var lockstep = LockstepManager.Instance;
            if (lockstep == null)
            {
                var go = new GameObject("LockstepManager");
                lockstep = go.AddComponent<LockstepManager>();
            }

            // Initialize based on role
            if (IsHost)
            {
                lockstep.InitializeAsHost(HostPort, RemotePlayers);
            }
            else
            {
                lockstep.InitializeAsClient(LocalPort, HostIP, HostPort, LocalPlayerIndex, LocalFaction);
            }

            // Wait for network ID assignment to complete
            yield return null;

            // Start simulation
            lockstep.StartSimulation();

            _initialized = true;
            Debug.Log("[LockstepBootstrap] Lockstep simulation started!");
        }

        /// <summary>
        /// Called from lobby to set up host configuration
        /// </summary>
        public static LockstepBootstrap SetupAsHost(int port, List<ClientInfo> clients)
        {
            var bootstrap = GetOrCreate();
            bootstrap.IsHost = true;
            bootstrap.LocalPlayerIndex = 0;
            bootstrap.LocalFaction = Faction.Blue;
            bootstrap.HostPort = port;
            bootstrap.LocalPort = port;

            bootstrap.RemotePlayers.Clear();
            foreach (var client in clients)
            {
                bootstrap.RemotePlayers.Add(new RemotePlayerInfo
                {
                    IP = client.IP,
                    Port = client.Port,
                    Faction = client.Faction // Use faction from ClientInfo
                });
            }

            return bootstrap;
        }

        /// <summary>
        /// Called from lobby to set up client configuration
        /// </summary>
        public static LockstepBootstrap SetupAsClient(int localPort, string hostIP, int hostPort, int slotIndex, Faction faction)
        {
            var bootstrap = GetOrCreate();
            bootstrap.IsHost = false;
            bootstrap.LocalPlayerIndex = slotIndex;
            bootstrap.LocalFaction = faction;
            bootstrap.LocalPort = localPort;
            bootstrap.HostIP = hostIP;
            bootstrap.HostPort = hostPort;

            return bootstrap;
        }

        private static LockstepBootstrap GetOrCreate()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("LockstepBootstrap");
            return go.AddComponent<LockstepBootstrap>();
        }
    }

    // ClientInfo is defined in LockstepTypes.cs
}