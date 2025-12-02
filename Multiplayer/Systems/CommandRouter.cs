// Assets/Scripts/Core/CommandRouter.cs
// Unified command routing system for local player, remote player, and AI
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.Core
{
    /// <summary>
    /// CommandRouter is the SINGLE ENTRY POINT for all game commands.
    /// 
    /// Whether commands come from:
    /// - Local player (RTSInput, UI panels)
    /// - Remote player (network/lockstep)
    /// - AI (AITacticalManager, AIEconomyManager, etc.)
    /// 
    /// They ALL flow through here. This ensures:
    /// 1. Consistent behavior across all command sources
    /// 2. Proper multiplayer synchronization when needed
    /// 3. Easy debugging (single point to log all commands)
    /// 4. Clean separation of concerns
    /// 
    /// USAGE:
    /// - For player input: CommandRouter.IssueMove(entity, destination)
    /// - For AI: CommandRouter.IssueMove(entity, destination, CommandSource.AI)
    /// - The router handles whether to execute immediately or queue for lockstep
    /// </summary>
    public static class CommandRouter
    {
        // ==================== Configuration ====================
        
        /// <summary>
        /// Enable detailed logging of all commands (useful for debugging sync issues)
        /// </summary>
        public static bool LogCommands = false;
        
        // ==================== Command Sources ====================
        
        /// <summary>
        /// Identifies where a command originated from
        /// </summary>
        public enum CommandSource
        {
            /// <summary>Local human player (RTSInput, UI clicks)</summary>
            LocalPlayer,
            
            /// <summary>Remote human player (received via network)</summary>
            RemotePlayer,
            
            /// <summary>AI system (AITacticalManager, etc.)</summary>
            AI,
            
            /// <summary>Internal system (auto-targeting, spawning, etc.)</summary>
            System
        }
        
        // ==================== Movement Commands ====================
        
        /// <summary>
        /// Issue a move command to a unit.
        /// Default source is LocalPlayer (for RTSInput calls).
        /// </summary>
        public static void IssueMove(Entity unit, float3 destination, CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(unit)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Move command from {source}: Entity {unit.Index} -> {destination}");
            
            // Route based on source and game mode
            if (ShouldQueueForLockstep(source))
            {
                QueueMoveForLockstep(em, unit, destination);
            }
            else
            {
                // Execute immediately
                CommandGateway.IssueMove(em, unit, destination);
            }
        }
        
        /// <summary>
        /// Overload that takes EntityManager directly (for ECS systems)
        /// </summary>
        public static void IssueMove(EntityManager em, Entity unit, float3 destination, CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Move command from {source}: Entity {unit.Index} -> {destination}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueMoveForLockstep(em, unit, destination);
            }
            else
            {
                CommandGateway.IssueMove(em, unit, destination);
            }
        }
        
        // ==================== Attack Commands ====================
        
        /// <summary>
        /// Issue an attack command to a unit.
        /// </summary>
        public static void IssueAttack(Entity unit, Entity target, CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || target == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(unit) || !em.Exists(target)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Attack command from {source}: Entity {unit.Index} -> Target {target.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueAttackForLockstep(em, unit, target);
            }
            else
            {
                CommandGateway.IssueAttack(em, unit, target);
            }
        }
        
        public static void IssueAttack(EntityManager em, Entity unit, Entity target, CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || target == Entity.Null) return;
            if (!em.Exists(unit) || !em.Exists(target)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Attack command from {source}: Entity {unit.Index} -> Target {target.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueAttackForLockstep(em, unit, target);
            }
            else
            {
                CommandGateway.IssueAttack(em, unit, target);
            }
        }
        
        // ==================== Stop Commands ====================
        
        /// <summary>
        /// Issue a stop command to a unit.
        /// </summary>
        public static void IssueStop(Entity unit, CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(unit)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Stop command from {source}: Entity {unit.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueStopForLockstep(em, unit);
            }
            else
            {
                CommandGateway.IssueStop(em, unit);
            }
        }
        
        public static void IssueStop(EntityManager em, Entity unit, CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Stop command from {source}: Entity {unit.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueStopForLockstep(em, unit);
            }
            else
            {
                CommandGateway.IssueStop(em, unit);
            }
        }
        
        // ==================== Gather Commands ====================
        
        /// <summary>
        /// Issue a gather command to a miner.
        /// </summary>
        public static void IssueGather(Entity miner, Entity resourceNode, Entity depositLocation, 
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (miner == Entity.Null || resourceNode == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(miner)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Gather command from {source}: Miner {miner.Index} -> Resource {resourceNode.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueGatherForLockstep(em, miner, resourceNode, depositLocation);
            }
            else
            {
                CommandGateway.IssueGather(em, miner, resourceNode, depositLocation);
            }
        }
        
        public static void IssueGather(EntityManager em, Entity miner, Entity resourceNode, Entity depositLocation,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (miner == Entity.Null || resourceNode == Entity.Null) return;
            if (!em.Exists(miner)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Gather command from {source}: Miner {miner.Index} -> Resource {resourceNode.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueGatherForLockstep(em, miner, resourceNode, depositLocation);
            }
            else
            {
                CommandGateway.IssueGather(em, miner, resourceNode, depositLocation);
            }
        }
        
        // ==================== Build Commands ====================
        
        /// <summary>
        /// Issue a build command to a builder.
        /// </summary>
        public static void IssueBuild(Entity builder, Entity targetBuilding, string buildingId, float3 position,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (builder == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(builder)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Build command from {source}: Builder {builder.Index} -> {buildingId} at {position}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueBuildForLockstep(em, builder, targetBuilding, buildingId, position);
            }
            else
            {
                CommandGateway.IssueBuild(em, builder, targetBuilding, buildingId, position);
            }
        }
        
        public static void IssueBuild(EntityManager em, Entity builder, Entity targetBuilding, string buildingId, float3 position,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (builder == Entity.Null || !em.Exists(builder)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Build command from {source}: Builder {builder.Index} -> {buildingId} at {position}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueBuildForLockstep(em, builder, targetBuilding, buildingId, position);
            }
            else
            {
                CommandGateway.IssueBuild(em, builder, targetBuilding, buildingId, position);
            }
        }
        
        // ==================== Heal Commands ====================
        
        /// <summary>
        /// Issue a heal command to a healer unit.
        /// </summary>
        public static void IssueHeal(Entity healer, Entity target, CommandSource source = CommandSource.LocalPlayer)
        {
            if (healer == Entity.Null || target == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(healer) || !em.Exists(target)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Heal command from {source}: Healer {healer.Index} -> Target {target.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueHealForLockstep(em, healer, target);
            }
            else
            {
                CommandGateway.IssueHeal(em, healer, target);
            }
        }
        
        public static void IssueHeal(EntityManager em, Entity healer, Entity target, CommandSource source = CommandSource.LocalPlayer)
        {
            if (healer == Entity.Null || target == Entity.Null) return;
            if (!em.Exists(healer) || !em.Exists(target)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Heal command from {source}: Healer {healer.Index} -> Target {target.Index}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueHealForLockstep(em, healer, target);
            }
            else
            {
                CommandGateway.IssueHeal(em, healer, target);
            }
        }
        
        // ==================== Rally Point Commands ====================
        
        /// <summary>
        /// Set rally point for a building.
        /// </summary>
        public static void SetRallyPoint(Entity building, float3 position, CommandSource source = CommandSource.LocalPlayer)
        {
            if (building == Entity.Null) return;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(building)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Rally point from {source}: Building {building.Index} -> {position}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueRallyPointForLockstep(em, building, position);
            }
            else
            {
                // Execute immediately
                if (!em.HasComponent<RallyPoint>(building))
                    em.AddComponent<RallyPoint>(building);
                em.SetComponentData(building, new RallyPoint { Position = position, Has = 1 });
            }
        }
        
        public static void SetRallyPoint(EntityManager em, Entity building, float3 position, CommandSource source = CommandSource.LocalPlayer)
        {
            if (building == Entity.Null || !em.Exists(building)) return;
            
            if (LogCommands)
                Debug.Log($"[CommandRouter] Rally point from {source}: Building {building.Index} -> {position}");
            
            if (ShouldQueueForLockstep(source))
            {
                QueueRallyPointForLockstep(em, building, position);
            }
            else
            {
                if (!em.HasComponent<RallyPoint>(building))
                    em.AddComponent<RallyPoint>(building);
                em.SetComponentData(building, new RallyPoint { Position = position, Has = 1 });
            }
        }
        
        // ==================== Direct Execution (bypasses lockstep) ====================
        
        /// <summary>
        /// Execute a command immediately, bypassing lockstep.
        /// USE WITH CAUTION - only for commands that don't affect game state determinism.
        /// Examples: visual effects, UI updates, local-only state
        /// </summary>
        public static class Direct
        {
            public static void Move(EntityManager em, Entity unit, float3 destination)
            {
                if (unit == Entity.Null || !em.Exists(unit)) return;
                CommandGateway.IssueMove(em, unit, destination);
            }
            
            public static void Attack(EntityManager em, Entity unit, Entity target)
            {
                if (unit == Entity.Null || target == Entity.Null) return;
                if (!em.Exists(unit) || !em.Exists(target)) return;
                CommandGateway.IssueAttack(em, unit, target);
            }
            
            public static void Stop(EntityManager em, Entity unit)
            {
                if (unit == Entity.Null || !em.Exists(unit)) return;
                CommandGateway.IssueStop(em, unit);
            }
            
            public static void Gather(EntityManager em, Entity miner, Entity resource, Entity deposit)
            {
                if (miner == Entity.Null || !em.Exists(miner)) return;
                CommandGateway.IssueGather(em, miner, resource, deposit);
            }
            
            public static void Build(EntityManager em, Entity builder, Entity target, string buildingId, float3 pos)
            {
                if (builder == Entity.Null || !em.Exists(builder)) return;
                CommandGateway.IssueBuild(em, builder, target, buildingId, pos);
            }
            
            public static void Heal(EntityManager em, Entity healer, Entity target)
            {
                if (healer == Entity.Null || target == Entity.Null) return;
                if (!em.Exists(healer) || !em.Exists(target)) return;
                CommandGateway.IssueHeal(em, healer, target);
            }
        }
        
        // ==================== Internal Routing Logic ====================
        
        /// <summary>
        /// Determines if a command should be queued for lockstep synchronization.
        /// </summary>
        private static bool ShouldQueueForLockstep(CommandSource source)
        {
            // Only queue if:
            // 1. We're in multiplayer mode
            // 2. Lockstep system is active
            // 3. The command is from a LOCAL source that needs to be synchronized
            
            if (!GameSettings.IsMultiplayer)
                return false;
            
            if (Multiplayer.LockstepManager.Instance == null || 
                !Multiplayer.LockstepManager.Instance.IsSimulationRunning)
                return false;
            
            // Commands that need lockstep synchronization:
            // - LocalPlayer: Human player commands need to be sent to other players
            // - AI: AI commands also need to be synchronized (AI runs on all clients deterministically,
            //       but to avoid desyncs from timing differences, we still route through lockstep)
            //
            // Commands that DON'T need lockstep:
            // - RemotePlayer: These already came through lockstep from another player
            // - System: Internal commands that are deterministic consequences of game state
            
            switch (source)
            {
                case CommandSource.LocalPlayer:
                    return true;
                    
                case CommandSource.AI:
                    // AI commands should also go through lockstep to ensure all clients
                    // process them at the same tick. The AI runs deterministically on all clients,
                    // but we queue commands to ensure they execute at the same game tick.
                    // 
                    // IMPORTANT: Only the HOST should issue AI commands in multiplayer.
                    // Clients should NOT run AI decision-making.
                    return Multiplayer.LockstepManager.Instance.IsHost;
                    
                case CommandSource.RemotePlayer:
                    // Already synchronized - execute immediately
                    return false;
                    
                case CommandSource.System:
                    // System commands are deterministic - execute immediately
                    return false;
                    
                default:
                    return false;
            }
        }
        
        private static EntityManager GetEntityManager()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return default;
            return world.EntityManager;
        }
        
        // ==================== Lockstep Queue Methods ====================
        
        private static void QueueMoveForLockstep(EntityManager em, Entity unit, float3 destination)
        {
            int networkId = GetNetworkId(em, unit);
            if (networkId <= 0)
            {
                Debug.LogWarning($"[CommandRouter] Cannot queue move - entity {unit.Index} has no network ID, executing locally");
                CommandGateway.IssueMove(em, unit, destination);
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.Move,
                EntityNetworkId = networkId,
                TargetPosition = destination
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static void QueueAttackForLockstep(EntityManager em, Entity unit, Entity target)
        {
            int unitId = GetNetworkId(em, unit);
            int targetId = GetNetworkId(em, target);
            
            if (unitId <= 0 || targetId <= 0)
            {
                Debug.LogWarning($"[CommandRouter] Cannot queue attack - missing network IDs, executing locally");
                CommandGateway.IssueAttack(em, unit, target);
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.Attack,
                EntityNetworkId = unitId,
                TargetEntityId = targetId
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static void QueueStopForLockstep(EntityManager em, Entity unit)
        {
            int networkId = GetNetworkId(em, unit);
            if (networkId <= 0)
            {
                CommandGateway.IssueStop(em, unit);
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.Stop,
                EntityNetworkId = networkId
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static void QueueGatherForLockstep(EntityManager em, Entity miner, Entity resource, Entity deposit)
        {
            int minerId = GetNetworkId(em, miner);
            int resourceId = GetNetworkId(em, resource);
            int depositId = GetNetworkId(em, deposit);
            
            if (minerId <= 0 || resourceId <= 0)
            {
                CommandGateway.IssueGather(em, miner, resource, deposit);
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.Gather,
                EntityNetworkId = minerId,
                TargetEntityId = resourceId,
                SecondaryTargetId = depositId
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static void QueueBuildForLockstep(EntityManager em, Entity builder, Entity targetBuilding, 
            string buildingId, float3 position)
        {
            int builderId = GetNetworkId(em, builder);
            int targetId = GetNetworkId(em, targetBuilding);
            
            if (builderId <= 0)
            {
                CommandGateway.IssueBuild(em, builder, targetBuilding, buildingId, position);
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.Build,
                EntityNetworkId = builderId,
                TargetEntityId = targetId,
                TargetPosition = position,
                BuildingId = buildingId
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static void QueueHealForLockstep(EntityManager em, Entity healer, Entity target)
        {
            int healerId = GetNetworkId(em, healer);
            int targetId = GetNetworkId(em, target);
            
            if (healerId <= 0 || targetId <= 0)
            {
                CommandGateway.IssueHeal(em, healer, target);
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.Heal,
                EntityNetworkId = healerId,
                TargetEntityId = targetId
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static void QueueRallyPointForLockstep(EntityManager em, Entity building, float3 position)
        {
            int buildingId = GetNetworkId(em, building);
            
            if (buildingId <= 0)
            {
                if (!em.HasComponent<RallyPoint>(building))
                    em.AddComponent<RallyPoint>(building);
                em.SetComponentData(building, new RallyPoint { Position = position, Has = 1 });
                return;
            }
            
            var cmd = new Multiplayer.LockstepCommand
            {
                Type = Multiplayer.LockstepCommandType.SetRally,
                EntityNetworkId = buildingId,
                TargetPosition = position
            };
            
            Multiplayer.LockstepManager.Instance.QueueCommand(cmd);
        }
        
        private static int GetNetworkId(EntityManager em, Entity entity)
        {
            if (entity == Entity.Null || !em.Exists(entity)) return -1;
            if (!em.HasComponent<Multiplayer.NetworkedEntity>(entity)) return -1;
            return em.GetComponentData<Multiplayer.NetworkedEntity>(entity).NetworkId;
        }
    }
}