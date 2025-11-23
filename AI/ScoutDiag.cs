// ScoutDiagnosticSystem.cs
// Press F4 to toggle scout diagnostics
// Shows which components scouts have and their current state

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIScoutingManager))]
    public partial struct ScoutDiagnosticSystem : ISystem
    {
        private float _lastCheckTime;
        private bool _enabled;

        public void OnCreate(ref SystemState state)
        {
            _lastCheckTime = 0f;
            _enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Toggle diagnostics with F4 key
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F4))
            {
                _enabled = !_enabled;
                UnityEngine.Debug.Log($"[ScoutDiagnostics] Scout diagnostics {(_enabled ? "ENABLED" : "DISABLED")}");
            }

            if (!_enabled) return;

            float time = (float)SystemAPI.Time.ElapsedTime;
            if (time < _lastCheckTime + 2.0f) return; // Check every 2 seconds
            _lastCheckTime = time;

            var em = state.EntityManager;

            UnityEngine.Debug.Log("=== SCOUT DIAGNOSTICS ===");

            // Check all units with Scout class
            int scoutCount = 0;
            foreach (var (unitTag, factionTag, transform, entity) in
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (unitTag.ValueRO.Class != UnitClass.Scout) continue;

                scoutCount++;
                var faction = factionTag.ValueRO.Value;
                var pos = transform.ValueRO.Position;

                // Check components
                bool hasDesiredDest = em.HasComponent<DesiredDestination>(entity);
                bool hasMoveSpeed = em.HasComponent<MoveSpeed>(entity);
                bool hasArmyTag = em.HasComponent<ArmyTag>(entity);
                bool hasTarget = em.HasComponent<Target>(entity);
                bool hasUserMoveOrder = em.HasComponent<UserMoveOrder>(entity);

                string components = $"DesiredDest:{hasDesiredDest}, MoveSpeed:{hasMoveSpeed}, ArmyTag:{hasArmyTag}, Target:{hasTarget}, UserMove:{hasUserMoveOrder}";

                // Check destination status
                string destStatus = "NONE";
                float distToTarget = 0f;
                if (hasDesiredDest)
                {
                    var dd = em.GetComponentData<DesiredDestination>(entity);
                    if (dd.Has == 1)
                    {
                        distToTarget = math.distance(pos, dd.Position);
                        destStatus = $"ACTIVE - Dest:{dd.Position:F1}, Dist:{distToTarget:F1}";
                    }
                    else
                    {
                        destStatus = "INACTIVE (Has=0)";
                    }
                }

                // Check move speed
                float speed = 0f;
                if (hasMoveSpeed)
                {
                    speed = em.GetComponentData<MoveSpeed>(entity).Value;
                }

                // Check if assigned to scouting
                int armyId = -999;
                if (hasArmyTag)
                {
                    armyId = em.GetComponentData<ArmyTag>(entity).ArmyId;
                }

                // Estimate time to reach destination
                string eta = "N/A";
                if (distToTarget > 0 && speed > 0)
                {
                    float timeToReach = distToTarget / speed;
                    eta = $"{timeToReach:F1}s";
                }

                UnityEngine.Debug.Log(
                    $"[Scout {scoutCount}] Faction:{faction}, " +
                    $"Pos:{pos:F1}, " +
                    $"Speed:{speed:F1}, " +
                    $"ArmyID:{armyId}, " +
                    $"Dist:{distToTarget:F1}, " +
                    $"ETA:{eta}, " +
                    $"Dest:{destStatus}");
            }

            if (scoutCount == 0)
            {
                UnityEngine.Debug.Log("[ScoutDiagnostics] No scouts found!");
            }

            // Check scout assignments in AI brains
            foreach (var (brain, scoutingState, assignments) in
                SystemAPI.Query<RefRO<AIBrain>, RefRO<AIScoutingState>, DynamicBuffer<ScoutAssignment>>())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var faction = brain.ValueRO.Owner;
                UnityEngine.Debug.Log(
                    $"[ScoutDiagnostics] {faction} - " +
                    $"Active Scouts: {scoutingState.ValueRO.ActiveScouts}/{scoutingState.ValueRO.DesiredScouts}, " +
                    $"Assignments: {assignments.Length}");

                for (int i = 0; i < assignments.Length; i++)
                {
                    var assignment = assignments[i];
                    bool exists = em.Exists(assignment.ScoutUnit);
                    
                    // Calculate time since last assignment
                    float timeSinceAssignment = time - assignment.LastReportTime;
                    
                    // Get distance to target if unit exists
                    string distInfo = "N/A";
                    if (exists)
                    {
                        var scoutPos = em.GetComponentData<LocalTransform>(assignment.ScoutUnit).Position;
                        float dist = math.distance(scoutPos, assignment.TargetArea);
                        distInfo = $"{dist:F1}";
                    }
                    
                    UnityEngine.Debug.Log(
                        $"  Assignment {i}: " +
                        $"Exists:{exists}, " +
                        $"Active:{assignment.IsActive}, " +
                        $"Target:{assignment.TargetArea:F1}, " +
                        $"Dist:{distInfo}, " +
                        $"TimeSince:{timeSinceAssignment:F1}s");
                }
            }

            UnityEngine.Debug.Log("=== END DIAGNOSTICS ===");
        }
    }
}