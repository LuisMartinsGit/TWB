// AIMissionManager.cs
// Requests armies and intel, assigns missions (attack/defend)
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIScoutingManager))]
    public partial struct AIMissionManager : ISystem
    {
        private const float MISSION_UPDATE_INTERVAL = 4.0f;
        private const int MIN_ATTACK_STRENGTH = 30;
        private const int MIN_DEFENSE_STRENGTH = 20;

        private NativeHashMap<int, int> _nextMissionId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
            _nextMissionId = new NativeHashMap<int, int>(8, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_nextMissionId.IsCreated)
                _nextMissionId.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            double elapsedTime = SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process each AI player's mission management
            foreach (var (brain, missionState, sightings, sharedKnowledge, entity) 
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIMissionState>, 
                    DynamicBuffer<EnemySighting>, RefRO<AISharedKnowledge>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = missionState.ValueRW;

                // Periodic mission updates
                if (time >= state_val.LastMissionUpdate + state_val.MissionUpdateInterval)
                {
                    state_val.LastMissionUpdate = time;

                    // Evaluate strategic situation
                    EvaluateStrategicSituation(ref state, brain.ValueRO, sightings, 
                        sharedKnowledge.ValueRO);

                    // Create new missions based on intel
                    CreateMissions(ref state, brain.ValueRO, sightings, sharedKnowledge.ValueRO, ecb);

                    // Update existing missions
                    UpdateMissions(ref state, brain.ValueRO.Owner, ref state_val, ecb);

                    // Assign armies to missions
                    AssignArmiesToMissions(ref state, brain.ValueRO.Owner, ecb);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void EvaluateStrategicSituation(ref SystemState state, AIBrain brain,
            DynamicBuffer<EnemySighting> sightings, AISharedKnowledge knowledge)
        {
            // Analyze the current strategic situation
            // This influences what missions we create

            int enemyStrength = knowledge.EnemyEstimatedStrength;
            int ownStrength = knowledge.OwnMilitaryStrength;

            // Log strategic assessment (for debugging)
            if (sightings.Length > 0)
            {
                // We have intel on enemies
                float strengthRatio = ownStrength > 0 ? (float)enemyStrength / ownStrength : 1f;
                
                // Based on personality and strength ratio, we'll create appropriate missions
                // This is evaluated in CreateMissions
            }
        }

        private void CreateMissions(ref SystemState state, AIBrain brain,
            DynamicBuffer<EnemySighting> sightings, AISharedKnowledge knowledge, 
            EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            double currentTime = SystemAPI.Time.ElapsedTime;

            // Check available army strength
            int availableStrength = GetAvailableArmyStrength(ref state, brain.Owner);

            // Defensive missions - always have one for our base
            CreateDefenseMission(ref state, brain, availableStrength, ecb);

            // Offensive missions based on personality and situation
            if (brain.Personality == AIPersonality.Aggressive || brain.Personality == AIPersonality.Rush)
            {
                // Create attack missions if we have strength advantage
                if (availableStrength >= MIN_ATTACK_STRENGTH && sightings.Length > 0)
                {
                    CreateAttackMission(ref state, brain, sightings, availableStrength, ecb);
                }
            }
            else if (brain.Personality == AIPersonality.Balanced)
            {
                // Balanced approach: attack when we have clear advantage
                if (availableStrength >= MIN_ATTACK_STRENGTH * 1.5f && sightings.Length > 0)
                {
                    CreateAttackMission(ref state, brain, sightings, availableStrength, ecb);
                }
            }

            // Raid missions for economic targets
            if (brain.Personality != AIPersonality.Defensive)
            {
                CreateRaidMissions(ref state, brain, sightings, availableStrength, ecb);
            }

            // Expansion missions if economy is strong
            if (brain.Personality == AIPersonality.Economic)
            {
                if (knowledge.OwnEconomicStrength > 500)
                {
                    CreateExpansionMission(ref state, brain, ecb);
                }
            }
        }

        private void CreateDefenseMission(ref SystemState state, AIBrain brain,
            int availableStrength, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Check if defense mission already exists
            bool hasDefenseMission = false;
            foreach (var (mission, factionTag) in SystemAPI.Query<RefRO<AIMission>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == brain.Owner && 
                    mission.ValueRO.Type == MissionType.Defend &&
                    mission.ValueRO.Status == MissionStatus.Active)
                {
                    hasDefenseMission = true;
                    break;
                }
            }

            if (!hasDefenseMission)
            {
                // Get base position
                float3 basePos = GetBasePosition(ref state, brain.Owner);

                // Create defense mission
                int missionId = GetNextMissionId(brain.Owner);
                var missionEntity = ecb.CreateEntity();

                ecb.AddComponent(missionEntity, new AIMission
                {
                    MissionId = missionId,
                    Type = MissionType.Defend,
                    Status = MissionStatus.Active,
                    TargetPosition = basePos,
                    TargetFaction = brain.Owner, // Defending our own base
                    RequiredStrength = MIN_DEFENSE_STRENGTH,
                    AssignedStrength = 0,
                    CreatedTime = SystemAPI.Time.ElapsedTime,
                    LastUpdateTime = SystemAPI.Time.ElapsedTime,
                    Priority = 10 // Highest priority
                });

                ecb.AddComponent(missionEntity, new FactionTag { Value = brain.Owner });
                ecb.AddBuffer<AssignedArmy>(missionEntity);
            }
        }

        private void CreateAttackMission(ref SystemState state, AIBrain brain,
            DynamicBuffer<EnemySighting> sightings, int availableStrength, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Find best attack target (enemy base or large army)
            EnemySighting bestTarget = default;
            int highestPriority = 0;

            for (int i = 0; i < sightings.Length; i++)
            {
                var sighting = sightings[i];
                int priority = sighting.EstimatedStrength;
                
                if (sighting.IsBase == 1)
                    priority += 50; // Bases are high priority

                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    bestTarget = sighting;
                }
            }

            if (highestPriority == 0) return;

            // Check if attack mission already exists for this target
            bool missionExists = false;
            foreach (var (mission, factionTag) in SystemAPI.Query<RefRO<AIMission>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == brain.Owner && 
                    mission.ValueRO.Type == MissionType.Attack &&
                    mission.ValueRO.Status != MissionStatus.Completed)
                {
                    float dist = math.distance(mission.ValueRO.TargetPosition, bestTarget.Position);
                    if (dist < 20f) // Same target area
                    {
                        missionExists = true;
                        break;
                    }
                }
            }

            if (!missionExists)
            {
                int missionId = GetNextMissionId(brain.Owner);
                var missionEntity = ecb.CreateEntity();

                ecb.AddComponent(missionEntity, new AIMission
                {
                    MissionId = missionId,
                    Type = MissionType.Attack,
                    Status = MissionStatus.Pending,
                    TargetPosition = bestTarget.Position,
                    TargetFaction = bestTarget.EnemyFaction,
                    RequiredStrength = math.max(bestTarget.EstimatedStrength * 2, MIN_ATTACK_STRENGTH),
                    AssignedStrength = 0,
                    CreatedTime = SystemAPI.Time.ElapsedTime,
                    LastUpdateTime = SystemAPI.Time.ElapsedTime,
                    Priority = bestTarget.IsBase == 1 ? 8 : 6
                });

                ecb.AddComponent(missionEntity, new FactionTag { Value = brain.Owner });
                ecb.AddBuffer<AssignedArmy>(missionEntity);
            }
        }

        private void CreateRaidMissions(ref SystemState state, AIBrain brain,
            DynamicBuffer<EnemySighting> sightings, int availableStrength, EntityCommandBuffer ecb)
        {
            // Raid missions target enemy economy buildings
            for (int i = 0; i < sightings.Length; i++)
            {
                var sighting = sightings[i];
                if (sighting.IsBase == 0) continue; // Only interested in buildings
                if (sighting.EstimatedStrength > 30) continue; // Too strong

                // Check if raid already exists
                bool exists = false;
                foreach (var (mission, factionTag) in SystemAPI.Query<RefRO<AIMission>, RefRO<FactionTag>>())
                {
                    if (factionTag.ValueRO.Value == brain.Owner && 
                        mission.ValueRO.Type == MissionType.Raid)
                    {
                        float dist = math.distance(mission.ValueRO.TargetPosition, sighting.Position);
                        if (dist < 15f)
                        {
                            exists = true;
                            break;
                        }
                    }
                }

                if (!exists && availableStrength >= 10)
                {
                    int missionId = GetNextMissionId(brain.Owner);
                    var missionEntity = ecb.CreateEntity();

                    ecb.AddComponent(missionEntity, new AIMission
                    {
                        MissionId = missionId,
                        Type = MissionType.Raid,
                        Status = MissionStatus.Pending,
                        TargetPosition = sighting.Position,
                        TargetFaction = sighting.EnemyFaction,
                        RequiredStrength = 10,
                        AssignedStrength = 0,
                        CreatedTime = SystemAPI.Time.ElapsedTime,
                        LastUpdateTime = SystemAPI.Time.ElapsedTime,
                        Priority = 4
                    });

                    ecb.AddComponent(missionEntity, new FactionTag { Value = brain.Owner });
                    ecb.AddBuffer<AssignedArmy>(missionEntity);
                    
                    break; // Only one raid at a time
                }
            }
        }

        private void CreateExpansionMission(ref SystemState state, AIBrain brain, EntityCommandBuffer ecb)
        {
            // Check if expansion mission already exists
            bool exists = false;
            foreach (var (mission, factionTag) in SystemAPI.Query<RefRO<AIMission>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == brain.Owner && 
                    mission.ValueRO.Type == MissionType.Expand &&
                    mission.ValueRO.Status != MissionStatus.Completed)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                // Find good expansion location
                float3 basePos = GetBasePosition(ref state, brain.Owner);
                float3 expansionPos = basePos + new float3(60, 0, 60); // Away from base

                int missionId = GetNextMissionId(brain.Owner);
                var missionEntity = ecb.CreateEntity();

                ecb.AddComponent(missionEntity, new AIMission
                {
                    MissionId = missionId,
                    Type = MissionType.Expand,
                    Status = MissionStatus.Pending,
                    TargetPosition = expansionPos,
                    TargetFaction = brain.Owner,
                    RequiredStrength = 15, // Need some protection
                    AssignedStrength = 0,
                    CreatedTime = SystemAPI.Time.ElapsedTime,
                    LastUpdateTime = SystemAPI.Time.ElapsedTime,
                    Priority = 5
                });

                ecb.AddComponent(missionEntity, new FactionTag { Value = brain.Owner });
                ecb.AddBuffer<AssignedArmy>(missionEntity);
            }
        }

        private void UpdateMissions(ref SystemState state, Faction faction,
            ref AIMissionState missionState, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            int active = 0;
            int pending = 0;

            // Update all missions for this faction
            foreach (var (mission, missionFaction, entity) in 
                SystemAPI.Query<RefRW<AIMission>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (missionFaction.ValueRO.Value != faction) continue;

                var m = mission.ValueRW;
                m.LastUpdateTime = SystemAPI.Time.ElapsedTime;

                if (m.Status == MissionStatus.Active)
                    active++;
                else if (m.Status == MissionStatus.Pending)
                    pending++;

                // Check if mission should be completed or failed
                if (m.Status == MissionStatus.Active)
                {
                    // Check mission completion conditions
                    if (m.Type == MissionType.Attack || m.Type == MissionType.Raid)
                    {
                        // Check if target area is clear
                        bool targetClear = IsAreaClear(ref state, m.TargetPosition, m.TargetFaction);
                        if (targetClear)
                        {
                            m.Status = MissionStatus.Completed;
                        }
                    }

                    // Check if mission has been active too long without progress
                    if (SystemAPI.Time.ElapsedTime - m.CreatedTime > 120.0) // 2 minutes
                    {
                        m.Status = MissionStatus.Failed;
                    }
                }

                // Remove completed/failed missions
                if (m.Status == MissionStatus.Completed || m.Status == MissionStatus.Failed)
                {
                    // Release assigned armies
                    if (em.HasBuffer<AssignedArmy>(entity))
                    {
                        var armies = em.GetBuffer<AssignedArmy>(entity);
                        for (int i = 0; i < armies.Length; i++)
                        {
                            if (em.Exists(armies[i].ArmyEntity))
                            {
                                var army = em.GetComponentData<AIArmy>(armies[i].ArmyEntity);
                                army.MissionEntity = Entity.Null;
                                em.SetComponentData(armies[i].ArmyEntity, army);
                            }
                        }
                    }

                    ecb.DestroyEntity(entity);
                }
            }

            missionState.ActiveMissions = active;
            missionState.PendingMissions = pending;
        }

        private void AssignArmiesToMissions(ref SystemState state, Faction faction, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Get all pending/active missions sorted by priority
            var missions = new NativeList<(Entity entity, AIMission mission)>(Allocator.Temp);
            
            foreach (var (mission, missionFaction, entity) in 
                SystemAPI.Query<RefRO<AIMission>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (missionFaction.ValueRO.Value != faction) continue;
                if (mission.ValueRO.Status == MissionStatus.Pending || 
                    mission.ValueRO.Status == MissionStatus.Active)
                {
                    missions.Add((entity, mission.ValueRO));
                }
            }

            // Sort by priority
            missions.Sort();

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
                var (missionEntity, mission) = missions[i];
                
                // Check if mission needs more strength
                if (mission.AssignedStrength >= mission.RequiredStrength)
                    continue;

                int neededStrength = mission.RequiredStrength - mission.AssignedStrength;

                // Find armies to assign
                for (int j = availableArmies.Length - 1; j >= 0 && neededStrength > 0; j--)
                {
                    var (armyEntity, army) = availableArmies[j];

                    // Assign this army to mission
                    var updatedArmy = army;
                    updatedArmy.MissionEntity = missionEntity;
                    ecb.SetComponent(armyEntity, updatedArmy);

                    // Add to mission's assigned armies
                    if (em.HasBuffer<AssignedArmy>(missionEntity))
                    {
                        var assignedBuffer = em.GetBuffer<AssignedArmy>(missionEntity);
                        assignedBuffer.Add(new AssignedArmy
                        {
                            ArmyEntity = armyEntity,
                            Strength = army.TotalStrength
                        });
                    }

                    // Update mission strength
                    mission.AssignedStrength += army.TotalStrength;
                    neededStrength -= army.TotalStrength;

                    // Remove from available list
                    availableArmies.RemoveAtSwapBack(j);

                    // Update mission status
                    if (mission.Status == MissionStatus.Pending && 
                        mission.AssignedStrength >= mission.RequiredStrength)
                    {
                        mission.Status = MissionStatus.Active;
                    }

                    ecb.SetComponent(missionEntity, mission);
                }
            }

            missions.Dispose();
            availableArmies.Dispose();
        }

        private int GetAvailableArmyStrength(ref SystemState state, Faction faction)
        {
            int totalStrength = 0;

            foreach (var (army, armyFaction) in SystemAPI.Query<RefRO<AIArmy>, RefRO<FactionTag>>())
            {
                if (armyFaction.ValueRO.Value == faction && army.ValueRO.MissionEntity == Entity.Null)
                {
                    totalStrength += army.ValueRO.TotalStrength;
                }
            }

            return totalStrength;
        }

        private bool IsAreaClear(ref SystemState state, float3 position, Faction enemyFaction)
        {
            // Check if there are any enemy units/buildings near the target
            foreach (var (factionTag, transform) in SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>())
            {
                if (factionTag.ValueRO.Value != enemyFaction) continue;

                float dist = math.distance(transform.ValueRO.Position, position);
                if (dist < 15f)
                    return false; // Enemy still present
            }

            return true;
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

        private int GetNextMissionId(Faction faction)
        {
 int fKey = (int)faction;
 if (!_nextMissionId.ContainsKey(fKey))
     _nextMissionId[fKey] = 1;
 int id = _nextMissionId[fKey];
 _nextMissionId[fKey] = id + 1;
            return id;
        }
    }

    public struct SortableMission : System.IComparable<SortableMission>
    {
        public Entity Entity;
        public AIMission Mission;

        public int CompareTo(SortableMission other)
            => other.Mission.Priority.CompareTo(Mission.Priority); // descending
    }

}