using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Core
{
    /// <summary>
    /// CommandGateway provides a unified API for issuing commands to units.
    /// All player and AI commands should flow through this gateway to ensure consistent behavior.
    /// </summary>
    public static class CommandGateway
    {
        // ==================== Movement Commands ====================

        /// <summary>
        /// Command a unit to move to a specific destination.
        /// Clears any existing attack, build, gather, or heal commands.
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

        // ==================== Combat Commands ====================

        /// <summary>
        /// Command a unit to attack a specific target.
        /// Clears any existing move, build, gather, or heal commands.
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
                    em.SetComponentData(unit, new GuardPoint
                    {
                        Position = pos,
                        Has = 1
                    });
                }
                else
                {
                    em.AddComponentData(unit, new GuardPoint
                    {
                        Position = pos,
                        Has = 1
                    });
                }
            }
        }

        /// <summary>
        /// Command a unit to stop all current actions.
        /// Clears all commands and sets guard point to current position.
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
                    em.SetComponentData(unit, new GuardPoint
                    {
                        Position = pos,
                        Has = 1
                    });
                }
                else
                {
                    em.AddComponentData(unit, new GuardPoint
                    {
                        Position = pos,
                        Has = 1
                    });
                }
            }
        }

        // ==================== Work Commands ====================

        /// <summary>
        /// Command a builder to construct a building at a specific position.
        /// The building entity should be created first (in ghost state) and passed here.
        /// </summary>
        public static void IssueBuild(EntityManager em, Entity builder, Entity targetBuilding, string buildingId, float3 position)
        {
            if (!em.Exists(builder)) return;
            if (!em.HasComponent<CanBuild>(builder)) return;

            // Clear conflicting commands
            ClearAllCommands(em, builder);

            // Add BuildCommand
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
        /// Command a miner to gather from a resource node.
        /// </summary>
        public static void IssueGather(EntityManager em, Entity miner, Entity resourceNode, Entity depositLocation)
        {
            if (!em.Exists(miner) || !em.Exists(resourceNode)) return;

            // Clear conflicting commands
            ClearAllCommands(em, miner);

            // Add GatherCommand
            if (!em.HasComponent<GatherCommand>(miner))
            {
                em.AddComponentData(miner, new GatherCommand
                {
                    ResourceNode = resourceNode,
                    DepositLocation = depositLocation
                });
            }
            else
            {
                em.SetComponentData(miner, new GatherCommand
                {
                    ResourceNode = resourceNode,
                    DepositLocation = depositLocation
                });
            }
        }

        /// <summary>
        /// Command a healer to heal a friendly unit.
        /// </summary>
        public static void IssueHeal(EntityManager em, Entity healer, Entity target)
        {
            if (!em.Exists(healer) || !em.Exists(target)) return;

            // Verify target is friendly
            if (em.HasComponent<FactionTag>(healer) && em.HasComponent<FactionTag>(target))
            {
                var healerFaction = em.GetComponentData<FactionTag>(healer).Value;
                var targetFaction = em.GetComponentData<FactionTag>(target).Value;
                if (healerFaction != targetFaction) return; // Can't heal enemies
            }

            // Clear conflicting commands
            ClearAllCommands(em, healer);

            // Add HealCommand
            if (!em.HasComponent<HealCommand>(healer))
                em.AddComponentData(healer, new HealCommand { Target = target });
            else
                em.SetComponentData(healer, new HealCommand { Target = target });
        }

        // ==================== Helper Methods ====================

        /// <summary>
        /// Clears all command components from a unit.
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
            if (em.HasComponent<Target>(unit))
                em.SetComponentData(unit, new Target { Value = Entity.Null });
            if (em.HasComponent<UserMoveOrder>(unit))
                em.RemoveComponent<UserMoveOrder>(unit);
        }
    }
}
