using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

// ----------------- Shared components (kept local for convenience) -----------------


// NEW: point to return to after combat (last stand / move destination)
public struct GuardPoint : IComponentData
{
    public float3 Position;
    public byte Has; // 0/1
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MoveAndTurnSystem : ISystem
{
    const float StopDistance        = 0.1f;
    const float DefaultMoveSpeed    = 3.5f;
    const float DefaultAttackRange  = 4.0f;
    const float DefaultLOS          = 8.0f;   // auto-acquire vision
    const int   DefaultDamage       = 5;
    const float DefaultFireCooldown = 0.5f;

    [BurstCompile]
    public void OnCreate(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        float dt = SystemAPI.Time.DeltaTime;

        // =========================================================================
        // A) Bridge MoveCommand -> DesiredDestination
        //    - Buildings ignore & clear movement
        //    - When a move is set, store a GuardPoint at that destination
        // =========================================================================
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mc, e) in SystemAPI.Query<RefRO<MoveCommand>>().WithEntityAccess())
            {
                // Buildings don't move
                if (SystemAPI.HasComponent<BuildingTag>(e))
                {
                    ecb.RemoveComponent<MoveCommand>(e);
                    if (em.HasComponent<DesiredDestination>(e))
                    {
                        var dd = em.GetComponentData<DesiredDestination>(e);
                        if (dd.Has != 0) { dd.Has = 0; ecb.SetComponent(e, dd); }
                    }
                    continue;
                }

                if (!em.HasComponent<DesiredDestination>(e))
                    ecb.AddComponent(e, new DesiredDestination { Position = mc.ValueRO.Destination, Has = 1 });
                else
                {
                    var dd = em.GetComponentData<DesiredDestination>(e);
                    dd.Position = mc.ValueRO.Destination;
                    dd.Has = 1;
                    ecb.SetComponent(e, dd);
                }

                // Set/overwrite guard point to the commanded destination
                if (!em.HasComponent<GuardPoint>(e))
                    ecb.AddComponent(e, new GuardPoint { Position = mc.ValueRO.Destination, Has = 1 });
                else
                    ecb.SetComponent(e, new GuardPoint { Position = mc.ValueRO.Destination, Has = 1 });

                ecb.RemoveComponent<MoveCommand>(e);
            }

            ecb.Playback(em);
        }

        // =========================================================================
        // B) AUTO-ACQUIRE: When IDLE (no move in progress) and no AttackCommand,
        //    pick the nearest enemy within LOS and issue AttackCommand.
        //    Move orders suppress this until arrival (dd.Has == 0).
        // =========================================================================
        {
            // Build a light list of enemy units with positions
            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, FactionTag>()
                .Build();

            using var all = enemyQuery.ToEntityArray(Allocator.Temp);
            using var allXF = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var allFaction = enemyQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Iterate possible seekers: must be units, not buildings, alive, with LocalTransform
            foreach (var (xf, e) in SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<BuildingTag>(e)) continue;
                if (!SystemAPI.HasComponent<UnitTag>(e)) continue;
                if (SystemAPI.HasComponent<AttackCommand>(e)) continue;    // already attacking
                if (SystemAPI.HasComponent<Health>(e) && em.GetComponentData<Health>(e).Value <= 0) continue;

                // Suppress auto while moving
                if (SystemAPI.HasComponent<DesiredDestination>(e) &&
                    em.GetComponentData<DesiredDestination>(e).Has != 0)
                    continue;

                // Determine our faction; only acquire enemies
                if (!SystemAPI.HasComponent<FactionTag>(e)) continue;
                var myFaction = em.GetComponentData<FactionTag>(e).Value;

                // vision range
                float los = DefaultLOS;
                if (SystemAPI.HasComponent<LineOfSight>(e))
                {
                    var v = em.GetComponentData<LineOfSight>(e).Radius;
                    if (v > 0) los = v;
                }

                float losSqr = los * los;
                float3 myPos = xf.ValueRO.Position;

                // find nearest enemy in LOS
                Entity best = Entity.Null;
                float bestDistSqr = float.MaxValue;

                for (int i = 0; i < all.Length; i++)
                {
                    var other = all[i];
                    if (other == e) continue;
                    if (!em.Exists(other)) continue;

                    // must be enemy and (preferably) a unit
                    var f = allFaction[i].Value;
                    if (f == myFaction) continue;

                    if (SystemAPI.HasComponent<Health>(other) && em.GetComponentData<Health>(other).Value <= 0)
                        continue;

                    // Optional: ignore buildings as targets; only attack units
                    if (!SystemAPI.HasComponent<UnitTag>(other)) continue;

                    float3 p = allXF[i].Position;
                    float3 d = p - myPos; d.y = 0;
                    float ds = math.lengthsq(d);
                    if (ds <= losSqr && ds < bestDistSqr)
                    {
                        best = other;
                        bestDistSqr = ds;
                    }
                }

                if (best != Entity.Null)
                {
                    // Capture current spot as guard point if we don't already have one
                    if (!em.HasComponent<GuardPoint>(e))
                        ecb.AddComponent(e, new GuardPoint { Position = myPos, Has = 1 });

                    // Start attacking
                    if (!em.HasComponent<AttackCommand>(e)) ecb.AddComponent<AttackCommand>(e);
                    ecb.SetComponent(e, new AttackCommand { Target = best });

                    // Ensure we aren't simultaneously marching somewhere
                    if (em.HasComponent<DesiredDestination>(e))
                    {
                        var dd = em.GetComponentData<DesiredDestination>(e);
                        if (dd.Has != 0) { dd.Has = 0; ecb.SetComponent(e, dd); }
                    }
                }
            }

            ecb.Playback(em);
        }

        // =========================================================================
        // C) ATTACK LOOP: chase target; when in range, apply damage on cooldown.
        //    If target lost/dead, clear order and RETURN to GuardPoint (if any).
        // =========================================================================
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (atk, e) in SystemAPI.Query<RefRO<AttackCommand>>().WithEntityAccess())
            {
                // Buildings don't attack
                if (SystemAPI.HasComponent<BuildingTag>(e))
                {
                    ecb.RemoveComponent<AttackCommand>(e);
                    continue;
                }

                var target = atk.ValueRO.Target;
                bool validTarget = target != Entity.Null && em.Exists(target) && em.HasComponent<LocalTransform>(target);

                // Target gone → clear order + return
                if (!validTarget)
                {
                    ClearAttackAndReturn(ref ecb, em, e);
                    continue;
                }

                // Dead target → clear order + return
                if (em.HasComponent<Health>(target) && em.GetComponentData<Health>(target).Value <= 0)
                {
                    ClearAttackAndReturn(ref ecb, em, e);
                    continue;
                }

                if (!em.HasComponent<LocalTransform>(e)) continue;

                float range = DefaultAttackRange;
                if (em.HasComponent<AttackRange>(e))
                {
                    float r = em.GetComponentData<AttackRange>(e).Value;
                    if (r > 0) range = r;
                }

                var selfXf = em.GetComponentData<LocalTransform>(e);
                var tgtXf  = em.GetComponentData<LocalTransform>(target);

                float3 to = tgtXf.Position - selfXf.Position; to.y = 0f;
                float dist = math.length(to);

                if (dist > range * 0.98f)
                {
                    // Chase: DesiredDestination follows target
                    if (!em.HasComponent<DesiredDestination>(e))
                        ecb.AddComponent(e, new DesiredDestination { Position = tgtXf.Position, Has = 1 });
                    else
                    {
                        var dd = em.GetComponentData<DesiredDestination>(e);
                        dd.Position = tgtXf.Position;
                        dd.Has = 1;
                        ecb.SetComponent(e, dd);
                    }

                    // Set guard point if missing (return here after combat)
                    if (!em.HasComponent<GuardPoint>(e))
                        ecb.AddComponent(e, new GuardPoint { Position = selfXf.Position, Has = 1 });
                }
                else
                {
                    // In range: stop moving
                    if (em.HasComponent<DesiredDestination>(e))
                    {
                        var dd = em.GetComponentData<DesiredDestination>(e);
                        if (dd.Has != 0) { dd.Has = 0; ecb.SetComponent(e, dd); }
                    }

                    // Fire on cooldown
                    int dmg = DefaultDamage;
                    if (em.HasComponent<Damage>(e))
                    {
                        int d = em.GetComponentData<Damage>(e).Value;
                        if (d > 0) dmg = d;
                    }

                    if (em.HasComponent<AttackCooldown>(e))
                    {
                        var cd = em.GetComponentData<AttackCooldown>(e);
                        float fireCd = cd.Cooldown > 0f ? cd.Cooldown : DefaultFireCooldown;

                        cd.Timer -= dt;
                        if (cd.Timer <= 0f)
                        {
                            if (em.HasComponent<Health>(target))
                            {
                                var h = em.GetComponentData<Health>(target);
                                h.Value -= math.max(1, dmg);
                                ecb.SetComponent(target, h);

                                if (h.Value <= 0)
                                {
                                    // Target will be cleaned later; clear & return
                                    ClearAttackAndReturn(ref ecb, em, e);
                                }
                            }
                            cd.Timer = fireCd;
                        }

                        ecb.SetComponent(e, cd);
                    }
                    else
                    {
                        // First shot + add cooldown
                        if (em.HasComponent<Health>(target))
                        {
                            var h = em.GetComponentData<Health>(target);
                            h.Value -= math.max(1, dmg);
                            ecb.SetComponent(target, h);
                            if (h.Value <= 0)
                                ClearAttackAndReturn(ref ecb, em, e);
                        }
                        ecb.AddComponent(e, new AttackCooldown { Cooldown = DefaultFireCooldown, Timer = DefaultFireCooldown });
                    }
                }
            }

            ecb.Playback(em);
        }

        // =========================================================================
        // D) Movement + facing (units only). Add MoveSpeed to units if missing.
        // =========================================================================
        var ecbPost = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (xf, dd, e) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRW<DesiredDestination>>()
                     .WithEntityAccess())
        {
            if (dd.ValueRO.Has == 0) continue;
            if (SystemAPI.HasComponent<BuildingTag>(e)) { dd.ValueRW.Has = 0; continue; }

            float speed = DefaultMoveSpeed;
            if (em.HasComponent<MoveSpeed>(e))
            {
                float ms = em.GetComponentData<MoveSpeed>(e).Value;
                if (ms > 0) speed = ms;
            }
            else if (SystemAPI.HasComponent<UnitTag>(e))
            {
                ecbPost.AddComponent(e, new MoveSpeed { Value = DefaultMoveSpeed });
            }

            float3 pos  = xf.ValueRO.Position;
            float3 goal = dd.ValueRO.Position;

            float3 to = goal - pos; to.y = 0f;
            float distSqr = math.lengthsq(to);
            if (distSqr <= (StopDistance * StopDistance))
            {
                var cleared = dd.ValueRO; cleared.Has = 0; dd.ValueRW = cleared;
                continue;
            }

            float dist = math.sqrt(distSqr);
            float3 dir = to / math.max(1e-5f, dist);

            float step = math.min(speed * dt, dist);
            var t = xf.ValueRO;
            t.Position = pos + dir * step;

            if (math.lengthsq(dir) > 1e-8f)
            {
                float3 fwd = math.normalize(new float3(dir.x, 0f, dir.z));
                t.Rotation = quaternion.RotateY(math.atan2(fwd.x, fwd.z));
            }

            xf.ValueRW = t;
        }

        ecbPost.Playback(em);

        // =========================================================================
        // E) Return-to-guard after combat:
        //    If not moving, no attack order, has GuardPoint, and far from it → go back.
        // =========================================================================
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (xf, gp, e) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<GuardPoint>>().WithEntityAccess())
            {
                if (gp.ValueRO.Has == 0) continue;
                if (SystemAPI.HasComponent<BuildingTag>(e)) continue;

                bool moving = SystemAPI.HasComponent<DesiredDestination>(e) &&
                              em.GetComponentData<DesiredDestination>(e).Has != 0;
                if (moving) continue;

                if (SystemAPI.HasComponent<AttackCommand>(e)) continue; // still fighting

                float3 pos = xf.ValueRO.Position;
                float3 to  = gp.ValueRO.Position - pos; to.y = 0f;
                if (math.lengthsq(to) > (StopDistance * StopDistance))
                {
                    if (!em.HasComponent<DesiredDestination>(e))
                        ecb.AddComponent(e, new DesiredDestination { Position = gp.ValueRO.Position, Has = 1 });
                    else
                    {
                        var dd = em.GetComponentData<DesiredDestination>(e);
                        dd.Position = gp.ValueRO.Position; dd.Has = 1;
                        ecb.SetComponent(e, dd);
                    }
                }
            }

            ecb.Playback(em);
        }

        // =========================================================================
        // F) Death cleanup: clear attackers targeting dead, then destroy dead.
        // =========================================================================
        {
            var deadSet = new NativeParallelHashSet<Entity>(128, Allocator.Temp);

            foreach (var (h, e) in SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
                if (h.ValueRO.Value <= 0) deadSet.Add(e);

            if (!deadSet.IsEmpty)
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (atk, e) in SystemAPI.Query<RefRO<AttackCommand>>().WithEntityAccess())
                {
                    var tgt = atk.ValueRO.Target;
                    if (tgt != Entity.Null && deadSet.Contains(tgt))
                    {
                        ecb.RemoveComponent<AttackCommand>(e);
                        if (em.HasComponent<DesiredDestination>(e))
                        {
                            var dd = em.GetComponentData<DesiredDestination>(e);
                            if (dd.Has != 0) { dd.Has = 0; ecb.SetComponent(e, dd); }
                        }
                        if (em.HasComponent<Target>(e))
                            ecb.SetComponent(e, new Target { Value = Entity.Null });

                        // After losing target, return to guard if any
                        if (em.HasComponent<GuardPoint>(e))
                        {
                            var gp = em.GetComponentData<GuardPoint>(e);
                            if (gp.Has != 0)
                            {
                                if (!em.HasComponent<DesiredDestination>(e))
                                    ecb.AddComponent(e, new DesiredDestination { Position = gp.Position, Has = 1 });
                                else
                                {
                                    var dd2 = em.GetComponentData<DesiredDestination>(e);
                                    dd2.Position = gp.Position; dd2.Has = 1;
                                    ecb.SetComponent(e, dd2);
                                }
                            }
                        }
                    }
                }

                using (var deadList = deadSet.ToNativeArray(Allocator.Temp))
                {
                    for (int i = 0; i < deadList.Length; i++)
                        ecb.DestroyEntity(deadList[i]);
                }

                ecb.Playback(em);
            }

            deadSet.Dispose();
        }
    }

    // Helper: clear attack and start returning to guard point (if any)
    static void ClearAttackAndReturn(ref EntityCommandBuffer ecb, EntityManager em, Entity e)
    {
        if (em.HasComponent<AttackCommand>(e)) ecb.RemoveComponent<AttackCommand>(e);
        if (em.HasComponent<Target>(e)) ecb.SetComponent(e, new Target { Value = Entity.Null });
        if (em.HasComponent<DesiredDestination>(e))
        {
            var dd = em.GetComponentData<DesiredDestination>(e);
            if (dd.Has != 0) { dd.Has = 0; ecb.SetComponent(e, dd); }
        }

        if (em.HasComponent<GuardPoint>(e))
        {
            var gp = em.GetComponentData<GuardPoint>(e);
            if (gp.Has != 0)
            {
                if (!em.HasComponent<DesiredDestination>(e))
                    ecb.AddComponent(e, new DesiredDestination { Position = gp.Position, Has = 1 });
                else
                {
                    var dd = em.GetComponentData<DesiredDestination>(e);
                    dd.Position = gp.Position; dd.Has = 1;
                    ecb.SetComponent(e, dd);
                }
            }
        }
    }
}
