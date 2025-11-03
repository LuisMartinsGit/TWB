// AIMilitaryManager.cs
// Requests resources and barracks, trains units, creates armies and scouts
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

            // Process each AI player's military operations
            foreach (var (brain, militaryState, recruitReqs, resourceReqs, entity) 
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIMilitaryState>, 
                    DynamicBuffer<RecruitmentRequest>, DynamicBuffer<ResourceRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = militaryState.ValueRW;

                // Periodic military management
                if (time >= state_val.LastRecruitmentCheck + state_val.RecruitmentCheckInterval)
                {
                    state_val.LastRecruitmentCheck = time;
                    ManageMilitary(ref state, brain.ValueRO.Owner, ref state_val, recruitReqs, resourceReqs, ecb);
                }

                // Process recruitment requests
                ProcessRecruitmentRequests(ref state, brain.ValueRO.Owner, recruitReqs, ecb);

                // Organize units into armies
                OrganizeArmies(ref state, brain.ValueRO.Owner, ref state_val, ecb);

                // Assign scouts
                ManageScouts(ref state, brain.ValueRO.Owner, ref state_val, ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ManageMilitary(ref SystemState state, Faction faction, 
            ref AIMilitaryState militaryState, DynamicBuffer<RecruitmentRequest> recruitReqs,
            DynamicBuffer<ResourceRequest> resourceReqs, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Count current military units
            CountMilitaryUnits(ref state, faction, ref militaryState);

            // Count barracks
            int barracksCount = 0;
            foreach (var (barracksTag, factionTag) in SystemAPI.Query<RefRO<BarracksTag>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == faction)
                    barracksCount++;
            }
            militaryState.ActiveBarracks = barracksCount;

            // Request more barracks if needed
            if (barracksCount < TARGET_BARRACKS)
            {
                RequestBarracks(ref state, faction);
            }

            // Determine military composition based on personality
            DetermineMilitaryNeeds(ref state, faction, ref militaryState, recruitReqs);

            // Request resources for military if needed
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

        private void DetermineMilitaryNeeds(ref SystemState state, Faction faction,
            ref AIMilitaryState militaryState, DynamicBuffer<RecruitmentRequest> recruitReqs)
        {
            // Get AI personality
            AIPersonality personality = AIPersonality.Balanced;
            foreach (var (brain, a) in SystemAPI.Query<RefRO<AIBrain>, RefRO<FactionTag>>())
            {
                if (a.ValueRO.Value == faction)
                {
                    personality = brain.ValueRO.Personality;
                    break;
                }
            }

            // Base military targets
            int targetSoldiers = 8;
            int targetArchers = 6;
            int targetSiege = 2;

            // Adjust based on personality
            switch (personality)
            {
                case AIPersonality.Aggressive:
                case AIPersonality.Rush:
                    targetSoldiers = 12;
                    targetArchers = 8;
                    targetSiege = 3;
                    break;
                case AIPersonality.Defensive:
                    targetSoldiers = 6;
                    targetArchers = 10; // More ranged for defense
                    targetSiege = 1;
                    break;
                case AIPersonality.Economic:
                    targetSoldiers = 4;
                    targetArchers = 4;
                    targetSiege = 1;
                    break;
            }

            // Request units if below target
            if (militaryState.TotalSoldiers < targetSoldiers)
            {
                AddRecruitmentRequest(recruitReqs, UnitClass.Melee, 
                    targetSoldiers - militaryState.TotalSoldiers, 5);
            }

            if (militaryState.TotalArchers < targetArchers)
            {
                AddRecruitmentRequest(recruitReqs, UnitClass.Ranged,
                    targetArchers - militaryState.TotalArchers, 5);
            }

            if (militaryState.TotalSiegeUnits < targetSiege)
            {
                AddRecruitmentRequest(recruitReqs, UnitClass.Siege,
                    targetSiege - militaryState.TotalSiegeUnits, 3);
            }
        }

        private void AddRecruitmentRequest(DynamicBuffer<RecruitmentRequest> reqs,
            UnitClass unitType, int quantity, int priority)
        {
            // Check if similar request already exists
            bool exists = false;
            for (int i = 0; i < reqs.Length; i++)
            {
                if (reqs[i].UnitType == unitType && reqs[i].Priority == priority)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                reqs.Add(new RecruitmentRequest
                {
                    UnitType = unitType,
                    Quantity = quantity,
                    Priority = priority,
                    RequestingManager = Entity.Null
                });
            }
        }

        private void ProcessRecruitmentRequests(ref SystemState state, Faction faction,
            DynamicBuffer<RecruitmentRequest> recruitReqs, EntityCommandBuffer ecb)
        {
            if (recruitReqs.Length == 0) return;

            var em = state.EntityManager;

            // Find available barracks for this faction
            var availableBarracks = new NativeList<Entity>(Allocator.Temp);
            
            foreach (var (barracksTag, trainingState, factionTag, entity) in 
                SystemAPI.Query<RefRO<BarracksTag>, RefRO<TrainingState>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction && trainingState.ValueRO.Busy == 0)
                    availableBarracks.Add(entity);
            }

            if (availableBarracks.Length == 0)
            {
                availableBarracks.Dispose();
                return;
            }

            // Sort requests by priority
            var sortedReqs = new NativeList<RecruitmentRequest>(Allocator.Temp);
            for (int i = 0; i < recruitReqs.Length; i++)
                sortedReqs.Add(recruitReqs[i]);

            sortedReqs.Sort(new RecruitmentRequestComparer());

            // Process high priority requests
            int barracksIdx = 0;
            for (int i = 0; i < sortedReqs.Length && barracksIdx < availableBarracks.Length; i++)
            {
                var req = sortedReqs[i];
                if (req.Quantity <= 0) continue;

                // Queue unit in barracks
                Entity barracks = availableBarracks[barracksIdx];
                
                if (em.HasBuffer<TrainQueueItem>(barracks))
                {
                    var queue = em.GetBuffer<TrainQueueItem>(barracks);
                    
                    // Convert UnitClass to unit ID string
                    FixedString64Bytes unitId = GetUnitIdFromClass(req.UnitType);
                    
                    queue.Add(new TrainQueueItem { UnitId = unitId });
                    
                    // Reduce quantity
                    req.Quantity--;
                    
                    // Update request
                    for (int j = 0; j < recruitReqs.Length; j++)
                    {
                        if (recruitReqs[j].UnitType == req.UnitType)
                        {
                            var updated = recruitReqs[j];
                            updated.Quantity = req.Quantity;
                            recruitReqs[j] = updated;
                            break;
                        }
                    }
                }

                barracksIdx++;
            }

            // Clean up completed requests
            for (int i = recruitReqs.Length - 1; i >= 0; i--)
            {
                if (recruitReqs[i].Quantity <= 0)
                    recruitReqs.RemoveAt(i);
            }

            availableBarracks.Dispose();
            sortedReqs.Dispose();
        }

        private FixedString64Bytes GetUnitIdFromClass(UnitClass unitClass)
        {
            return unitClass switch
            {
                UnitClass.Melee => "Soldier",
                UnitClass.Ranged => "Archer",
                UnitClass.Siege => "Catapult",
                UnitClass.Magic => "Acolyte",
                UnitClass.Economy => "Builder",
                _ => "Soldier"
            };
        }

        private void OrganizeArmies(ref SystemState state, Faction faction, 
            ref AIMilitaryState militaryState, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Count armies
            int armyCount = 0;
            foreach (var (army, a) in SystemAPI.Query<RefRO<AIArmy>, RefRO<FactionTag>>())
            {
                if (a.ValueRO.Value == faction)
                    armyCount++;
            }
            militaryState.ArmiesCount = armyCount;

            // Collect unassigned military units
            var unassignedUnits = new NativeList<Entity>(Allocator.Temp);
            
            foreach (var (unitTag, factionTag, entity) in 
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>>()
                .WithNone<ArmyTag>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;
                
                // Only combat units
                if (unitTag.ValueRO.Class == UnitClass.Melee || 
                    unitTag.ValueRO.Class == UnitClass.Ranged ||
                    unitTag.ValueRO.Class == UnitClass.Siege)
                {
                    unassignedUnits.Add(entity);
                }
            }

            // Create new army if we have enough unassigned units
            if (unassignedUnits.Length >= MIN_ARMY_SIZE)
            {
                CreateArmy(ref state, faction, unassignedUnits, ecb);
            }

            unassignedUnits.Dispose();
        }

        private void CreateArmy(ref SystemState state, Faction faction,
            NativeList<Entity> units, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Get next army ID
            int fKey = (int)faction;
            if (!_nextArmyId.ContainsKey(fKey))
                _nextArmyId[fKey] = 1;
            int armyId = _nextArmyId[fKey];
            _nextArmyId[fKey] = armyId + 1;

            // Create army entity
            var armyEntity = ecb.CreateEntity();
            
            ecb.AddComponent(armyEntity, new AIArmy
            {
                ArmyId = armyId,
                Owner = faction,
                MissionEntity = Entity.Null,
                Position = float3.zero,
                TotalStrength = 0,
                IsInCombat = 0,
                LastCombatTime = 0
            });

            ecb.AddComponent(armyEntity, new FactionTag { Value = faction });
            var armyUnitBuffer = ecb.AddBuffer<ArmyUnit>(armyEntity);

            // Assign units to army (up to MAX_ARMY_SIZE)
            int assigned = 0;
            for (int i = 0; i < units.Length && assigned < MAX_ARMY_SIZE; i++)
            {
                Entity unit = units[i];
                if (!em.Exists(unit)) continue;

                // Add army tag to unit
                ecb.AddComponent(unit, new ArmyTag { ArmyId = armyId });

                // Add to army's unit buffer
                var unitTag = em.GetComponentData<UnitTag>(unit);
                var health = em.GetComponentData<Health>(unit);
                
                armyUnitBuffer.Add(new ArmyUnit
                {
                    Unit = unit,
                    Type = unitTag.Class,
                    Strength = health.Max / 10 // Simple strength calculation
                });

                assigned++;
            }
        }

        private void ManageScouts(ref SystemState state, Faction faction,
            ref AIMilitaryState militaryState, EntityCommandBuffer ecb)
        {
            // Count current scouts (units with scout assignment)
            int scoutCount = 0;
            foreach (var (unitTag, factionTag, a) in 
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>, RefRO<ArmyTag>>())
            {
                if (factionTag.ValueRO.Value == faction && a.ValueRO.ArmyId == -1) // -1 = scout
                    scoutCount++;
            }

            militaryState.ScoutsCount = scoutCount;

            // Request scouts from scouting manager if needed
            int desiredScouts = 2;
            if (scoutCount < desiredScouts)
            {
                // This will be handled by the scouting manager
            }
        }

        private void RequestBarracks(ref SystemState state, Faction faction)
        {
            // Find the building manager to request barracks construction
            foreach (var (brain, buildReqs, entity) in 
                SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<BuildRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                // Check if barracks request already exists
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
                    // Find a good location for barracks
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
            // Find faction's main base
            foreach (var (factionTag, transform, buildingTag) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>())
            {
                if (factionTag.ValueRO.Value == faction && buildingTag.ValueRO.IsBase == 1)
                {
                    // Offset from base
                    return transform.ValueRO.Position + new float3(10, 0, -5);
                }
            }

            return new float3(15, 0, 15);
        }

        private void RequestMilitaryResources(ref SystemState state, Faction faction,
            AIMilitaryState militaryState, DynamicBuffer<ResourceRequest> resourceReqs)
        {
            // Calculate resources needed for military production
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
    }

    // Comparer for sorting recruitment requests by priority
    struct RecruitmentRequestComparer : IComparer<RecruitmentRequest>
    {
        public int Compare(RecruitmentRequest a, RecruitmentRequest b)
        {
            return b.Priority.CompareTo(a.Priority); // Descending
        }
    }
}