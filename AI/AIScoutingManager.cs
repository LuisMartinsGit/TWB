// AIScoutingManager.cs
// Requests scouts, tracks enemy sightings and map intel
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIMilitaryManager))]
    public partial struct AIScoutingManager : ISystem
    {
        private const float SCOUT_UPDATE_INTERVAL = 2.0f;
        private const int DESIRED_SCOUTS = 2;
        private const float SIGHTING_EXPIRY_TIME = 30.0f; // Sightings older than this are removed
        private const float MAP_EXPLORATION_RADIUS = 100f;

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

            // Process each AI player's scouting operations
            foreach (var (brain, scoutingState, scoutAssignments, sightings, sharedKnowledge, entity) 
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIScoutingState>, 
                    DynamicBuffer<ScoutAssignment>, DynamicBuffer<EnemySighting>, 
                    RefRW<AISharedKnowledge>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = scoutingState.ValueRW;

                // Periodic scouting updates
                if (time >= state_val.LastScoutUpdate + state_val.ScoutUpdateInterval)
                {
                    state_val.LastScoutUpdate = time;
                    
                    // Update scout assignments
                    UpdateScoutAssignments(ref state, brain.ValueRO.Owner, ref state_val, 
                        scoutAssignments, ecb);

                    // Scan for enemy sightings
                    ScanForEnemies(ref state, brain.ValueRO.Owner, sightings, elapsedTime);

                    // Clean up old sightings
                    CleanupOldSightings(sightings, elapsedTime);

                    // Update shared knowledge
                    UpdateSharedKnowledge(ref state, brain.ValueRO.Owner, sightings, 
                        ref sharedKnowledge.ValueRW);

                    // Calculate map exploration
                    CalculateMapExploration(ref state, brain.ValueRO.Owner, ref state_val);
                }

                // Assign new patrol areas to scouts
                AssignScoutPatrols(ref state, brain.ValueRO.Owner, scoutAssignments, ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void UpdateScoutAssignments(ref SystemState state, Faction faction,
            ref AIScoutingState scoutingState, DynamicBuffer<ScoutAssignment> assignments,
            EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Count active scouts
            int activeScouts = 0;
            for (int i = assignments.Length - 1; i >= 0; i--)
            {
                var assignment = assignments[i];
                
                // Check if scout still exists
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

            // Request more scouts if needed
            if (activeScouts < DESIRED_SCOUTS)
            {
                RequestScouts(ref state, faction, DESIRED_SCOUTS - activeScouts);
            }

            // Recruit scouts from unassigned units
            RecruitScouts(ref state, faction, assignments, ecb);
        }

        private void RequestScouts(ref SystemState state, Faction faction, int count)
        {
            // Request fast units from military manager to use as scouts
            foreach (var (brain, recruitReqs, entity) in 
                SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<RecruitmentRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                // Check if scout request already exists
                bool exists = false;
                for (int i = 0; i < recruitReqs.Length; i++)
                {
                    if (recruitReqs[i].UnitType == UnitClass.Ranged && recruitReqs[i].Priority == 6)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    recruitReqs.Add(new RecruitmentRequest
                    {
                        UnitType = UnitClass.Ranged, // Archers are good scouts
                        Quantity = count,
                        Priority = 6,
                        RequestingManager = entity
                    });
                }

                break;
            }
        }

        private void RecruitScouts(ref SystemState state, Faction faction,
            DynamicBuffer<ScoutAssignment> assignments, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Find unassigned fast units
            foreach (var (unitTag, factionTag, moveSpeed, entity) in 
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>, RefRO<MoveSpeed>>()
                .WithNone<ArmyTag>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;
                if (assignments.Length >= DESIRED_SCOUTS) break;

                // Check if already assigned as scout
                bool alreadyScout = false;
                for (int i = 0; i < assignments.Length; i++)
                {
                    if (assignments[i].ScoutUnit == entity)
                    {
                        alreadyScout = true;
                        break;
                    }
                }

                if (!alreadyScout && (unitTag.ValueRO.Class == UnitClass.Ranged || 
                                      unitTag.ValueRO.Class == UnitClass.Melee))
                {
                    // Assign as scout with special army ID
                    ecb.AddComponent(entity, new ArmyTag { ArmyId = -1 }); // -1 = scout

                    assignments.Add(new ScoutAssignment
                    {
                        ScoutUnit = entity,
                        TargetArea = float3.zero,
                        IsActive = 0,
                        LastReportTime = 0
                    });
                }
            }
        }

        private void AssignScoutPatrols(ref SystemState state, Faction faction,
            DynamicBuffer<ScoutAssignment> assignments, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Get base position
            float3 basePos = GetBasePosition(ref state, faction);

            for (int i = 0; i < assignments.Length; i++)
            {
                var assignment = assignments[i];
                if (!em.Exists(assignment.ScoutUnit)) continue;

                // Check if scout needs new target
                bool needsNewTarget = assignment.IsActive == 0;
                
                if (!needsNewTarget && em.HasComponent<DesiredDestination>(assignment.ScoutUnit))
                {
                    var dest = em.GetComponentData<DesiredDestination>(assignment.ScoutUnit);
                    if (dest.Has == 0)
                        needsNewTarget = true;
                }

                if (needsNewTarget)
                {
                    // Assign new patrol area
                    float3 patrolArea = GenerateScoutingTarget(basePos, i, time);
                    
                    assignment.TargetArea = patrolArea;
                    assignment.IsActive = 1;
                    assignment.LastReportTime = time;
                    assignments[i] = assignment;

                    // Set scout destination
                    ecb.SetComponent(assignment.ScoutUnit, new DesiredDestination
                    {
                        Position = patrolArea,
                        Has = 1
                    });
                }
            }
        }

        private float3 GenerateScoutingTarget(float3 basePos, int scoutIndex, float time)
        {
            // Generate different patrol patterns for different scouts
            float angle = (scoutIndex * 90f + time * 10f) * math.PI / 180f;
            float distance = 50f + (scoutIndex * 20f);

            return basePos + new float3(
                math.cos(angle) * distance,
                0,
                math.sin(angle) * distance
            );
        }

        private void ScanForEnemies(ref SystemState state, Faction faction,
            DynamicBuffer<EnemySighting> sightings, double elapsedTime)
        {
            var em = state.EntityManager;

            // Get scout positions and their line of sight
            var scoutPositions = new NativeList<float3>(Allocator.Temp);
            var scoutRadii = new NativeList<float>(Allocator.Temp);

            foreach (var (armyTag, factionTag, transform, los) in 
                SystemAPI.Query<RefRO<ArmyTag>, RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<LineOfSight>>())
            {
                if (factionTag.ValueRO.Value == faction && armyTag.ValueRO.ArmyId == -1) // Scout
                {
                    scoutPositions.Add(transform.ValueRO.Position);
                    scoutRadii.Add(los.ValueRO.Radius);
                }
            }

            // Scan for enemy units and buildings
            foreach (var (factionTag, transform, health, unitTag, entity) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<Health>, RefRO<UnitTag>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction) continue; // Skip own units

                // Check if any scout can see this enemy
                bool visible = false;
                for (int i = 0; i < scoutPositions.Length; i++)
                {
                    float dist = math.distance(scoutPositions[i], transform.ValueRO.Position);
                    if (dist <= scoutRadii[i])
                    {
                        visible = true;
                        break;
                    }
                }

                if (visible)
                {
                    // Add or update sighting
                    AddEnemySighting(sightings, factionTag.ValueRO.Value, 
                        transform.ValueRO.Position, elapsedTime, 
                        health.ValueRO.Max / 10, false);
                }
            }

            // Scan for enemy buildings
            foreach (var (factionTag, transform, health, buildingTag, entity) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<Health>, RefRO<BuildingTag>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction) continue;

                bool visible = false;
                for (int i = 0; i < scoutPositions.Length; i++)
                {
                    float dist = math.distance(scoutPositions[i], transform.ValueRO.Position);
                    if (dist <= scoutRadii[i])
                    {
                        visible = true;
                        break;
                    }
                }

                if (visible)
                {
                    AddEnemySighting(sightings, factionTag.ValueRO.Value,
                        transform.ValueRO.Position, elapsedTime,
                        health.ValueRO.Max / 20, true);
                }
            }

            scoutPositions.Dispose();
            scoutRadii.Dispose();
        }

        private void AddEnemySighting(DynamicBuffer<EnemySighting> sightings,
            Faction enemyFaction, float3 position, double time, int strength, bool isBase)
        {
            // Check if similar sighting already exists (within 10 units)
            for (int i = 0; i < sightings.Length; i++)
            {
                var sighting = sightings[i];
                if (sighting.EnemyFaction == enemyFaction)
                {
                    float dist = math.distance(sighting.Position, position);
                    if (dist < 10f)
                    {
                        // Update existing sighting
                        sighting.Position = position;
                        sighting.TimeStamp = time;
                        sighting.EstimatedStrength = strength;
                        sightings[i] = sighting;
                        return;
                    }
                }
            }

            // Add new sighting
            sightings.Add(new EnemySighting
            {
                EnemyFaction = enemyFaction,
                Position = position,
                TimeStamp = time,
                EstimatedStrength = strength,
                IsBase = isBase ? (byte)1 : (byte)0
            });
        }

        private void CleanupOldSightings(DynamicBuffer<EnemySighting> sightings, double currentTime)
        {
            // Remove sightings older than expiry time
            for (int i = sightings.Length - 1; i >= 0; i--)
            {
                if (currentTime - sightings[i].TimeStamp > SIGHTING_EXPIRY_TIME)
                {
                    sightings.RemoveAt(i);
                }
            }
        }

        private void UpdateSharedKnowledge(ref SystemState state, Faction faction,
            DynamicBuffer<EnemySighting> sightings, ref AISharedKnowledge knowledge)
        {
            // Find most recent and strongest enemy position
            if (sightings.Length > 0)
            {
                double latestTime = 0;
                int totalStrength = 0;
                int bases = 0;

                for (int i = 0; i < sightings.Length; i++)
                {
                    var sighting = sightings[i];
                    totalStrength += sighting.EstimatedStrength;
                    
                    if (sighting.IsBase == 1)
                        bases++;

                    if (sighting.TimeStamp > latestTime)
                    {
                        latestTime = sighting.TimeStamp;
                        knowledge.EnemyLastKnownPosition = sighting.Position;
                        knowledge.EnemyLastSeenTime = sighting.TimeStamp;
                    }
                }

                knowledge.EnemyEstimatedStrength = totalStrength;
                knowledge.KnownEnemyBases = bases;
            }
        }

        private void CalculateMapExploration(ref SystemState state, Faction faction,
            ref AIScoutingState scoutingState)
        {
            // Simple calculation: percentage of map revealed by fog of war
            // This is a simplified version - actual implementation would check fog of war system
            
            int revealedTiles = 0;
            int totalTiles = 100; // Simplified

            // Check if FogOfWarManager exists
            if (FogOfWarManager.Instance != null)
            {
                // In a real implementation, you would check the fog of war grid
                // For now, estimate based on number of scouts and time
                revealedTiles = math.min(scoutingState.ActiveScouts * 10, totalTiles);
            }

            scoutingState.MapExplorationPercent = (revealedTiles * 100f) / totalTiles;
        }

        private float3 GetBasePosition(ref SystemState state, Faction faction)
        {
            // Find faction's main base
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