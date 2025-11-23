// File: Assets/Scripts/ECS/Systems/ApplySuppliesIncomeSystem.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ApplySuppliesIncomeSystem : ISystem
{
    private int _lastWholeSecondGlobally; // optimization guard

    public void OnCreate(ref SystemState state)
    {
        _lastWholeSecondGlobally = (int)math.floor(state.WorldUnmanaged.Time.ElapsedTime);
        state.RequireForUpdate(SystemAPI.QueryBuilder()
            .WithAll<FactionTag, FactionResources, ResourceTickState>()
            .Build());
    }

    public void OnUpdate(ref SystemState state)
    {
        var nowWhole = (int)math.floor(state.WorldUnmanaged.Time.ElapsedTime);
        if (nowWhole == _lastWholeSecondGlobally)
            return;

        // We only run once per transition to a new whole second.
        var deltaSeconds = math.max(0, nowWhole - _lastWholeSecondGlobally);
        _lastWholeSecondGlobally = nowWhole;

        // 1) Aggregate per-faction per-second supplies income from all providers.
        // perSecond = PerMinute / 60 (integer)
        var perFactionIncome = new NativeParallelHashMap<byte, int>(16, Allocator.Temp);
        foreach (var (tag, income) in SystemAPI.Query<RefRO<FactionTag>, RefRO<SuppliesIncome>>())
        {
            int perSecond = income.ValueRO.PerMinute / 60;
            if (perSecond <= 0) continue;

            var key = (byte)tag.ValueRO.Value;
            if (perFactionIncome.TryGetValue(key, out int existing))
                perFactionIncome[key] = existing + perSecond;
            else
                perFactionIncome.TryAdd(key, perSecond);
        }

        if (perFactionIncome.IsEmpty) { perFactionIncome.Dispose(); return; }

        // 2) For each bank, if it's time, credit income * deltaSeconds.
        foreach (var (tag, bank, tick) in SystemAPI.Query<RefRO<FactionTag>, RefRW<FactionResources>, RefRW<ResourceTickState>>())
        {
            // Every bank ticks on the same whole-second step; we still respect its own last tick for robustness.
            int missed = math.max(0, nowWhole - tick.ValueRO.LastWholeSecond);
            if (missed <= 0) continue;

            var facKey = (byte)tag.ValueRO.Value;
            if (perFactionIncome.TryGetValue(facKey, out int perSec))
            {
                bank.ValueRW.Supplies += perSec * missed;
            }

            tick.ValueRW.LastWholeSecond = nowWhole;
        }

        perFactionIncome.Dispose();
    }
}
