// Assets/Scripts/Multiplayer/LockstepInputAdapter.cs
// Intercepts player commands and routes them through the lockstep system
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Adapter that intercepts commands from RTSInput and routes them through
    /// the lockstep system instead of executing them immediately.
    /// 
    /// In single-player: Commands execute immediately via CommandGateway
    /// In multiplayer: Commands are queued in LockstepManager and execute on the scheduled tick
    /// </summary>
    public static class LockstepInputAdapter
    {
        /// <summary>
        /// Check if lockstep is active and running
        /// </summary>
        private static bool IsLockstepActive()
        {
            return GameSettings.IsMultiplayer && 
                   LockstepManager.Instance != null && 
                   LockstepManager.Instance.IsSimulationRunning;
        }

        /// <summary>
        /// Issue a move command (replaces direct CommandGateway.IssueMove calls)
        /// </summary>
        public static void IssueMove(EntityManager em, Entity unit, float3 destination)
        {
            if (!IsLockstepActive())
            {
                // Single-player or lockstep not ready: execute immediately
                CommandGateway.IssueMove(em, unit, destination);
                return;
            }

            // Multiplayer: queue command
            int networkId = GetNetworkId(em, unit);
            if (networkId <= 0)
            {
                Debug.LogWarning("[LockstepInput] Cannot issue move - entity has no network ID, executing locally");
                CommandGateway.IssueMove(em, unit, destination);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Move,
                EntityNetworkId = networkId,
                TargetPosition = destination
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        /// <summary>
        /// Issue an attack command
        /// </summary>
        public static void IssueAttack(EntityManager em, Entity unit, Entity target)
        {
            if (!IsLockstepActive())
            {
                CommandGateway.IssueAttack(em, unit, target);
                return;
            }

            int unitId = GetNetworkId(em, unit);
            int targetId = GetNetworkId(em, target);
            if (unitId <= 0 || targetId <= 0)
            {
                Debug.LogWarning("[LockstepInput] Cannot issue attack - entity has no network ID, executing locally");
                CommandGateway.IssueAttack(em, unit, target);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Attack,
                EntityNetworkId = unitId,
                TargetEntityId = targetId
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        /// <summary>
        /// Issue a stop command
        /// </summary>
        public static void IssueStop(EntityManager em, Entity unit)
        {
            if (!IsLockstepActive())
            {
                CommandGateway.IssueStop(em, unit);
                return;
            }

            int networkId = GetNetworkId(em, unit);
            if (networkId <= 0)
            {
                CommandGateway.IssueStop(em, unit);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Stop,
                EntityNetworkId = networkId
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        /// <summary>
        /// Issue a gather command
        /// </summary>
        public static void IssueGather(EntityManager em, Entity miner, Entity resourceNode, Entity depositLocation)
        {
            if (!IsLockstepActive())
            {
                CommandGateway.IssueGather(em, miner, resourceNode, depositLocation);
                return;
            }

            int minerId = GetNetworkId(em, miner);
            int resourceId = GetNetworkId(em, resourceNode);
            int depositId = GetNetworkId(em, depositLocation);
            if (minerId <= 0 || resourceId <= 0)
            {
                CommandGateway.IssueGather(em, miner, resourceNode, depositLocation);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Gather,
                EntityNetworkId = minerId,
                TargetEntityId = resourceId,
                SecondaryTargetId = depositId
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        /// <summary>
        /// Issue a build command
        /// </summary>
        public static void IssueBuild(EntityManager em, Entity builder, Entity targetBuilding, string buildingId, float3 position)
        {
            if (!IsLockstepActive())
            {
                CommandGateway.IssueBuild(em, builder, targetBuilding, buildingId, position);
                return;
            }

            int builderId = GetNetworkId(em, builder);
            int targetBuildingId = GetNetworkId(em, targetBuilding);
            if (builderId <= 0)
            {
                CommandGateway.IssueBuild(em, builder, targetBuilding, buildingId, position);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Build,
                EntityNetworkId = builderId,
                TargetPosition = position,
                TargetEntityId = targetBuildingId,
                BuildingId = buildingId
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        /// <summary>
        /// Issue a heal command
        /// </summary>
        public static void IssueHeal(EntityManager em, Entity healer, Entity target)
        {
            if (!IsLockstepActive())
            {
                CommandGateway.IssueHeal(em, healer, target);
                return;
            }

            int healerId = GetNetworkId(em, healer);
            int targetId = GetNetworkId(em, target);
            if (healerId <= 0 || targetId <= 0)
            {
                CommandGateway.IssueHeal(em, healer, target);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Heal,
                EntityNetworkId = healerId,
                TargetEntityId = targetId
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        /// <summary>
        /// Set rally point for a building
        /// </summary>
        public static void SetRallyPoint(EntityManager em, Entity building, float3 position)
        {
            if (!IsLockstepActive())
            {
                // Execute locally
                if (!em.HasComponent<RallyPoint>(building))
                    em.AddComponent<RallyPoint>(building);
                em.SetComponentData(building, new RallyPoint { Position = position, Has = 1 });
                return;
            }

            int buildingId = GetNetworkId(em, building);
            if (buildingId <= 0)
            {
                if (!em.HasComponent<RallyPoint>(building))
                    em.AddComponent<RallyPoint>(building);
                em.SetComponentData(building, new RallyPoint { Position = position, Has = 1 });
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.SetRally,
                EntityNetworkId = buildingId,
                TargetPosition = position
            };

            LockstepManager.Instance.QueueCommand(cmd);
        }

        // Helper to get network ID
        private static int GetNetworkId(EntityManager em, Entity entity)
        {
            if (entity == Entity.Null || !em.Exists(entity)) return -1;
            if (!em.HasComponent<NetworkedEntity>(entity)) return -1;
            return em.GetComponentData<NetworkedEntity>(entity).NetworkId;
        }
    }
}