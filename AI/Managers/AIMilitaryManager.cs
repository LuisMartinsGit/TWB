// AIMilitaryManager.cs
// Requests resources and barracks, trains units, creates armies and scouts
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIBuildingManager))]
    public partial struct AIMilitaryManager : ISystem
    {
        private const float RECRUITMENT_CHECK_INTERVAL = 5.0f;
        private const int MIN_ARMY_SIZE = 6;
        private const int MAX_ARMY_SIZE = 12;
        private const int TARGET_BARRACKS = 2;

        private NativeHashMap<int, int> _nextArmyId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
            _nextArmyId = new NativeHashMap<int, int>(8, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_nextArmyId.IsCreated)
                _nextArmyId.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (brain, militaryState, recruitReqs, resourceReqs, entity)
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIMilitaryState>,
                    DynamicBuffer<RecruitmentRequest>, DynamicBuffer<ResourceRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = militaryState.ValueRW;

                if (time >= state_val.LastRecruitmentCheck + state_val.RecruitmentCheckInterval)
                {
                    state_val.LastRecruitmentCheck = time;
                    ManageMilitary(ref state, brain.ValueRO.Owner, ref state_val, recruitReqs, resourceReqs, ecb);
                }

                ProcessRecruitmentRequests(ref state, brain.ValueRO.Owner, recruitReqs, ecb);
                OrganizeArmies(ref state, brain.ValueRO.Owner, ref state_val, ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ManageMilitary(ref SystemState state, Faction faction,
            ref AIMilitaryState militaryState, DynamicBuffer<RecruitmentRequest> recruitReqs,
            DynamicBuffer<ResourceRequest> resourceReqs, EntityCommandBuffer ecb)
        {
            CountMilitaryUnits(ref state, faction, ref militaryState);
            CountQueuedMilitary(ref state, faction, ref militaryState);

            int barracksCount = 0;
            foreach (var (barracksTag, factionTag) in SystemAPI.Query<RefRO<BarracksTag>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == faction)
                    barracksCount++;
            }
            militaryState.ActiveBarracks = barracksCount;

            if (barracksCount < TARGET_BARRACKS)
            {
                RequestBarracks(ref state, faction);
            }

            DetermineMilitaryNeeds(ref state, faction, ref militaryState, recruitReqs);
            RequestMilitaryResources(ref state, faction, militaryState, resourceReqs);
        }

        private void CountMilitaryUnits(ref SystemState state, Faction faction, ref AIMilitaryState militaryState)
        {
            militaryState.TotalSoldiers = 0;
            militaryState.TotalArchers = 0;
            militaryState.TotalSiegeUnits = 0;

            foreach (var (unitTag, factionTag) in SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                switch (unitTag.ValueRO.Class)
                {
                    case UnitClass.Melee:
                        militaryState.TotalSoldiers++;
                        break;
                    case UnitClass.Ranged:
                        militaryState.TotalArchers++;
                        break;
                    case UnitClass.Siege:
                        militaryState.TotalSiegeUnits++;
                        break;
                }
            }
        }

        private void CountQueuedMilitary(ref SystemState state, Faction faction, ref AIMilitaryState militaryState)
        {
            int queuedSoldiers = 0;
            int queuedArchers = 0;
            int queuedSiege = 0;

            foreach (var (factionTag, trainQueue) in SystemAPI.Query<RefRO<FactionTag>, DynamicBuffer<TrainQueueItem>>())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                for (int i = 0; i < trainQueue.Length; i++)
                {
                    var item = trainQueue[i];
                    if (item.UnitId.Equals("Swordsman"))
                        queuedSoldiers++;
                    else if (item.UnitId.Equals("Archer"))
                        queuedArchers++;
                    else if (item.UnitId.Equals("Catapult"))
                        queuedSiege++;
                }
            }

            militaryState.QueuedSoldiers = queuedSoldiers;
            militaryState.QueuedArchers = queuedArchers;
            militaryState.QueuedSiegeUnits = queuedSiege;
        }

        private void DetermineMilitaryNeeds(ref SystemState state, Faction faction,
            ref AIMilitaryState militaryState, DynamicBuffer<RecruitmentRequest> recruitReqs)
        {
            // Get AI personality for composition preferences
            AIPersonality personality = AIPersonality.Balanced;
            foreach (var brain in SystemAPI.Query<RefRO<AIBrain>>())
            {
                if (brain.ValueRO.Owner == faction)
                {
                    personality = brain.ValueRO.Personality;
                    break;
                }
            }

            int totalMilitary = militaryState.TotalSoldiers + militaryState.TotalArchers + militaryState.TotalSiegeUnits;
            int targetSize = MIN_ARMY_SIZE * 2;

            if (totalMilitary < targetSize)
            {
                int needed = targetSize - totalMilitary;

                // Determine composition based on personality
                int soldiers, archers, siege;
                switch (personality)
                {
                    case AIPersonality.Aggressive:
                    case AIPersonality.Rush:
                        soldiers = (int)(needed * 0.6f);
                        archers = (int)(needed * 0.3f);
                        siege = needed - soldiers - archers;
                        break;
                    case AIPersonality.Defensive:
                        soldiers = (int)(needed * 0.3f);
                        archers = (int)(needed * 0.5f);
                        siege = needed - soldiers - archers;
                        break;
                    default: // Balanced, Economic
                        soldiers = (int)(needed * 0.4f);
                        archers = (int)(needed * 0.4f);
                        siege = needed - soldiers - archers;
                        break;
                }

                if (soldiers > 0)
                {
                    recruitReqs.Add(new RecruitmentRequest
                    {
                        UnitType = UnitClass.Melee,
                        Quantity = soldiers,
                        Priority = 5,
                        RequestingManager = Entity.Null
                    });
                }

                if (archers > 0)
                {
                    recruitReqs.Add(new RecruitmentRequest
                    {
                        UnitType = UnitClass.Ranged,
                        Quantity = archers,
                        Priority = 5,
                        RequestingManager = Entity.Null
                    });
                }
            }
        }

        private void ProcessRecruitmentRequests(ref SystemState state, Faction faction,
            DynamicBuffer<RecruitmentRequest> recruitReqs, EntityCommandBuffer ecb)
        {
            if (recruitReqs.Length == 0) return;

            var em = state.EntityManager;

            var availableBarracks = new NativeList<Entity>(Allocator.Temp);
            var availableHalls = new NativeList<Entity>(Allocator.Temp);

            foreach (var (barracksTag, trainingState, factionTag, entity) in
                SystemAPI.Query<RefRO<BarracksTag>, RefRO<TrainingState>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction && trainingState.ValueRO.Busy == 0)
                    availableBarracks.Add(entity);
            }

            foreach (var (buildingTag, trainingState, factionTag, entity) in
                SystemAPI.Query<RefRO<BuildingTag>, RefRO<TrainingState>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction &&
                    buildingTag.ValueRO.IsBase == 1 &&
                    trainingState.ValueRO.Busy == 0)
                    availableHalls.Add(entity);
            }

            var sortedReqs = new NativeList<RecruitmentRequest>(Allocator.Temp);
            for (int i = 0; i < recruitReqs.Length; i++)
                sortedReqs.Add(recruitReqs[i]);
            sortedReqs.Sort(new RecruitmentRequestComparer());

            for (int i = 0; i < sortedReqs.Length; i++)
            {
                var req = sortedReqs[i];
                if (req.Quantity <= 0) continue;

                bool useHall = req.UnitType == UnitClass.Economy || req.UnitType == UnitClass.Miner;
                var availableBuildings = useHall ? availableHalls : availableBarracks;

                if (availableBuildings.Length == 0) continue;

                Entity building = availableBuildings[0];
                availableBuildings.RemoveAt(0);

                // Queue training
                if (em.HasBuffer<TrainQueueItem>(building))
                {
                    var queue = em.GetBuffer<TrainQueueItem>(building);
                    string unitId = GetUnitIdForClass(req.UnitType);

                    for (int j = 0; j < req.Quantity && j < 3; j++)
                    {
                        queue.Add(new TrainQueueItem { UnitId = unitId });
                    }
                }
            }

            // Clear processed requests
            recruitReqs.Clear();

            availableBarracks.Dispose();
            availableHalls.Dispose();
            sortedReqs.Dispose();
        }

        private string GetUnitIdForClass(UnitClass unitClass)
        {
            return unitClass switch
            {
                UnitClass.Melee => "Swordsman",
                UnitClass.Ranged => "Archer",
                UnitClass.Siege => "Catapult",
                UnitClass.Economy => "Builder",
                UnitClass.Miner => "Miner",
                UnitClass.Scout => "Scout",
                _ => "Swordsman"
            };
        }

        private void OrganizeArmies(ref SystemState state, Faction faction,
            ref AIMilitaryState militaryState, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Count existing armies
            int armyCount = 0;
            foreach (var army in SystemAPI.Query<RefRO<AIArmy>>())
            {
                if (army.ValueRO.Owner == faction)
                    armyCount++;
            }

            militaryState.ArmiesCount = armyCount;

            // Find unassigned military units
            var unassigned = new NativeList<Entity>(Allocator.Temp);

            foreach (var (unitTag, factionTag, entity) in
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>>()
                .WithNone<ArmyTag>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                if (unitTag.ValueRO.Class == UnitClass.Melee ||
                    unitTag.ValueRO.Class == UnitClass.Ranged ||
                    unitTag.ValueRO.Class == UnitClass.Siege)
                {
                    unassigned.Add(entity);
                }
            }

            // Create new army if enough unassigned units
            if (unassigned.Length >= MIN_ARMY_SIZE)
            {
                int armyId = GetNextArmyId(faction);

                var armyEntity = ecb.CreateEntity();
                ecb.AddComponent(armyEntity, new AIArmy
                {
                    ArmyId = armyId,
                    Owner = faction,
                    MissionEntity = Entity.Null,
                    Position = float3.zero,
                    TotalStrength = 0,
                    IsEngaging = 0,
                    IsRetreating = 0
                });
                ecb.AddComponent(armyEntity, new FactionTag { Value = faction });
                var armyUnits = ecb.AddBuffer<ArmyUnit>(armyEntity);

                int assigned = 0;
                for (int i = 0; i < unassigned.Length && assigned < MAX_ARMY_SIZE; i++)
                {
                    Entity unit = unassigned[i];

                    int strength = 1;
                    if (em.HasComponent<Damage>(unit))
                        strength = em.GetComponentData<Damage>(unit).Value;

                    armyUnits.Add(new ArmyUnit { Unit = unit, Strength = strength });

                    ecb.AddComponent(unit, new ArmyTag { ArmyId = armyId, ArmyEntity = armyEntity });
                    assigned++;
                }

                Debug.Log($"[AIMilitaryManager] {faction} created army {armyId} with {assigned} units");
            }

            unassigned.Dispose();
        }

        private void RequestBarracks(ref SystemState state, Faction faction)
        {
            foreach (var (brain, buildReqs, entity) in
                SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<BuildRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                bool exists = false;
                for (int i = 0; i < buildReqs.Length; i++)
                {
                    if (buildReqs[i].BuildingType.Equals("Barracks"))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    float3 location = FindBarracksLocation(ref state, faction);

                    buildReqs.Add(new BuildRequest
                    {
                        BuildingType = "Barracks",
                        DesiredPosition = location,
                        Priority = 6,
                        Assigned = 0,
                        AssignedBuilder = Entity.Null
                    });
                }

                break;
            }
        }

        private float3 FindBarracksLocation(ref SystemState state, Faction faction)
        {
            foreach (var (factionTag, transform, buildingTag) in
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>())
            {
                if (factionTag.ValueRO.Value == faction && buildingTag.ValueRO.IsBase == 1)
                {
                    return transform.ValueRO.Position + new float3(10, 0, -5);
                }
            }

            return new float3(15, 0, 15);
        }

        private void RequestMilitaryResources(ref SystemState state, Faction faction,
            AIMilitaryState militaryState, DynamicBuffer<ResourceRequest> resourceReqs)
        {
            int neededSupplies = militaryState.TotalSoldiers * 20;
            int neededIron = militaryState.TotalArchers * 15;

            if (neededSupplies > 0 || neededIron > 0)
            {
                resourceReqs.Add(new ResourceRequest
                {
                    Supplies = neededSupplies,
                    Iron = neededIron,
                    Crystal = 0,
                    Veilsteel = 0,
                    Glow = 0,
                    Priority = 5,
                    Requester = Entity.Null,
                    Approved = 0
                });
            }
        }

        private int GetNextArmyId(Faction faction)
        {
            int factionInt = (int)faction;
            if (!_nextArmyId.TryGetValue(factionInt, out int id))
            {
                id = 0;
            }
            _nextArmyId[factionInt] = id + 1;
            return id;
        }
    }

    struct RecruitmentRequestComparer : IComparer<RecruitmentRequest>
    {
        public int Compare(RecruitmentRequest a, RecruitmentRequest b)
        {
            return b.Priority.CompareTo(a.Priority);
        }
    }
}