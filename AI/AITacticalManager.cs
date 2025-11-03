// AITacticalManager.cs
// Manages armies to fulfill missions, issues move/attack orders, prioritizes targets
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIMissionManager))]
    public partial struct AITacticalManager : ISystem
    {
        private const float TACTICAL_UPDATE_INTERVAL = 1.0f;
        private const float ENGAGEMENT_RANGE = 25f;
        private const float FORMATION_SPACING = 3f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process each AI player's tactical operations
            foreach (var (brain, tacticalState, entity) 
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AITacticalState>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = tacticalState.ValueRW;

                // Periodic tactical updates
                if (time >= state_val.LastTacticalUpdate + state_val.TacticalUpdateInterval)
                {
                    state_val.LastTacticalUpdate = time;

                    // Manage all armies for this faction
                    ManageArmies(ref state, brain.ValueRO.Owner, ref state_val, ecb);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ManageArmies(ref SystemState state, Faction faction,
            ref AITacticalState tacticalState, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            int managedCount = 0;

            // Process each army
            foreach (var (army, armyFaction, armyUnits, entity) in 
                SystemAPI.Query<RefRW<AIArmy>, RefRO<FactionTag>, DynamicBuffer<ArmyUnit>>()
                .WithEntityAccess())
            {
                if (armyFaction.ValueRO.Value != faction) continue;

                managedCount++;

                // Update army position and strength
                UpdateArmyStats(ref state, ref army.ValueRW, armyUnits);

                // Execute mission if assigned
                if (army.ValueRO.MissionEntity != Entity.Null && em.Exists(army.ValueRO.MissionEntity))
                {
                    var mission = em.GetComponentData<AIMission>(army.ValueRO.MissionEntity);
                    ExecuteMission(ref state, army.ValueRO, mission, armyUnits, ecb);
                }
                else
                {
                    // No mission - keep army near base
                    float3 basePos = GetBasePosition(ref state, faction);
                    MoveArmyTo(ref state, armyUnits, basePos, ecb);
                }
            }

            tacticalState.ManagedArmies = managedCount;
        }

        private void UpdateArmyStats(ref SystemState state, ref AIArmy army,
            DynamicBuffer<ArmyUnit> armyUnits)
        {
            var em = state.EntityManager;

            // Calculate average position and total strength
            float3 avgPos = float3.zero;
            int totalStrength = 0;
            int validUnits = 0;

            for (int i = armyUnits.Length - 1; i >= 0; i--)
            {
                var armyUnit = armyUnits[i];
                
                // Check if unit still exists
                if (!em.Exists(armyUnit.Unit))
                {
                    armyUnits.RemoveAt(i);
                    continue;
                }

                // Get unit position
                if (em.HasComponent<LocalTransform>(armyUnit.Unit))
                {
                    var transform = em.GetComponentData<LocalTransform>(armyUnit.Unit);
                    avgPos += transform.Position;
                    validUnits++;
                }

                // Update unit strength
                if (em.HasComponent<Health>(armyUnit.Unit))
                {
                    var health = em.GetComponentData<Health>(armyUnit.Unit);
                    armyUnit.Strength = health.Value / 10;
                    armyUnits[i] = armyUnit;
                }

                totalStrength += armyUnit.Strength;
            }

            if (validUnits > 0)
            {
                army.Position = avgPos / validUnits;
                army.TotalStrength = totalStrength;
            }
        }

        private void ExecuteMission(ref SystemState state, AIArmy army, AIMission mission,
            DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
        {
            switch (mission.Type)
            {
                case MissionType.Attack:
                    ExecuteAttackMission(ref state, army, mission, armyUnits, ecb);
                    break;
                case MissionType.Defend:
                    ExecuteDefendMission(ref state, army, mission, armyUnits, ecb);
                    break;
                case MissionType.Raid:
                    ExecuteRaidMission(ref state, army, mission, armyUnits, ecb);
                    break;
                case MissionType.Expand:
                    ExecuteExpansionMission(ref state, army, mission, armyUnits, ecb);
                    break;
            }
        }

        private void ExecuteAttackMission(ref SystemState state, AIArmy army, AIMission mission,
            DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Check if we're near the target
            float distToTarget = math.distance(army.Position, mission.TargetPosition);

            if (distToTarget > ENGAGEMENT_RANGE)
            {
                // Move towards target
                MoveArmyTo(ref state, armyUnits, mission.TargetPosition, ecb);
            }
            else
            {
                // We're in range - engage enemies
                EngageEnemies(ref state, army, mission.TargetFaction, armyUnits, ecb);
            }
        }

        private void ExecuteDefendMission(ref SystemState state, AIArmy army, AIMission mission,
            DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Stay near defense point
            float distToDefensePoint = math.distance(army.Position, mission.TargetPosition);

            if (distToDefensePoint > 15f)
            {
                // Return to defense position
                MoveArmyTo(ref state, armyUnits, mission.TargetPosition, ecb);
            }
            else
            {
                // Look for nearby enemies
                Entity nearestEnemy = FindNearestEnemy(ref state, army.Position, army.Owner, 30f);
                
                if (nearestEnemy != Entity.Null)
                {
                    // Engage nearby threat
                    EngageEnemies(ref state, army, GetEnemyFaction(ref state, nearestEnemy), 
                        armyUnits, ecb);
                }
                else
                {
                    // Hold position in formation
                    HoldFormation(ref state, armyUnits, mission.TargetPosition, ecb);
                }
            }
        }

        private void ExecuteRaidMission(ref SystemState state, AIArmy army, AIMission mission,
            DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
        {
            // Raid missions: fast strike on target, then retreat
            float distToTarget = math.distance(army.Position, mission.TargetPosition);

            if (distToTarget > 10f)
            {
                // Approach target quickly
                MoveArmyTo(ref state, armyUnits, mission.TargetPosition, ecb);
            }
            else
            {
                // Strike target
                EngageEnemies(ref state, army, mission.TargetFaction, armyUnits, ecb);

                // If we've taken casualties, consider retreating
                if (army.TotalStrength < mission.RequiredStrength * 0.5f)
                {
                    // Retreat to base
                    float3 basePos = GetBasePosition(ref state, army.Owner);
                    MoveArmyTo(ref state, armyUnits, basePos, ecb);
                }
            }
        }

        private void ExecuteExpansionMission(ref SystemState state, AIArmy army, AIMission mission,
            DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
        {
            // Move to expansion point and hold position
            float distToTarget = math.distance(army.Position, mission.TargetPosition);

            if (distToTarget > 10f)
            {
                MoveArmyTo(ref state, armyUnits, mission.TargetPosition, ecb);
            }
            else
            {
                // Secure the area
                HoldFormation(ref state, armyUnits, mission.TargetPosition, ecb);

                // Look for threats
                Entity nearestEnemy = FindNearestEnemy(ref state, army.Position, army.Owner, 25f);
                if (nearestEnemy != Entity.Null)
                {
                    EngageEnemies(ref state, army, GetEnemyFaction(ref state, nearestEnemy),
                        armyUnits, ecb);
                }
            }
        }

        private void MoveArmyTo(ref SystemState state, DynamicBuffer<ArmyUnit> armyUnits,
            float3 destination, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Calculate formation positions
            int unitsPerRow = (int)math.sqrt(armyUnits.Length);
            int row = 0, col = 0;

            for (int i = 0; i < armyUnits.Length; i++)
            {
                if (!em.Exists(armyUnits[i].Unit)) continue;

                // Calculate offset from center for formation
                float3 formationOffset = new float3(
                    (col - unitsPerRow / 2f) * FORMATION_SPACING,
                    0,
                    (row - unitsPerRow / 2f) * FORMATION_SPACING
                );

                float3 unitDestination = destination + formationOffset;

                // Set unit destination
                ecb.SetComponent(armyUnits[i].Unit, new DesiredDestination
                {
                    Position = unitDestination,
                    Has = 1
                });

                // Clear any current target
                if (em.HasComponent<Target>(armyUnits[i].Unit))
                {
                    ecb.SetComponent(armyUnits[i].Unit, new Target { Value = Entity.Null });
                }

                col++;
                if (col >= unitsPerRow)
                {
                    col = 0;
                    row++;
                }
            }
        }

        private void HoldFormation(ref SystemState state, DynamicBuffer<ArmyUnit> armyUnits,
            float3 centerPosition, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            int unitsPerRow = (int)math.sqrt(armyUnits.Length);
            int row = 0, col = 0;

            for (int i = 0; i < armyUnits.Length; i++)
            {
                if (!em.Exists(armyUnits[i].Unit)) continue;

                // Calculate formation position
                float3 formationPos = centerPosition + new float3(
                    (col - unitsPerRow / 2f) * FORMATION_SPACING,
                    0,
                    (row - unitsPerRow / 2f) * FORMATION_SPACING
                );

                var transform = em.GetComponentData<LocalTransform>(armyUnits[i].Unit);
                float distToFormationPos = math.distance(transform.Position, formationPos);

                // Only move if significantly out of position
                if (distToFormationPos > FORMATION_SPACING * 0.5f)
                {
                    ecb.SetComponent(armyUnits[i].Unit, new DesiredDestination
                    {
                        Position = formationPos,
                        Has = 1
                    });
                }

                col++;
                if (col >= unitsPerRow)
                {
                    col = 0;
                    row++;
                }
            }
        }

        private void EngageEnemies(ref SystemState state, AIArmy army, Faction enemyFaction,
            DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Find and prioritize enemy targets
            var targets = FindEnemyTargets(ref state, army.Position, enemyFaction, ENGAGEMENT_RANGE);

            if (targets.Length == 0)
            {
                targets.Dispose();
                return;
            }

            // Prioritize targets
            PrioritizeTargets(ref state, targets);

            // Assign targets to units
            int targetIdx = 0;
            for (int i = 0; i < armyUnits.Length; i++)
            {
                if (!em.Exists(armyUnits[i].Unit)) continue;

                if (targetIdx < targets.Length)
                {
                    // Assign target
                    ecb.SetComponent(armyUnits[i].Unit, new Target 
                    { 
                        Value = targets[targetIdx].Entity 
                    });

                    // Clear movement order (attack takes priority)
                    ecb.SetComponent(armyUnits[i].Unit, new DesiredDestination 
                    { 
                        Position = float3.zero, 
                        Has = 0 
                    });

                    // Assign multiple units to high-priority targets
                    if (targets[targetIdx].Priority < 8)
                        targetIdx++;
                }
            }

            targets.Dispose();
        }

        private NativeList<(Entity Entity, float3 Position, int Priority)> FindEnemyTargets(
            ref SystemState state, float3 position, Faction enemyFaction, float range)
        {
            var em = state.EntityManager;
            var targets = new NativeList<(Entity, float3, int)>(Allocator.Temp);

            // Find enemy units
            foreach (var (factionTag, transform, entity) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != enemyFaction) continue;

                float dist = math.distance(position, transform.ValueRO.Position);
                if (dist <= range)
                {
                    // Base priority
                    int priority = 5;

                    // Increase priority for buildings
                    if (em.HasComponent<BuildingTag>(entity))
                        priority += 3;

                    // Increase priority for low health targets
                    if (em.HasComponent<Health>(entity))
                    {
                        var health = em.GetComponentData<Health>(entity);
                        if (health.Value < health.Max * 0.3f)
                            priority += 2;
                    }

                    // Increase priority for ranged units (threats)
                    if (em.HasComponent<UnitTag>(entity))
                    {
                        var unitTag = em.GetComponentData<UnitTag>(entity);
                        if (unitTag.Class == UnitClass.Ranged || unitTag.Class == UnitClass.Siege)
                            priority += 2;
                    }

                    targets.Add((entity, transform.ValueRO.Position, priority));
                }
            }

            return targets;
        }

        private void PrioritizeTargets(ref SystemState state,
            NativeList<(Entity Entity, float3 Position, int Priority)> targets)
        {
            // Sort targets by priority (higher priority first)
            for (int i = 0; i < targets.Length - 1; i++)
            {
                for (int j = i + 1; j < targets.Length; j++)
                {
                    if (targets[j].Priority > targets[i].Priority)
                    {
                        var temp = targets[i];
                        targets[i] = targets[j];
                        targets[j] = temp;
                    }
                }
            }
        }

        private Entity FindNearestEnemy(ref SystemState state, float3 position, 
            Faction ownFaction, float maxRange)
        {
            var em = state.EntityManager;
            Entity nearest = Entity.Null;
            float nearestDist = maxRange;

            foreach (var (factionTag, transform, entity) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == ownFaction) continue;

                float dist = math.distance(position, transform.ValueRO.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = entity;
                }
            }

            return nearest;
        }

        private Faction GetEnemyFaction(ref SystemState state, Entity enemy)
        {
            var em = state.EntityManager;
            if (em.HasComponent<FactionTag>(enemy))
            {
                return em.GetComponentData<FactionTag>(enemy).Value;
            }
            return Faction.Red; // Default
        }

        private float3 GetBasePosition(ref SystemState state, Faction faction)
        {
            foreach (var (factionTag, transform, buildingTag) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>())
            {
                if (factionTag.ValueRO.Value == faction && buildingTag.ValueRO.IsBase == 1)
                {
                    return transform.ValueRO.Position;
                }
            }

            return float3.zero;
        }
    }
}