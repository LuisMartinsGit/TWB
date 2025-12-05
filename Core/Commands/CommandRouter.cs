// CommandRouter.cs
// Unified command routing system for local player, remote player, and AI
// Location: Assets/Scripts/Core/Commands/CommandRouter.cs

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.Commands.Types;
using TheWaningBorder.Multiplayer;

namespace TheWaningBorder.Core.Commands
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
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Enable detailed logging of all commands (useful for debugging sync issues)
        /// </summary>
        public static bool LogCommands = false;

        // ═══════════════════════════════════════════════════════════════
        // MOVEMENT COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Issue a move command to a unit.
        /// </summary>
        public static CommandResult IssueMove(Entity unit, float3 destination, 
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null) return CommandResult.EntityNotFound;
            
            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(unit)) 
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Move: {source} -> Entity {unit.Index} to {destination}");

            if (ShouldQueueForLockstep(source))
            {
                QueueMoveForLockstep(em, unit, destination);
                return CommandResult.QueuedForLockstep;
            }

            MoveCommandHelper.Execute(em, unit, destination);
            return CommandResult.Success;
        }

        /// <summary>
        /// Overload with explicit EntityManager (for ECS systems)
        /// </summary>
        public static CommandResult IssueMove(EntityManager em, Entity unit, float3 destination,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || !em.Exists(unit)) 
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Move: {source} -> Entity {unit.Index} to {destination}");

            if (ShouldQueueForLockstep(source))
            {
                QueueMoveForLockstep(em, unit, destination);
                return CommandResult.QueuedForLockstep;
            }

            MoveCommandHelper.Execute(em, unit, destination);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // ATTACK COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Issue an attack command to a unit.
        /// </summary>
        public static CommandResult IssueAttack(Entity unit, Entity target,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null) return CommandResult.EntityNotFound;
            if (target == Entity.Null) return CommandResult.TargetNotFound;

            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(unit))
                return CommandResult.EntityNotFound;
            if (!em.Exists(target))
                return CommandResult.TargetNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Attack: {source} -> Entity {unit.Index} attacks {target.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueAttackForLockstep(em, unit, target);
                return CommandResult.QueuedForLockstep;
            }

            AttackCommandHelper.Execute(em, unit, target);
            return CommandResult.Success;
        }

        public static CommandResult IssueAttack(EntityManager em, Entity unit, Entity target,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || !em.Exists(unit))
                return CommandResult.EntityNotFound;
            if (target == Entity.Null || !em.Exists(target))
                return CommandResult.TargetNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Attack: {source} -> Entity {unit.Index} attacks {target.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueAttackForLockstep(em, unit, target);
                return CommandResult.QueuedForLockstep;
            }

            AttackCommandHelper.Execute(em, unit, target);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // STOP COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Issue a stop command to a unit (clears all commands).
        /// </summary>
        public static CommandResult IssueStop(Entity unit,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null) return CommandResult.EntityNotFound;

            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(unit))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Stop: {source} -> Entity {unit.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueStopForLockstep(em, unit);
                return CommandResult.QueuedForLockstep;
            }

            CommandHelper.ClearAllCommands(em, unit);
            CommandHelper.SetGuardPointToCurrent(em, unit);
            return CommandResult.Success;
        }

        public static CommandResult IssueStop(EntityManager em, Entity unit,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (unit == Entity.Null || !em.Exists(unit))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Stop: {source} -> Entity {unit.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueStopForLockstep(em, unit);
                return CommandResult.QueuedForLockstep;
            }

            CommandHelper.ClearAllCommands(em, unit);
            CommandHelper.SetGuardPointToCurrent(em, unit);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // BUILD COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Issue a build command to a builder unit.
        /// </summary>
        public static CommandResult IssueBuild(Entity builder, Entity targetBuilding,
            string buildingId, float3 position, CommandSource source = CommandSource.LocalPlayer)
        {
            if (builder == Entity.Null) return CommandResult.EntityNotFound;

            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(builder))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Build: {source} -> Builder {builder.Index} constructs {buildingId} at {position}");

            if (ShouldQueueForLockstep(source))
            {
                QueueBuildForLockstep(em, builder, targetBuilding, buildingId, position);
                return CommandResult.QueuedForLockstep;
            }

            BuildCommandHelper.Execute(em, builder, targetBuilding, buildingId, position);
            return CommandResult.Success;
        }

        public static CommandResult IssueBuild(EntityManager em, Entity builder, Entity targetBuilding,
            string buildingId, float3 position, CommandSource source = CommandSource.LocalPlayer)
        {
            if (builder == Entity.Null || !em.Exists(builder))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Build: {source} -> Builder {builder.Index} constructs {buildingId} at {position}");

            if (ShouldQueueForLockstep(source))
            {
                QueueBuildForLockstep(em, builder, targetBuilding, buildingId, position);
                return CommandResult.QueuedForLockstep;
            }

            BuildCommandHelper.Execute(em, builder, targetBuilding, buildingId, position);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // GATHER COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Issue a gather command to a miner/worker unit.
        /// </summary>
        public static CommandResult IssueGather(Entity miner, Entity resourceNode,
            Entity depositLocation, CommandSource source = CommandSource.LocalPlayer)
        {
            if (miner == Entity.Null) return CommandResult.EntityNotFound;
            if (resourceNode == Entity.Null) return CommandResult.TargetNotFound;

            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(miner))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Gather: {source} -> Miner {miner.Index} gathers from {resourceNode.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueGatherForLockstep(em, miner, resourceNode, depositLocation);
                return CommandResult.QueuedForLockstep;
            }

            GatherCommandHelper.Execute(em, miner, resourceNode, depositLocation);
            return CommandResult.Success;
        }

        public static CommandResult IssueGather(EntityManager em, Entity miner, Entity resourceNode,
            Entity depositLocation, CommandSource source = CommandSource.LocalPlayer)
        {
            if (miner == Entity.Null || !em.Exists(miner))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Gather: {source} -> Miner {miner.Index} gathers from {resourceNode.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueGatherForLockstep(em, miner, resourceNode, depositLocation);
                return CommandResult.QueuedForLockstep;
            }

            GatherCommandHelper.Execute(em, miner, resourceNode, depositLocation);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // HEAL COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Issue a heal command to a healer unit.
        /// </summary>
        public static CommandResult IssueHeal(Entity healer, Entity target,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (healer == Entity.Null) return CommandResult.EntityNotFound;
            if (target == Entity.Null) return CommandResult.TargetNotFound;

            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(healer))
                return CommandResult.EntityNotFound;
            if (!em.Exists(target))
                return CommandResult.TargetNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Heal: {source} -> Healer {healer.Index} heals {target.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueHealForLockstep(em, healer, target);
                return CommandResult.QueuedForLockstep;
            }

            HealCommandHelper.Execute(em, healer, target);
            return CommandResult.Success;
        }

        public static CommandResult IssueHeal(EntityManager em, Entity healer, Entity target,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (healer == Entity.Null || !em.Exists(healer))
                return CommandResult.EntityNotFound;
            if (target == Entity.Null || !em.Exists(target))
                return CommandResult.TargetNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] Heal: {source} -> Healer {healer.Index} heals {target.Index}");

            if (ShouldQueueForLockstep(source))
            {
                QueueHealForLockstep(em, healer, target);
                return CommandResult.QueuedForLockstep;
            }

            HealCommandHelper.Execute(em, healer, target);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // RALLY POINT COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Set a rally point for a building.
        /// </summary>
        public static CommandResult SetRallyPoint(Entity building, float3 position,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (building == Entity.Null) return CommandResult.EntityNotFound;

            var em = GetEntityManager();
            if (em.Equals(default(EntityManager)) || !em.Exists(building))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] RallyPoint: {source} -> Building {building.Index} to {position}");

            if (ShouldQueueForLockstep(source))
            {
                QueueRallyPointForLockstep(em, building, position);
                return CommandResult.QueuedForLockstep;
            }

            SetRallyPointDirect(em, building, position);
            return CommandResult.Success;
        }

        public static CommandResult SetRallyPoint(EntityManager em, Entity building, float3 position,
            CommandSource source = CommandSource.LocalPlayer)
        {
            if (building == Entity.Null || !em.Exists(building))
                return CommandResult.EntityNotFound;

            if (LogCommands)
                Debug.Log($"[CommandRouter] RallyPoint: {source} -> Building {building.Index} to {position}");

            if (ShouldQueueForLockstep(source))
            {
                QueueRallyPointForLockstep(em, building, position);
                return CommandResult.QueuedForLockstep;
            }

            SetRallyPointDirect(em, building, position);
            return CommandResult.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        // DIRECT EXECUTION (bypasses lockstep)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Direct command execution - bypasses lockstep routing.
        /// USE WITH CAUTION - only for commands that don't affect game state determinism.
        /// Examples: visual effects, UI updates, local-only state
        /// </summary>
        public static class Direct
        {
            public static void Move(EntityManager em, Entity unit, float3 destination)
            {
                if (unit == Entity.Null || !em.Exists(unit)) return;
                MoveCommandHelper.Execute(em, unit, destination);
            }

            public static void Attack(EntityManager em, Entity unit, Entity target)
            {
                if (unit == Entity.Null || target == Entity.Null) return;
                if (!em.Exists(unit) || !em.Exists(target)) return;
                AttackCommandHelper.Execute(em, unit, target);
            }

            public static void Stop(EntityManager em, Entity unit)
            {
                if (unit == Entity.Null || !em.Exists(unit)) return;
                CommandHelper.ClearAllCommands(em, unit);
                CommandHelper.SetGuardPointToCurrent(em, unit);
            }

            public static void Gather(EntityManager em, Entity miner, Entity resource, Entity deposit)
            {
                if (miner == Entity.Null || !em.Exists(miner)) return;
                GatherCommandHelper.Execute(em, miner, resource, deposit);
            }

            public static void Build(EntityManager em, Entity builder, Entity target, string buildingId, float3 pos)
            {
                if (builder == Entity.Null || !em.Exists(builder)) return;
                BuildCommandHelper.Execute(em, builder, target, buildingId, pos);
            }

            public static void Heal(EntityManager em, Entity healer, Entity target)
            {
                if (healer == Entity.Null || target == Entity.Null) return;
                if (!em.Exists(healer) || !em.Exists(target)) return;
                HealCommandHelper.Execute(em, healer, target);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INTERNAL ROUTING LOGIC
        // ═══════════════════════════════════════════════════════════════

        private static bool ShouldQueueForLockstep(CommandSource source)
        {
            // Only queue if in multiplayer with active lockstep
            if (!GameSettings.IsMultiplayer) return false;
            if (LockstepManager.Instance == null || !LockstepManager.Instance.IsSimulationRunning)
                return false;

            return source switch
            {
                CommandSource.LocalPlayer => true,
                CommandSource.AI => LockstepManager.Instance.IsHost, // Only host queues AI commands
                CommandSource.RemotePlayer => false, // Already synchronized
                CommandSource.System => false,       // Deterministic - execute immediately
                _ => false
            };
        }

        private static EntityManager GetEntityManager()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return default;
            return world.EntityManager;
        }

        private static int GetNetworkId(EntityManager em, Entity entity)
        {
            if (entity == Entity.Null || !em.Exists(entity)) return -1;
            if (!em.HasComponent<NetworkedEntity>(entity)) return -1;
            return em.GetComponentData<NetworkedEntity>(entity).NetworkId;
        }

        // ═══════════════════════════════════════════════════════════════
        // LOCKSTEP QUEUE METHODS
        // ═══════════════════════════════════════════════════════════════

        private static void QueueMoveForLockstep(EntityManager em, Entity unit, float3 destination)
        {
            int networkId = GetNetworkId(em, unit);
            if (networkId <= 0)
            {
                Debug.LogWarning($"[CommandRouter] Entity {unit.Index} has no network ID, executing locally");
                MoveCommandHelper.Execute(em, unit, destination);
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

        private static void QueueAttackForLockstep(EntityManager em, Entity unit, Entity target)
        {
            int unitId = GetNetworkId(em, unit);
            int targetId = GetNetworkId(em, target);

            if (unitId <= 0 || targetId <= 0)
            {
                Debug.LogWarning($"[CommandRouter] Missing network IDs for attack, executing locally");
                AttackCommandHelper.Execute(em, unit, target);
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

        private static void QueueStopForLockstep(EntityManager em, Entity unit)
        {
            int networkId = GetNetworkId(em, unit);
            if (networkId <= 0)
            {
                CommandHelper.ClearAllCommands(em, unit);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Stop,
                EntityNetworkId = networkId
            };
            LockstepManager.Instance.QueueCommand(cmd);
        }

        private static void QueueBuildForLockstep(EntityManager em, Entity builder, Entity targetBuilding,
            string buildingId, float3 position)
        {
            int builderId = GetNetworkId(em, builder);
            int targetId = targetBuilding != Entity.Null ? GetNetworkId(em, targetBuilding) : 0;

            if (builderId <= 0)
            {
                BuildCommandHelper.Execute(em, builder, targetBuilding, buildingId, position);
                return;
            }

            var cmd = new LockstepCommand
            {
                Type = LockstepCommandType.Build,
                EntityNetworkId = builderId,
                TargetEntityId = targetId,
                TargetPosition = position,
                BuildingId = buildingId
            };
            LockstepManager.Instance.QueueCommand(cmd);
        }

        private static void QueueGatherForLockstep(EntityManager em, Entity miner, Entity resource, Entity deposit)
        {
            int minerId = GetNetworkId(em, miner);
            int resourceId = GetNetworkId(em, resource);
            int depositId = deposit != Entity.Null ? GetNetworkId(em, deposit) : 0;

            if (minerId <= 0)
            {
                GatherCommandHelper.Execute(em, miner, resource, deposit);
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

        private static void QueueHealForLockstep(EntityManager em, Entity healer, Entity target)
        {
            int healerId = GetNetworkId(em, healer);
            int targetId = GetNetworkId(em, target);

            if (healerId <= 0 || targetId <= 0)
            {
                HealCommandHelper.Execute(em, healer, target);
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

        private static void QueueRallyPointForLockstep(EntityManager em, Entity building, float3 position)
        {
            int buildingId = GetNetworkId(em, building);

            if (buildingId <= 0)
            {
                SetRallyPointDirect(em, building, position);
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

        private static void SetRallyPointDirect(EntityManager em, Entity building, float3 position)
        {
            if (!em.HasComponent<RallyPoint>(building))
                em.AddComponent<RallyPoint>(building);
            em.SetComponentData(building, new RallyPoint { Position = position, Has = 1 });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SHARED COMMAND HELPER
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Shared utility methods for command execution
    /// </summary>
    public static class CommandHelper
    {
        /// <summary>
        /// Clears all command components from a unit
        /// </summary>
        public static void ClearAllCommands(EntityManager em, Entity unit)
        {
            if (em.HasComponent<Types.MoveCommand>(unit))
                em.RemoveComponent<Types.MoveCommand>(unit);
            if (em.HasComponent<Types.AttackCommand>(unit))
                em.RemoveComponent<Types.AttackCommand>(unit);
            if (em.HasComponent<Types.BuildCommand>(unit))
                em.RemoveComponent<Types.BuildCommand>(unit);
            if (em.HasComponent<Types.GatherCommand>(unit))
                em.RemoveComponent<Types.GatherCommand>(unit);
            if (em.HasComponent<Types.HealCommand>(unit))
                em.RemoveComponent<Types.HealCommand>(unit);
            if (em.HasComponent<Target>(unit))
                em.SetComponentData(unit, new Target { Value = Entity.Null });
            if (em.HasComponent<UserMoveOrder>(unit))
                em.RemoveComponent<UserMoveOrder>(unit);
            if (em.HasComponent<DesiredDestination>(unit))
                em.SetComponentData(unit, new DesiredDestination { Has = 0 });
        }

        /// <summary>
        /// Sets guard point to current position
        /// </summary>
        public static void SetGuardPointToCurrent(EntityManager em, Entity unit)
        {
            if (!em.HasComponent<Unity.Transforms.LocalTransform>(unit)) return;

            var pos = em.GetComponentData<Unity.Transforms.LocalTransform>(unit).Position;
            
            if (em.HasComponent<GuardPoint>(unit))
                em.SetComponentData(unit, new GuardPoint { Position = pos, Has = 1 });
            else
                em.AddComponentData(unit, new GuardPoint { Position = pos, Has = 1 });
        }
    }
}