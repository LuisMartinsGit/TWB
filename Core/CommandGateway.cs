// Assets/Scripts/Core/CommandGateway.cs
// Updated with ECB-based methods for use within ECS systems

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Core
{
    /// <summary>
    /// CommandGateway provides a unified API for issuing commands to units.
    /// All player and AI commands should flow through this gateway to ensure consistent behavior.
    /// 
    /// IMPORTANT: When calling from within ECS systems (ISystem.OnUpdate), use the ECB overloads
    /// to avoid "Structural changes are not allowed while iterating" errors.
    /// </summary>
    public static class CommandGateway
    {
        // ==================== Movement Commands ====================

        /// <summary>
        /// Command a unit to move to a specific destination.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        public static void IssueMove(EntityManager em, Entity unit, float3 destination)
        {
            if (!em.Exists(unit)) return;

            // Clear conflicting commands
            ClearAllCommands(em, unit);

            // Add MoveCommand
            if (!em.HasComponent<MoveCommand>(unit))
                em.AddComponentData(unit, new MoveCommand { Destination = destination });
            else
                em.SetComponentData(unit, new MoveCommand { Destination = destination });

            // Add UserMoveOrder tag to prevent auto-targeting from overriding
            if (!em.HasComponent<UserMoveOrder>(unit))
                em.AddComponent<UserMoveOrder>(unit);

            // Update guard point to new destination
            if (em.HasComponent<GuardPoint>(unit))
            {
                em.SetComponentData(unit, new GuardPoint
                {
                    Position = destination,
                    Has = 1
                });
            }
            else
            {
                em.AddComponentData(unit, new GuardPoint
                {
                    Position = destination,
                    Has = 1
                });
            }
        }

        /// <summary>
        /// ECB-safe version: Command a unit to move to a specific destination.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        public static void IssueMove(EntityManager em, EntityCommandBuffer ecb, Entity unit, float3 destination)
        {
            if (!em.Exists(unit)) return;

            // Clear conflicting commands via ECB
            ClearAllCommands(em, ecb, unit);

            // Add MoveCommand
            if (!em.HasComponent<MoveCommand>(unit))
                ecb.AddComponent(unit, new MoveCommand { Destination = destination });
            else
                ecb.SetComponent(unit, new MoveCommand { Destination = destination });

            // Add UserMoveOrder tag
            if (!em.HasComponent<UserMoveOrder>(unit))
                ecb.AddComponent<UserMoveOrder>(unit);

            // Update guard point
            if (em.HasComponent<GuardPoint>(unit))
            {
                ecb.SetComponent(unit, new GuardPoint { Position = destination, Has = 1 });
            }
            else
            {
                ecb.AddComponent(unit, new GuardPoint { Position = destination, Has = 1 });
            }
        }

        // ==================== Combat Commands ====================

        /// <summary>
        /// Command a unit to attack a specific target.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        public static void IssueAttack(EntityManager em, Entity unit, Entity target)
        {
            if (!em.Exists(unit) || !em.Exists(target)) return;

            // Clear conflicting commands (but NOT MoveCommand - combat system handles chasing)
            if (em.HasComponent<BuildCommand>(unit))
                em.RemoveComponent<BuildCommand>(unit);
            if (em.HasComponent<GatherCommand>(unit))
                em.RemoveComponent<GatherCommand>(unit);
            if (em.HasComponent<HealCommand>(unit))
                em.RemoveComponent<HealCommand>(unit);

            // Clear UserMoveOrder to allow combat system to take over
            if (em.HasComponent<UserMoveOrder>(unit))
                em.RemoveComponent<UserMoveOrder>(unit);

            // Add AttackCommand
            if (!em.HasComponent<AttackCommand>(unit))
                em.AddComponentData(unit, new AttackCommand { Target = target });
            else
                em.SetComponentData(unit, new AttackCommand { Target = target });

            // Set guard point to current position
            if (em.HasComponent<LocalTransform>(unit))
            {
                var pos = em.GetComponentData<LocalTransform>(unit).Position;
                if (em.HasComponent<GuardPoint>(unit))
                {
                    em.SetComponentData(unit, new GuardPoint { Position = pos, Has = 1 });
                }
                else
                {
                    em.AddComponentData(unit, new GuardPoint { Position = pos, Has = 1 });
                }
            }
        }

        /// <summary>
        /// ECB-safe version: Command a unit to attack a specific target.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        public static void IssueAttack(EntityManager em, EntityCommandBuffer ecb, Entity unit, Entity target)
        {
            if (!em.Exists(unit) || !em.Exists(target)) return;

            // Clear conflicting commands via ECB
            if (em.HasComponent<BuildCommand>(unit))
                ecb.RemoveComponent<BuildCommand>(unit);
            if (em.HasComponent<GatherCommand>(unit))
                ecb.RemoveComponent<GatherCommand>(unit);
            if (em.HasComponent<HealCommand>(unit))
                ecb.RemoveComponent<HealCommand>(unit);
            if (em.HasComponent<UserMoveOrder>(unit))
                ecb.RemoveComponent<UserMoveOrder>(unit);

            // Add AttackCommand
            if (!em.HasComponent<AttackCommand>(unit))
                ecb.AddComponent(unit, new AttackCommand { Target = target });
            else
                ecb.SetComponent(unit, new AttackCommand { Target = target });

            // Set guard point to current position
            if (em.HasComponent<LocalTransform>(unit))
            {
                var pos = em.GetComponentData<LocalTransform>(unit).Position;
                if (em.HasComponent<GuardPoint>(unit))
                    ecb.SetComponent(unit, new GuardPoint { Position = pos, Has = 1 });
                else
                    ecb.AddComponent(unit, new GuardPoint { Position = pos, Has = 1 });
            }
        }

        /// <summary>
        /// Command a unit to stop all current actions.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        public static void IssueStop(EntityManager em, Entity unit)
        {
            if (!em.Exists(unit)) return;

            ClearAllCommands(em, unit);

            // Clear destination
            if (em.HasComponent<DesiredDestination>(unit))
                em.SetComponentData(unit, new DesiredDestination { Has = 0 });

            // Set guard point to current position
            if (em.HasComponent<LocalTransform>(unit))
            {
                var pos = em.GetComponentData<LocalTransform>(unit).Position;
                if (em.HasComponent<GuardPoint>(unit))
                {
                    em.SetComponentData(unit, new GuardPoint { Position = pos, Has = 1 });
                }
                else
                {
                    em.AddComponentData(unit, new GuardPoint { Position = pos, Has = 1 });
                }
            }
        }

        /// <summary>
        /// ECB-safe version: Command a unit to stop all current actions.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        public static void IssueStop(EntityManager em, EntityCommandBuffer ecb, Entity unit)
        {
            if (!em.Exists(unit)) return;

            ClearAllCommands(em, ecb, unit);

            // Clear destination
            if (em.HasComponent<DesiredDestination>(unit))
                ecb.SetComponent(unit, new DesiredDestination { Has = 0 });

            // Set guard point to current position
            if (em.HasComponent<LocalTransform>(unit))
            {
                var pos = em.GetComponentData<LocalTransform>(unit).Position;
                if (em.HasComponent<GuardPoint>(unit))
                    ecb.SetComponent(unit, new GuardPoint { Position = pos, Has = 1 });
                else
                    ecb.AddComponent(unit, new GuardPoint { Position = pos, Has = 1 });
            }
        }

        // ==================== Work Commands ====================

        /// <summary>
        /// Command a builder to construct a building at a specific position.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        public static void IssueBuild(EntityManager em, Entity builder, Entity targetBuilding, string buildingId, float3 position)
        {
            if (!em.Exists(builder)) return;
            if (!em.HasComponent<CanBuild>(builder)) return;

            ClearAllCommands(em, builder);

            if (!em.HasComponent<BuildCommand>(builder))
            {
                em.AddComponentData(builder, new BuildCommand
                {
                    BuildingId = buildingId,
                    Position = position,
                    TargetBuilding = targetBuilding
                });
            }
            else
            {
                em.SetComponentData(builder, new BuildCommand
                {
                    BuildingId = buildingId,
                    Position = position,
                    TargetBuilding = targetBuilding
                });
            }
        }

        /// <summary>
        /// ECB-safe version: Command a builder to construct a building.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        public static void IssueBuild(EntityManager em, EntityCommandBuffer ecb, Entity builder, Entity targetBuilding, string buildingId, float3 position)
        {
            if (!em.Exists(builder)) return;
            if (!em.HasComponent<CanBuild>(builder)) return;

            ClearAllCommands(em, ecb, builder);

            var cmd = new BuildCommand
            {
                BuildingId = buildingId,
                Position = position,
                TargetBuilding = targetBuilding
            };

            if (!em.HasComponent<BuildCommand>(builder))
                ecb.AddComponent(builder, cmd);
            else
                ecb.SetComponent(builder, cmd);
        }

        /// <summary>
        /// Command a miner to gather from a resource node.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        public static void IssueGather(EntityManager em, Entity miner, Entity resourceNode, Entity depositLocation)
        {
            if (!em.Exists(miner)) return;

            ClearAllCommands(em, miner);

            var cmd = new GatherCommand
            {
                ResourceNode = resourceNode,
                DepositLocation = depositLocation
            };

            if (!em.HasComponent<GatherCommand>(miner))
                em.AddComponentData(miner, cmd);
            else
                em.SetComponentData(miner, cmd);
        }

        /// <summary>
        /// ECB-safe version: Command a miner to gather from a resource node.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        public static void IssueGather(EntityManager em, EntityCommandBuffer ecb, Entity miner, Entity resourceNode, Entity depositLocation)
        {
            if (!em.Exists(miner)) return;

            ClearAllCommands(em, ecb, miner);

            var cmd = new GatherCommand
            {
                ResourceNode = resourceNode,
                DepositLocation = depositLocation
            };

            if (!em.HasComponent<GatherCommand>(miner))
                ecb.AddComponent(miner, cmd);
            else
                ecb.SetComponent(miner, cmd);
        }

        /// <summary>
        /// Command a healer to heal a target unit.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        public static void IssueHeal(EntityManager em, Entity healer, Entity target)
        {
            if (!em.Exists(healer) || !em.Exists(target)) return;

            ClearAllCommands(em, healer);

            if (!em.HasComponent<HealCommand>(healer))
                em.AddComponentData(healer, new HealCommand { Target = target });
            else
                em.SetComponentData(healer, new HealCommand { Target = target });
        }

        /// <summary>
        /// ECB-safe version: Command a healer to heal a target unit.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        public static void IssueHeal(EntityManager em, EntityCommandBuffer ecb, Entity healer, Entity target)
        {
            if (!em.Exists(healer) || !em.Exists(target)) return;

            ClearAllCommands(em, ecb, healer);

            if (!em.HasComponent<HealCommand>(healer))
                ecb.AddComponent(healer, new HealCommand { Target = target });
            else
                ecb.SetComponent(healer, new HealCommand { Target = target });
        }

        // ==================== Helper Methods ====================

        /// <summary>
        /// Clears all command components from a unit.
        /// WARNING: Do NOT call this from within ECS system iteration loops. Use the ECB overload instead.
        /// </summary>
        private static void ClearAllCommands(EntityManager em, Entity unit)
        {
            if (em.HasComponent<MoveCommand>(unit))
                em.RemoveComponent<MoveCommand>(unit);
            if (em.HasComponent<AttackCommand>(unit))
                em.RemoveComponent<AttackCommand>(unit);
            if (em.HasComponent<BuildCommand>(unit))
                em.RemoveComponent<BuildCommand>(unit);
            if (em.HasComponent<GatherCommand>(unit))
                em.RemoveComponent<GatherCommand>(unit);
            if (em.HasComponent<HealCommand>(unit))
                em.RemoveComponent<HealCommand>(unit);
            if (em.HasComponent<UserMoveOrder>(unit))
                em.RemoveComponent<UserMoveOrder>(unit);
        }

        /// <summary>
        /// ECB-safe version: Clears all command components from a unit.
        /// Safe to call from within ECS system iteration loops.
        /// </summary>
        private static void ClearAllCommands(EntityManager em, EntityCommandBuffer ecb, Entity unit)
        {
            if (em.HasComponent<MoveCommand>(unit))
                ecb.RemoveComponent<MoveCommand>(unit);
            if (em.HasComponent<AttackCommand>(unit))
                ecb.RemoveComponent<AttackCommand>(unit);
            if (em.HasComponent<BuildCommand>(unit))
                ecb.RemoveComponent<BuildCommand>(unit);
            if (em.HasComponent<GatherCommand>(unit))
                ecb.RemoveComponent<GatherCommand>(unit);
            if (em.HasComponent<HealCommand>(unit))
                ecb.RemoveComponent<HealCommand>(unit);
            if (em.HasComponent<UserMoveOrder>(unit))
                ecb.RemoveComponent<UserMoveOrder>(unit);
        }
    }
}