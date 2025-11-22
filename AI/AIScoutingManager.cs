// AIScoutingManager.cs - FIXED VERSION WITH PROPER EXPLORATION
// Implements zone-based exploration system to prevent erratic scout behavior
// Scouts now explore the entire map systematically and return to stale zones

using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIMilitaryManager))]
    public partial struct AIScoutingManager : ISystem
    {
        private const float SCOUT_UPDATE_INTERVAL = 2.0f;
        private const int DESIRED_SCOUTS = 2;
        private const float SIGHTING_EXPIRY_TIME = 30.0f;
        private const float MAP_EXPLORATION_RADIUS = 100f;
        
        // NEW: Exploration zone settings
        private const int ZONES_PER_AXIS = 5;           // Creates a 5x5 grid = 25 zones
        private const float ZONE_SIZE = 60f;            // Each zone is 60x60 units
        private const float ZONE_ARRIVAL_DISTANCE = 15f; // Consider "arrived" when within this distance
        private const float ZONE_REVISIT_TIME = 120f;   // Revisit zones after 2 minutes
        private const float MIN_ASSIGNMENT_DURATION = 5f; // Scout must pursue target for at least 5 seconds

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            double elapsedTime = SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (brain, scoutingState, scoutAssignments, explorationZones, sightings, sharedKnowledge, entity)
                     in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIScoutingState>,
                         DynamicBuffer<ScoutAssignment>, DynamicBuffer<ExplorationZone>,
                         DynamicBuffer<EnemySighting>, RefRW<AISharedKnowledge>>()
                     .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = scoutingState.ValueRW;

                // Initialize exploration zones if needed
                if (explorationZones.Length == 0)
                {
                    InitializeExplorationZones(ref state, brain.ValueRO.Owner, explorationZones);
                }

                if (time >= state_val.LastScoutUpdate + state_val.ScoutUpdateInterval)
                {
                    state_val.LastScoutUpdate = time;

                    UpdateScoutAssignments(ref state, brain.ValueRO.Owner, ref state_val,
                        scoutAssignments, ecb);

                    ScanForEnemies(ref state, brain.ValueRO.Owner, sightings, elapsedTime);

                    CleanupOldSightings(sightings, elapsedTime);

                    UpdateSharedKnowledge(ref state, brain.ValueRO.Owner, sightings,
                        ref sharedKnowledge.ValueRW);

                    CalculateMapExploration(ref state, brain.ValueRO.Owner, ref state_val, explorationZones);
                }

                // Update zone visit tracking based on scout positions
                UpdateZoneVisits(ref state, brain.ValueRO.Owner, scoutAssignments, explorationZones, elapsedTime);

                // Assign scouts to unexplored zones
                AssignScoutPatrols(ref state, brain.ValueRO.Owner, scoutAssignments, explorationZones, ecb, elapsedTime);

                scoutingState.ValueRW = state_val;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        /// <summary>
        /// Initialize exploration zones in a grid pattern around the base
        /// </summary>
        private void InitializeExplorationZones(ref SystemState state, Faction faction,
            DynamicBuffer<ExplorationZone> zones)
        {
            float3 basePos = GetBasePosition(ref state, faction);

            // Create grid of exploration zones centered on base
            float halfMapSize = (ZONES_PER_AXIS * ZONE_SIZE) / 2f;
            float3 gridStart = basePos + new float3(-halfMapSize, 0, -halfMapSize);

            for (int x = 0; x < ZONES_PER_AXIS; x++)
            {
                for (int z = 0; z < ZONES_PER_AXIS; z++)
                {
                    float3 zoneCenter = gridStart + new float3(
                        x * ZONE_SIZE + ZONE_SIZE / 2f,
                        0,
                        z * ZONE_SIZE + ZONE_SIZE / 2f
                    );

                    zones.Add(new ExplorationZone
                    {
                        CenterPosition = zoneCenter,
                        LastVisitedTime = 0,
                        IsExplored = 0,
                        VisitCount = 0
                    });
                }
            }

            UnityEngine.Debug.Log($"[AIScoutingManager] {faction} initialized {zones.Length} exploration zones");
        }

        /// <summary>
        /// Update zone visit tracking when scouts get near zones
        /// </summary>
        private void UpdateZoneVisits(ref SystemState state, Faction faction,
            DynamicBuffer<ScoutAssignment> assignments, DynamicBuffer<ExplorationZone> zones,
            double elapsedTime)
        {
            var em = state.EntityManager;

            for (int i = 0; i < assignments.Length; i++)
            {
                var assignment = assignments[i];
                if (!em.Exists(assignment.ScoutUnit)) continue;
                if (assignment.AssignedZoneIndex < 0 || assignment.AssignedZoneIndex >= zones.Length) continue;

                var transform = em.GetComponentData<LocalTransform>(assignment.ScoutUnit);
                var zone = zones[assignment.AssignedZoneIndex];

                float distToZone = math.distance(transform.Position, zone.CenterPosition);

                // Update distance tracking
                assignment.DistanceToTarget = distToZone;
                assignments[i] = assignment;

                // If scout reached the zone, mark it as visited
                if (distToZone < ZONE_ARRIVAL_DISTANCE)
                {
                    if (zone.IsExplored == 0 || elapsedTime - zone.LastVisitedTime > 1f)
                    {
                        zone.LastVisitedTime = elapsedTime;
                        zone.IsExplored = 1;
                        zone.VisitCount++;
                        zones[assignment.AssignedZoneIndex] = zone;

                        UnityEngine.Debug.Log($"[AIScoutingManager] {faction} scout reached zone {assignment.AssignedZoneIndex} " +
                                            $"(Visit #{zone.VisitCount}) at {zone.CenterPosition}");
                    }
                }
            }
        }

        /// <summary>
        /// Calculate map exploration percentage based on visited zones
        /// </summary>
        private void CalculateMapExploration(ref SystemState state, Faction faction,
            ref AIScoutingState scoutingState, DynamicBuffer<ExplorationZone> zones)
        {
            if (zones.Length == 0)
            {
                scoutingState.MapExplorationPercent = 0f;
                return;
            }

            int exploredCount = 0;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].IsExplored == 1)
                    exploredCount++;
            }

            scoutingState.MapExplorationPercent = (exploredCount / (float)zones.Length) * 100f;
        }

        private void UpdateScoutAssignments(ref SystemState state, Faction faction,
            ref AIScoutingState scoutingState, DynamicBuffer<ScoutAssignment> assignments,
            EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            int activeScouts = 0;
            for (int i = assignments.Length - 1; i >= 0; i--)
            {
                var assignment = assignments[i];

                if (!em.Exists(assignment.ScoutUnit))
                {
                    assignments.RemoveAt(i);
                    continue;
                }

                if (assignment.IsActive == 1)
                    activeScouts++;
            }

            scoutingState.ActiveScouts = activeScouts;
            scoutingState.DesiredScouts = DESIRED_SCOUTS;

            if (activeScouts < DESIRED_SCOUTS)
            {
                RequestScouts(ref state, faction, DESIRED_SCOUTS - activeScouts);
            }
        }

        private void RequestScouts(ref SystemState state, Faction faction, int count)
        {
            foreach (var (brain, recruitReqs, entity) in
                     SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<RecruitmentRequest>>()
                     .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                bool exists = false;
                for (int i = 0; i < recruitReqs.Length; i++)
                {
                    if (recruitReqs[i].UnitType == UnitClass.Scout && recruitReqs[i].Priority == 6)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    recruitReqs.Add(new RecruitmentRequest
                    {
                        UnitType = UnitClass.Scout,
                        Quantity = count,
                        Priority = 6,
                        RequestingManager = entity
                    });
                }

                break;
            }
        }

        /// <summary>
        /// Assign scouts to explore zones, prioritizing least-recently-visited zones
        /// </summary>
        private void AssignScoutPatrols(ref SystemState state, Faction faction,
            DynamicBuffer<ScoutAssignment> assignments, DynamicBuffer<ExplorationZone> zones,
            EntityCommandBuffer ecb, double elapsedTime)
        {
            var em = state.EntityManager;

            for (int i = 0; i < assignments.Length; i++)
            {
                var assignment = assignments[i];
                if (!em.Exists(assignment.ScoutUnit)) continue;

                // Check if scout needs a new target
                bool needsNewTarget = false;

                // Case 1: Scout has no assigned zone
                if (assignment.AssignedZoneIndex < 0)
                {
                    needsNewTarget = true;
                }
                // Case 2: Scout reached its destination
                else if (assignment.DistanceToTarget < ZONE_ARRIVAL_DISTANCE)
                {
                    // Only reassign if scout has been at destination for minimum duration
                    double timeSinceAssignment = elapsedTime - assignment.AssignmentTime;
                    if (timeSinceAssignment > MIN_ASSIGNMENT_DURATION)
                    {
                        needsNewTarget = true;
                    }
                }
                // Case 3: Scout lost its movement command (shouldn't happen often with proper duration)
                else if (em.HasComponent<DesiredDestination>(assignment.ScoutUnit))
                {
                    var dest = em.GetComponentData<DesiredDestination>(assignment.ScoutUnit);
                    if (dest.Has == 0)
                    {
                        // Only reassign if enough time has passed
                        double timeSinceAssignment = elapsedTime - assignment.AssignmentTime;
                        if (timeSinceAssignment > MIN_ASSIGNMENT_DURATION)
                        {
                            needsNewTarget = true;
                        }
                    }
                }

                if (needsNewTarget)
                {
                    // Find the least-recently-explored zone
                    int bestZoneIndex = FindLeastExploredZone(zones, assignments, elapsedTime);

                    if (bestZoneIndex >= 0)
                    {
                        var targetZone = zones[bestZoneIndex];
                        var scoutPos = em.GetComponentData<LocalTransform>(assignment.ScoutUnit).Position;

                        assignment.TargetArea = targetZone.CenterPosition;
                        assignment.IsActive = 1;
                        assignment.AssignedZoneIndex = bestZoneIndex;
                        assignment.AssignmentTime = elapsedTime;
                        assignment.DistanceToTarget = math.distance(scoutPos, targetZone.CenterPosition);
                        assignments[i] = assignment;

                        ecb.SetComponent(assignment.ScoutUnit, new DesiredDestination
                        {
                            Position = targetZone.CenterPosition,
                            Has = 1
                        });

                        UnityEngine.Debug.Log($"[AIScoutingManager] {faction} scout {i} assigned to zone {bestZoneIndex} " +
                                            $"at {targetZone.CenterPosition} (Distance: {assignment.DistanceToTarget:F1})");
                    }
                }
            }
        }

        /// <summary>
        /// Find the exploration zone that needs visiting most urgently
        /// Priority: 1) Never explored, 2) Oldest exploration time, 3) Closest to any scout
        /// </summary>
        private int FindLeastExploredZone(DynamicBuffer<ExplorationZone> zones,
            DynamicBuffer<ScoutAssignment> assignments, double currentTime)
        {
            if (zones.Length == 0) return -1;

            int bestZone = -1;
            double oldestTime = double.MaxValue;
            byte foundUnexplored = 0;

            // First pass: Find zones that are either unexplored or stale
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];

                // Skip if zone is already assigned to another scout
                bool alreadyAssigned = false;
                for (int j = 0; j < assignments.Length; j++)
                {
                    if (assignments[j].AssignedZoneIndex == i && assignments[j].IsActive == 1)
                    {
                        alreadyAssigned = true;
                        break;
                    }
                }
                if (alreadyAssigned) continue;

                // Prioritize unexplored zones
                if (zone.IsExplored == 0)
                {
                    if (foundUnexplored == 0)
                    {
                        bestZone = i;
                        foundUnexplored = 1;
                    }
                    // Among unexplored, pick first one found
                    else if (bestZone < 0)
                    {
                        bestZone = i;
                    }
                }
                // If all zones explored, find the stalest one
                else if (foundUnexplored == 0)
                {
                    double timeSinceVisit = currentTime - zone.LastVisitedTime;
                    
                    // Only consider zones that haven't been visited recently
                    if (timeSinceVisit > ZONE_REVISIT_TIME)
                    {
                        if (zone.LastVisitedTime < oldestTime)
                        {
                            oldestTime = zone.LastVisitedTime;
                            bestZone = i;
                        }
                    }
                }
            }

            // If no stale zones found, pick the oldest explored zone
            if (bestZone < 0)
            {
                for (int i = 0; i < zones.Length; i++)
                {
                    var zone = zones[i];
                    
                    bool alreadyAssigned = false;
                    for (int j = 0; j < assignments.Length; j++)
                    {
                        if (assignments[j].AssignedZoneIndex == i && assignments[j].IsActive == 1)
                        {
                            alreadyAssigned = true;
                            break;
                        }
                    }
                    if (alreadyAssigned) continue;

                    if (zone.LastVisitedTime < oldestTime)
                    {
                        oldestTime = zone.LastVisitedTime;
                        bestZone = i;
                    }
                }
            }

            return bestZone;
        }

        private void ScanForEnemies(ref SystemState state, Faction faction,
            DynamicBuffer<EnemySighting> sightings, double elapsedTime)
        {
            var em = state.EntityManager;

            // Reset strength for old sightings
            for (int i = 0; i < sightings.Length; i++)
            {
                var sighting = sightings[i];
                if (elapsedTime - sighting.TimeStamp > SCOUT_UPDATE_INTERVAL)
                {
                    sighting.EstimatedStrength = 0;
                    sightings[i] = sighting;
                }
            }

            var unitCounts = new NativeHashMap<int, int>(10, Allocator.Temp);

            // Gather all friendly observers
            var observerPositions = new NativeList<float3>(Allocator.Temp);
            var observerRadii = new NativeList<float>(Allocator.Temp);

            foreach (var (factionTag, transform, los) in
                     SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<LineOfSight>>())
            {
                if (factionTag.ValueRO.Value == faction)
                {
                    observerPositions.Add(transform.ValueRO.Position);
                    observerRadii.Add(los.ValueRO.Radius);
                }
            }

            // Scan for enemy units
            foreach (var (enemyFaction, transform, combatPower, entity) in
                     SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<CombatPower>>()
                     .WithEntityAccess())
            {
                if (enemyFaction.ValueRO.Value == faction) continue;

                bool canSee = false;
                for (int i = 0; i < observerPositions.Length; i++)
                {
                    float dist = math.distance(observerPositions[i], transform.ValueRO.Position);
                    if (dist <= observerRadii[i])
                    {
                        canSee = true;
                        break;
                    }
                }

                if (canSee)
                {
                    int sightingIndex = FindOrCreateSighting(ref state, sightings, enemyFaction.ValueRO.Value,
                        transform.ValueRO.Position, elapsedTime);

                    if (sightingIndex >= 0)
                    {
                        var sighting = sightings[sightingIndex];
                        sighting.EstimatedStrength += combatPower.ValueRO.Value;
                        sightings[sightingIndex] = sighting;

                        if (unitCounts.TryGetValue(sightingIndex, out int count))
                            unitCounts[sightingIndex] = count + 1;
                        else
                            unitCounts.Add(sightingIndex, 1);
                    }
                }
            }

            // Scan for enemy buildings
            foreach (var (enemyFaction, transform, building, entity) in
                     SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>()
                     .WithEntityAccess())
            {
                if (enemyFaction.ValueRO.Value == faction) continue;

                bool canSee = false;
                for (int i = 0; i < observerPositions.Length; i++)
                {
                    float dist = math.distance(observerPositions[i], transform.ValueRO.Position);
                    if (dist <= observerRadii[i])
                    {
                        canSee = true;
                        break;
                    }
                }

                if (canSee)
                {
                    int sightingIndex = FindOrCreateSighting(ref state, sightings, enemyFaction.ValueRO.Value,
                        transform.ValueRO.Position, elapsedTime, isBase: true);

                    if (sightingIndex >= 0)
                    {
                        var sighting = sightings[sightingIndex];
                        sighting.EstimatedStrength += 50;
                        sightings[sightingIndex] = sighting;

                        if (unitCounts.TryGetValue(sightingIndex, out int count))
                            unitCounts[sightingIndex] = count + 1;
                        else
                            unitCounts.Add(sightingIndex, 1);
                    }
                }
            }

            LogSightingSummary(sightings, unitCounts, faction);

            observerPositions.Dispose();
            observerRadii.Dispose();
            unitCounts.Dispose();
        }

        private void CleanupOldSightings(DynamicBuffer<EnemySighting> sightings, double elapsedTime)
        {
            for (int i = sightings.Length - 1; i >= 0; i--)
            {
                if (elapsedTime - sightings[i].TimeStamp > SIGHTING_EXPIRY_TIME)
                    sightings.RemoveAt(i);
            }
        }

        private void UpdateSharedKnowledge(ref SystemState state, Faction faction,
            DynamicBuffer<EnemySighting> sightings, ref AISharedKnowledge knowledge)
        {
            int enemyBasesSpotted = 0;
            int enemyArmiesSpotted = 0;

            for (int i = 0; i < sightings.Length; i++)
            {
                if (sightings[i].EstimatedStrength > 0)
                {
                    if (sightings[i].IsBase == 1)
                        enemyBasesSpotted++;
                    else
                        enemyArmiesSpotted++;
                }
            }

            knowledge.EnemyBasesSpotted = enemyBasesSpotted;
            knowledge.EnemyArmiesSpotted = enemyArmiesSpotted;
        }

        private float3 GetBasePosition(ref SystemState state, Faction faction)
        {
            foreach (var (factionTag, transform, building) in
                     SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>())
            {
                if (factionTag.ValueRO.Value == faction)
                    return transform.ValueRO.Position;
            }
            return float3.zero;
        }

        private int FindOrCreateSighting(ref SystemState state, DynamicBuffer<EnemySighting> sightings,
            Faction enemyFaction, float3 position, double elapsedTime, bool isBase = false)
        {
            const float MERGE_DISTANCE = 20f;

            for (int i = 0; i < sightings.Length; i++)
            {
                var s = sightings[i];
                if (s.EnemyFaction == enemyFaction && s.IsBase == (isBase ? (byte)1 : (byte)0))
                {
                    float dist = math.distance(s.Position, position);
                    if (dist < MERGE_DISTANCE)
                    {
                        s.Position = position;
                        s.TimeStamp = elapsedTime;
                        sightings[i] = s;
                        return i;
                    }
                }
            }

            sightings.Add(new EnemySighting
            {
                EnemyFaction = enemyFaction,
                Position = position,
                TimeStamp = elapsedTime,
                EstimatedStrength = 0,
                IsBase = isBase ? (byte)1 : (byte)0
            });

            return sightings.Length - 1;
        }

        private void LogSightingSummary(DynamicBuffer<EnemySighting> sightings,
            NativeHashMap<int, int> unitCounts, Faction faction)
        {
            if (sightings.Length == 0) return;

            int totalStrength = 0;
            int totalUnits = 0;
            int totalSightings = 0;

            for (int i = 0; i < sightings.Length; i++)
            {
                var sighting = sightings[i];
                if (sighting.EstimatedStrength > 0)
                {
                    totalStrength += sighting.EstimatedStrength;
                    totalSightings++;

                    if (unitCounts.TryGetValue(i, out int count))
                        totalUnits += count;
                }
            }

            if (totalSightings > 0)
            {
                UnityEngine.Debug.Log($"[AI Scouting] {faction} detected {totalSightings} enemy groups " +
                                    $"({totalUnits} units, Strength: {totalStrength})");
            }
        }
    }

    internal struct CombatPower : IComponentData
    {
        public int Value { get; internal set; }
    }
}