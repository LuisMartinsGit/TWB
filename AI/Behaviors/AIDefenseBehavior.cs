// AIDefenseBehavior.cs
// Reactive defense system - responds to threats approaching the base
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheWaningBorder.AI
{
    /// <summary>
    /// Handles reactive defense behavior for AI factions.
    /// Detects threats approaching the base and rallies defense forces.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIScoutingBehavior))]
    public partial struct AIDefenseBehavior : ISystem
    {
        private const float DEFENSE_CHECK_INTERVAL = 1.0f;
        private const float THREAT_DETECTION_RADIUS = 50f;
        private const float EMERGENCY_RADIUS = 25f;
        private const float RALLY_DISTANCE = 10f;

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

            foreach (var (brain, sharedKnowledge, entity)
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AISharedKnowledge>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                float3 basePos = GetBasePosition(ref state, brain.ValueRO.Owner);
                if (basePos.Equals(float3.zero)) continue;

                // Detect threats near base
                var threats = DetectThreats(ref state, brain.ValueRO.Owner, basePos);

                if (threats.Length > 0)
                {
                    // Calculate total threat level
                    int totalThreat = 0;
                    float3 avgThreatPos = float3.zero;
                    float closestDist = float.MaxValue;

                    for (int i = 0; i < threats.Length; i++)
                    {
                        totalThreat += threats[i].Strength;
                        avgThreatPos += threats[i].Position;

                        float dist = math.distance(basePos, threats[i].Position);
                        if (dist < closestDist)
                            closestDist = dist;
                    }

                    avgThreatPos /= threats.Length;

                    // Update shared knowledge
                    var knowledge = sharedKnowledge.ValueRW;
                    knowledge.EnemyLastKnownPosition = avgThreatPos;
                    knowledge.EnemyLastSeenTime = SystemAPI.Time.ElapsedTime;
                    knowledge.EnemyEstimatedStrength = totalThreat;

                    // Emergency response if threat is very close
                    if (closestDist < EMERGENCY_RADIUS)
                    {
                        TriggerEmergencyDefense(ref state, brain.ValueRO.Owner, avgThreatPos, ecb);
                    }
                    // Standard defensive rally
                    else if (closestDist < THREAT_DETECTION_RADIUS)
                    {
                        RallyDefenders(ref state, brain.ValueRO.Owner, basePos, avgThreatPos, ecb);
                    }
                }

                threats.Dispose();
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private struct ThreatInfo
        {
            public Entity Entity;
            public float3 Position;
            public int Strength;
            public Faction Faction;
        }

        private NativeList<ThreatInfo> DetectThreats(ref SystemState state, Faction myFaction, float3 basePos)
        {
            var threats = new NativeList<ThreatInfo>(Allocator.Temp);

            // Detect enemy units near base
            foreach (var (factionTag, transform, entity) in
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithAll<UnitTag>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == myFaction) continue;

                float dist = math.distance(basePos, transform.ValueRO.Position);
                if (dist <= THREAT_DETECTION_RADIUS)
                {
                    int strength = 1;

                    // Get combat power if available
                    var em = state.EntityManager;
                    if (em.HasComponent<CombatPower>(entity))
                    {
                        strength = em.GetComponentData<CombatPower>(entity).Value;
                    }
                    else if (em.HasComponent<Damage>(entity))
                    {
                        strength = em.GetComponentData<Damage>(entity).Value;
                    }

                    threats.Add(new ThreatInfo
                    {
                        Entity = entity,
                        Position = transform.ValueRO.Position,
                        Strength = strength,
                        Faction = factionTag.ValueRO.Value
                    });
                }
            }

            return threats;
        }

        private void TriggerEmergencyDefense(ref SystemState state, Faction faction,
            float3 threatPos, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;
            float3 basePos = GetBasePosition(ref state, faction);

            Debug.Log($"[AIDefenseBehavior] {faction} EMERGENCY DEFENSE triggered! Threat at {threatPos}");

            // Rally ALL military units to defend
            foreach (var (factionTag, transform, entity) in
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithAll<UnitTag>()
                .WithNone<ScoutAssignment>() // Don't pull scouts
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                // Check if unit is a combat unit (has Damage component)
                if (!em.HasComponent<Damage>(entity)) continue;

                // Calculate interception position (between base and threat)
                float3 unitPos = transform.ValueRO.Position;
                float3 interceptPos = math.lerp(basePos, threatPos, 0.3f);

                // Issue move command through AICommandAdapter
                AICommandAdapter.IssueMove(em, entity, interceptPos);
            }
        }

        private void RallyDefenders(ref SystemState state, Faction faction,
            float3 basePos, float3 threatPos, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Calculate rally point (between base and threat, closer to base)
            float3 rallyPoint = math.lerp(basePos, threatPos, 0.25f);

            // Find idle military units to rally
            foreach (var (factionTag, transform, entity) in
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                .WithAll<UnitTag>()
                .WithNone<ArmyTag>() // Not already in an army
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                // Check if unit is a combat unit
                if (!em.HasComponent<Damage>(entity)) continue;

                // Check if unit is idle (no current target or destination)
                bool isIdle = true;

                if (em.HasComponent<Target>(entity))
                {
                    var target = em.GetComponentData<Target>(entity);
                    if (target.Value != Entity.Null && em.Exists(target.Value))
                        isIdle = false;
                }

                if (isIdle && em.HasComponent<DesiredDestination>(entity))
                {
                    var dest = em.GetComponentData<DesiredDestination>(entity);
                    if (dest.Has == 1)
                        isIdle = false;
                }

                // Rally idle units
                if (isIdle)
                {
                    float distToRally = math.distance(transform.ValueRO.Position, rallyPoint);
                    if (distToRally > RALLY_DISTANCE)
                    {
                        AICommandAdapter.IssueMove(em, entity, rallyPoint);
                    }
                }
            }
        }

        private float3 GetBasePosition(ref SystemState state, Faction faction)
        {
            foreach (var (factionTag, transform, building) in
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>())
            {
                if (factionTag.ValueRO.Value == faction && building.ValueRO.IsBase == 1)
                    return transform.ValueRO.Position;
            }
            return float3.zero;
        }
    }
}