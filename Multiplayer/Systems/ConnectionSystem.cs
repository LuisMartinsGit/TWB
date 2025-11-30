using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using UnityEngine;

namespace TheWaningBorder.Multiplayer.Systems
{
    /// <summary>
    /// Handles RPC processing for player connections, disconnections, and gamestart.
    /// Runs on both server and client.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ConnectionSystem : SystemBase
    {
        private int _nextPlayerId = 1;

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var isServer = World.IsServer();

            // Process join requests (server only)
            if (isServer)
            {
                foreach (var (joinRpc, rpcEntity) in SystemAPI.Query<RefRO<JoinGameRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                {
                    ProcessJoinRequest(joinRpc.ValueRO, rpcEntity);
                }

                // Process ready state changes (server only)
                foreach (var (readyRpc, rpcEntity) in SystemAPI.Query<RefRO<PlayerReadyRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                {
                    ProcessPlayerReady(readyRpc.ValueRO, rpcEntity);
                }
            }

            // Process faction assignments (client only)
            if (!isServer)
            {
                foreach (var (factionRpc, rpcEntity) in SystemAPI.Query<RefRO<AssignFactionRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                {
                    ProcessFactionAssignment(factionRpc.ValueRO);
                    em.DestroyEntity(rpcEntity);
                }

                // Process game start (client only)
                foreach (var (startRpc, rpcEntity) in SystemAPI.Query<RefRO<StartGameRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                {
                    ProcessGameStart(startRpc.ValueRO);
                    em.DestroyEntity(rpcEntity);
                }
            }
        }

        private void ProcessJoinRequest(JoinGameRpc joinRpc, Entity rpcEntity)
        {
            Debug.Log($"[ConnectionSystem] Player joined: {joinRpc.PlayerName}");

            // Find the connection entity that sent this RPC
            var sourceConnection = SystemAPI.GetComponent<ReceiveRpcCommandRequest>(rpcEntity).SourceConnection;

            // Assign a faction to the new player
            Faction assignedFaction = AssignNextAvailableFaction();

            // Create PlayerConnection component on the connection entity
            if (!EntityManager.HasComponent<PlayerConnection>(sourceConnection))
            {
                EntityManager.AddComponentData(sourceConnection, new PlayerConnection
                {
                    PlayerId = _nextPlayerId++,
                    AssignedFaction = assignedFaction,
                    PlayerName = joinRpc.PlayerName
                });
            }

            // Send faction assignment back to client
            var factionRpc = EntityManager.CreateEntity();
            EntityManager.AddComponentData(factionRpc, new AssignFactionRpc
            {
                PlayerId = _nextPlayerId - 1,
                Faction = assignedFaction
            });
            EntityManager.AddComponentData(factionRpc, new SendRpcCommandRequest { TargetConnection = sourceConnection });

            // Update game state
            RefRW<NetworkGameState> gameState = SystemAPI.GetSingletonRW<NetworkGameState>();
            gameState.ValueRW.TotalPlayers++;

            // Destroy the RPC entity
            EntityManager.DestroyEntity(rpcEntity);
        }

        private void ProcessPlayerReady(PlayerReadyRpc readyRpc, Entity rpcEntity)
        {
            var sourceConnection = SystemAPI.GetComponent<ReceiveRpcCommandRequest>(rpcEntity).SourceConnection;

            // Update player ready state
            if (EntityManager.HasComponent<PlayerConnection>(sourceConnection))
            {
                if (readyRpc.IsReady == 1)
                {
                    if (!EntityManager.HasComponent<PlayerReady>(sourceConnection))
                    {
                        EntityManager.AddComponentData(sourceConnection, new PlayerReady { IsReady = 1 });
                        RefRW<NetworkGameState> gameState = SystemAPI.GetSingletonRW<NetworkGameState>();
                        gameState.ValueRW.ReadyPlayers++;
                    }
                }
                else
                {
                    if (EntityManager.HasComponent<PlayerReady>(sourceConnection))
                    {
                        EntityManager.RemoveComponent<PlayerReady>(sourceConnection);
                        RefRW<NetworkGameState> gameState = SystemAPI.GetSingletonRW<NetworkGameState>();
                        gameState.ValueRW.ReadyPlayers--;
                    }
                }
            }

            EntityManager.DestroyEntity(rpcEntity);
        }

        private void ProcessFactionAssignment(AssignFactionRpc factionRpc)
        {
            Debug.Log($"[ConnectionSystem] Assigned faction: {factionRpc.Faction}");
            GameSettings.LocalPlayerFaction = factionRpc.Faction;
        }

        private void ProcessGameStart(StartGameRpc startRpc)
        {
            Debug.Log($"[ConnectionSystem] Game starting with seed: {startRpc.Seed}");
            GameSettings.SpawnSeed = startRpc.Seed;
            
            // Load game scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }

        private Faction AssignNextAvailableFaction()
        {
            // Count how many players we have and assign next faction
            int playerCount = 0;
            foreach (var connection in SystemAPI.Query<RefRO<PlayerConnection>>())
            {
                playerCount++;
            }

            // Assign factions in order: Blue, Red, Green, Yellow, Purple, Orange, Cyan, Magenta
            return (Faction)(playerCount % 8);
        }
    }
}
