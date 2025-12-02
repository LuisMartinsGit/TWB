// AITacticalManager.cs
// Manages armies to fulfill missions, assigns armies to missions, executes attack/defend orders
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Core;

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

                    // First, assign armies to missions
                    AssignArmiesToMissions(ref state, brain.ValueRO.Owner, ecb);

                    // Then, manage all armies and execute their missions
                    ManageArmies(ref state, brain.ValueRO.Owner, ref state_val, ecb);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void AssignArmiesToMissions(ref SystemState state, Faction faction, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Get all pending missions sorted by priority
            var missions = new NativeList<(Entity entity, AIMission mission)>(Allocator.Temp);

            foreach (var (mission, missionFaction, entity) in
                SystemAPI.Query<RefRO<AIMission>, RefRO<FactionTag>>()
                    .WithEntityAccess())
            {
                if (missionFaction.ValueRO.Value != faction) continue;
                if (mission.ValueRO.Status == MissionStatus.Pending)
                {
                    missions.Add((entity, mission.ValueRO));
                }
            }

            if (missions.Length == 0)
            {
                missions.Dispose();
                return;
            }

            // Sort by priority (descending)
            for (int i = 0; i < missions.Length - 1; i++)
            {
                for (int j = i + 1; j < missions.Length; j++)
                {
                    if (missions[j].mission.Priority > missions[i].mission.Priority)
                    {
                        var temp = missions[i];
                        missions[i] = missions[j];
                        missions[j] = temp;
                    }
                }
            }

            // Find available armies
            var availableArmies = new NativeList<(Entity entity, AIArmy army)>(Allocator.Temp);

            foreach (var (army, armyFaction, entity) in
                SystemAPI.Query<RefRO<AIArmy>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (armyFaction.ValueRO.Value != faction) continue;
                if (army.ValueRO.MissionEntity == Entity.Null) // Not assigned
                {
                    availableArmies.Add((entity, army.ValueRO));
                }
            }

            // Assign armies to missions
            for (int i = 0; i < missions.Length; i++)
            {
                var missionTuple = missions[i];
                var mission = missionTuple.mission;

                if (availableArmies.Length == 0) break;

                // Find best army for this mission
                int bestArmyIdx = -1;
                int bestStrengthMatch = int.MaxValue;

                for (int j = 0; j < availableArmies.Length; j++)
                {
                    var armyStrength = availableArmies[j].army.TotalStrength;
                    
                    // Prefer armies that match the required strength
                    int strengthDiff = math.abs(armyStrength - mission.RequiredStrength);
                    
                    if (armyStrength >= mission.RequiredStrength && strengthDiff < bestStrengthMatch)
                    {
                        bestStrengthMatch = strengthDiff;
                        bestArmyIdx = j;
                    }
                }

                // Assign army to mission if suitable one found
                if (bestArmyIdx >= 0)
                {
                    var armyTuple = availableArmies[bestArmyIdx];

                    // Update army's mission assignment
                    var updatedArmy = armyTuple.army;
                    updatedArmy.MissionEntity = missionTuple.entity;
                    ecb.SetComponent(armyTuple.entity, updatedArmy);

                    // Update mission status
                    var updatedMission = mission;
                    updatedMission.Status = MissionStatus.Active;
                    updatedMission.AssignedStrength = armyTuple.army.TotalStrength;
                    ecb.SetComponent(missionTuple.entity, updatedMission);

                    UnityEngine.Debug.Log($"[AI Tactical] {faction} - Assigned army (strength {armyTuple.army.TotalStrength}) " +
                        $"to {mission.Type} mission at ({mission.TargetPosition.x:F0}, {mission.TargetPosition.z:F0})");

                    // Remove assigned army from available list
                    availableArmies.RemoveAtSwapBack(bestArmyIdx);
                }
            }

            missions.Dispose();
            availableArmies.Dispose();
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
                    if (!basePos.Equals(float3.zero))
                    {
                        float distToBase = math.distance(army.ValueRO.Position, basePos);
                        if (distToBase > 30f)
                        {
                            MoveArmyTo(ref state, armyUnits, basePos, ecb);
                        }
                    }
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
                if (em.HasComponent<Damage>(armyUnit.Unit))
                {
                    var damage = em.GetComponentData<Damage>(armyUnit.Unit);
                    armyUnit.Strength = damage.Value;
                    armyUnits[i] = armyUnit;
                }

                totalStrength += armyUnit.Strength;
            }

            if (validUnits > 0)
            {
                army.Position = avgPos / validUnits;
                army.TotalStrength = totalStrength;
                army.UnitCount = validUnits;
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

            float distToTarget = math.distance(army.Position, mission.TargetPosition);

            // Check if any unit in the army is currently engaging
            bool isEngaging = false;
            for (int i = 0; i < armyUnits.Length; i++)
            {
                if (!em.Exists(armyUnits[i].Unit)) continue;

                if (em.HasComponent<Target>(armyUnits[i].Unit))
                {
                    var target = em.GetComponentData<Target>(armyUnits[i].Unit);
                    if (target.Value != Entity.Null)
                    {
                        isEngaging = true;
                        break;
                    }
                }
            }

            // Use hysteresis to prevent oscillation
            const float ENTER_ENGAGEMENT_RANGE = ENGAGEMENT_RANGE - 5f;
            const float EXIT_ENGAGEMENT_RANGE = ENGAGEMENT_RANGE + 5f;
            float effectiveRange = isEngaging ? EXIT_ENGAGEMENT_RANGE : ENTER_ENGAGEMENT_RANGE;

            if (distToTarget > effectiveRange)
            {
                // Move towards target (only if not already engaging)
                if (!isEngaging)
                {
                    MoveArmyTo(ref state, armyUnits, mission.TargetPosition, ecb);
                }
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

            const float DEFEND_RETURN_DISTANCE = 15f;
            const float DEFEND_HOLD_DISTANCE = 12f;

            bool isHolding = distToDefensePoint <= DEFEND_HOLD_DISTANCE;

            if (distToDefensePoint > DEFEND_RETURN_DISTANCE)
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
                else if (!isHolding)
                {
                    // Hold position in formation
                    HoldFormation(ref state, armyUnits, mission.TargetPosition, ecb);
                }
            }
        }

        private void ExecuteRaidMission(ref SystemState state, AIArmy army,
            AIMission mission, DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
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
            
            // Use the AICommandAdapter for proper multiplayer sync
            AICommandAdapter.MoveArmy(em, armyUnits, destination, FORMATION_SPACING);
        }


  private void EngageEnemies(ref SystemState state, AIArmy army, Faction enemyFaction,
    DynamicBuffer<ArmyUnit> armyUnits, EntityCommandBuffer ecb)
{
    var em = state.EntityManager;
    var targets = FindEnemyTargets(ref state, army.Position, enemyFaction, ENGAGEMENT_RANGE);

    if (targets.Length == 0)
    {
        targets.Dispose();
        return;
    }

    PrioritizeTargets(ref state, targets);

    // Use AICommandAdapter for proper attack command routing
    int targetIdx = 0;
    for (int i = 0; i < armyUnits.Length; i++)
    {
        if (!em.Exists(armyUnits[i].Unit)) continue;

        if (targetIdx < targets.Length)
        {
            bool needsNewTarget = true;
            
            if (em.HasComponent<Target>(armyUnits[i].Unit))
            {
                var currentTarget = em.GetComponentData<Target>(armyUnits[i].Unit);
                if (currentTarget.Value != Entity.Null && em.Exists(currentTarget.Value))
                {
                    needsNewTarget = false;
                }
            }

            if (needsNewTarget)
            {
                // Route through command system for multiplayer sync
                AICommandAdapter.IssueAttack(em, armyUnits[i].Unit, targets[targetIdx].Entity);
            }

            targetIdx = (targetIdx + 1) % targets.Length;
        }
    }

    targets.Dispose();
}
private void HoldFormation(ref SystemState state, DynamicBuffer<ArmyUnit> armyUnits,
    float3 position, EntityCommandBuffer ecb)
{
    var em = state.EntityManager;
    
    // Use AICommandAdapter for proper multiplayer sync
    AICommandAdapter.HoldFormation(em, armyUnits, position, FORMATION_SPACING, FORMATION_SPACING * 0.5f);
}
        private NativeList<(Entity Entity, float3 Position, int Priority)> FindEnemyTargets(
            ref SystemState state, float3 position, Faction enemyFaction, float range)
        {
            var em = state.EntityManager;
            var targets = new NativeList<(Entity, float3, int)>(Allocator.Temp);

            // Find enemy buildings (high priority)
            foreach (var (buildingTag, factionTag, transform, entity) in
                SystemAPI.Query<RefRO<BuildingTag>, RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != enemyFaction) continue;

                float3 targetPos = transform.ValueRO.Position;
                float dist = math.distance(position, targetPos);

                if (dist <= range)
                {
                    int priority = buildingTag.ValueRO.IsBase == 1 ? 100 : 80;
                    targets.Add((entity, targetPos, priority));
                }
            }

            // Find enemy units (medium priority)
            foreach (var (unitTag, factionTag, transform, entity) in
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != enemyFaction) continue;

                float3 targetPos = transform.ValueRO.Position;
                float dist = math.distance(position, targetPos);

                if (dist <= range)
                {
                    int priority = 50;
                    targets.Add((entity, targetPos, priority));
                }
            }

            return targets;
        }

        private void PrioritizeTargets(ref SystemState state,
            NativeList<(Entity Entity, float3 Position, int Priority)> targets)
        {
            // Simple bubble sort by priority (descending)
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