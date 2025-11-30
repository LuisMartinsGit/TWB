using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace TheWaningBorder.Multiplayer.Systems
{
    /// <summary>
    /// Initializes the network system when the game starts in multiplayer mode.
    /// Creates client or server worlds based on GameSettings.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial class NetworkBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamDriver>();
        }

        protected override void OnUpdate()
        {
            if (_initialized) return;

            // Only initialize if we're in multiplayer mode
            if (!GameSettings.IsMultiplayer)
            {
                Enabled = false;
                return;
            }

            Debug.Log($"[NetworkBootstrap] Initializing multiplayer as {GameSettings.NetworkRole}");

            if (GameSettings.NetworkRole == NetworkRole.Server)
            {
                InitializeServer();
            }
            else if (GameSettings.NetworkRole == NetworkRole.Client)
            {
                InitializeClient();
            }

            _initialized = true;
        }

        private void InitializeServer()
        {
            Debug.Log("[NetworkBootstrap] Creating server world");

            // The server world is already created by the default bootstrap
            // We just need to configure it

            var serverWorld = World;
            
            // Create singleton entities for server state
            var networkGameState = EntityManager.CreateEntity(typeof(NetworkGameState));
            EntityManager.SetComponentData(networkGameState, new NetworkGameState
            {
                CurrentPhase = GamePhase.Lobby,
                TotalPlayers = 0,
                ReadyPlayers = 0
            });

            var networkIdAllocator = EntityManager.CreateEntity(typeof(NetworkIdAllocator));
            EntityManager.SetComponentData(networkIdAllocator, new NetworkIdAllocator
            {
                NextId = 1 // Start from 1, 0 is reserved for invalid
            });

            Debug.Log("[NetworkBootstrap] Server world initialized");
        }

        private void InitializeClient()
        {
            Debug.Log("[NetworkBootstrap] Initializing client world");

            // Client world is already created by the default bootstrap
            // We just mark our local player

            // The connection entity will be created when we connect to the server
            // We'll mark it with LocalPlayerTag in the connection system

            Debug.Log("[NetworkBootstrap] Client world initialized");
        }
    }
}
