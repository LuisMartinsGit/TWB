// ResourceTickSystem.cs
// ECS system that applies passive resource income from buildings
// Part of: Economy/

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.Economy
{
    /// <summary>
    /// Applies passive resource income from buildings to faction banks.
    /// 
    /// Runs once per game-second and credits income to each faction based on:
    /// - SuppliesIncome: From Halls, Huts, economic buildings
    /// - IronIncome: From Foundries, mining operations
    /// - CrystalIncome: From Crystal Shrines, magical buildings
    /// - VeilsteelIncome: From advanced smelters
    /// - GlowIncome: From ley line nexuses
    /// 
    /// Only completed buildings contribute (those without UnderConstruction component).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ResourceTickSystem : ISystem
    {
        private int _lastWholeSecondGlobally;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _lastWholeSecondGlobally = (int)math.floor(state.WorldUnmanaged.Time.ElapsedTime);
            
            // Require at least one faction with resource tracking
            state.RequireForUpdate(SystemAPI.QueryBuilder()
                .WithAll<FactionTag, FactionResources, ResourceTickState>()
                .Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var nowWhole = (int)math.floor(state.WorldUnmanaged.Time.ElapsedTime);
            
            // Only run once per transition to a new whole second
            if (nowWhole == _lastWholeSecondGlobally)
                return;

            var deltaSeconds = math.max(0, nowWhole - _lastWholeSecondGlobally);
            _lastWholeSecondGlobally = nowWhole;

            // Aggregate income per faction from all provider buildings
            var perFactionIncome = new NativeParallelHashMap<byte, IncomeAccumulator>(16, Allocator.Temp);
            
            // Collect Supplies income
            CollectSuppliesIncome(ref state, ref perFactionIncome);
            
            // Collect Iron income (if component exists)
            CollectIronIncome(ref state, ref perFactionIncome);
            
            // Collect Crystal income (if component exists)
            CollectCrystalIncome(ref state, ref perFactionIncome);
            
            // Collect Veilsteel income (if component exists)
            CollectVeilsteelIncome(ref state, ref perFactionIncome);
            
            // Collect Glow income (if component exists)
            CollectGlowIncome(ref state, ref perFactionIncome);

            if (perFactionIncome.IsEmpty)
            {
                perFactionIncome.Dispose();
                return;
            }

            // Apply accumulated income to faction banks
            foreach (var (tag, bank, tick) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRW<FactionResources>, RefRW<ResourceTickState>>())
            {
                // Check how many seconds this bank missed
                int missed = math.max(0, nowWhole - tick.ValueRO.LastWholeSecond);
                if (missed <= 0) continue;

                var facKey = (byte)tag.ValueRO.Value;
                if (perFactionIncome.TryGetValue(facKey, out var income))
                {
                    // Credit income * missed seconds
                    var resources = bank.ValueRO;
                    resources.Supplies += income.Supplies * missed;
                    resources.Iron += income.Iron * missed;
                    resources.Crystal += income.Crystal * missed;
                    resources.Veilsteel += income.Veilsteel * missed;
                    resources.Glow += income.Glow * missed;
                    bank.ValueRW = resources;
                }

                tick.ValueRW.LastWholeSecond = nowWhole;
            }

            perFactionIncome.Dispose();
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // INCOME COLLECTION METHODS
        // ═══════════════════════════════════════════════════════════════════════
        
        private void CollectSuppliesIncome(ref SystemState state, 
            ref NativeParallelHashMap<byte, IncomeAccumulator> perFactionIncome)
        {
            foreach (var (tag, income) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<SuppliesIncome>>()
                    .WithNone<UnderConstruction>())  // Only completed buildings
            {
                int perSecond = income.ValueRO.PerMinute / 60;
                if (perSecond <= 0) continue;

                var key = (byte)tag.ValueRO.Value;
                if (perFactionIncome.TryGetValue(key, out var existing))
                {
                    existing.Supplies += perSecond;
                    perFactionIncome[key] = existing;
                }
                else
                {
                    perFactionIncome.TryAdd(key, new IncomeAccumulator { Supplies = perSecond });
                }
            }
        }
        
        private void CollectIronIncome(ref SystemState state,
            ref NativeParallelHashMap<byte, IncomeAccumulator> perFactionIncome)
        {
            foreach (var (tag, income) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<IronIncome>>()
                    .WithNone<UnderConstruction>())
            {
                int perSecond = income.ValueRO.PerMinute / 60;
                if (perSecond <= 0) continue;

                var key = (byte)tag.ValueRO.Value;
                if (perFactionIncome.TryGetValue(key, out var existing))
                {
                    existing.Iron += perSecond;
                    perFactionIncome[key] = existing;
                }
                else
                {
                    perFactionIncome.TryAdd(key, new IncomeAccumulator { Iron = perSecond });
                }
            }
        }
        
        private void CollectCrystalIncome(ref SystemState state,
            ref NativeParallelHashMap<byte, IncomeAccumulator> perFactionIncome)
        {
            foreach (var (tag, income) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<CrystalIncome>>()
                    .WithNone<UnderConstruction>())
            {
                int perSecond = income.ValueRO.PerMinute / 60;
                if (perSecond <= 0) continue;

                var key = (byte)tag.ValueRO.Value;
                if (perFactionIncome.TryGetValue(key, out var existing))
                {
                    existing.Crystal += perSecond;
                    perFactionIncome[key] = existing;
                }
                else
                {
                    perFactionIncome.TryAdd(key, new IncomeAccumulator { Crystal = perSecond });
                }
            }
        }
        
        private void CollectVeilsteelIncome(ref SystemState state,
            ref NativeParallelHashMap<byte, IncomeAccumulator> perFactionIncome)
        {
            foreach (var (tag, income) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<VeilsteelIncome>>()
                    .WithNone<UnderConstruction>())
            {
                int perSecond = income.ValueRO.PerMinute / 60;
                if (perSecond <= 0) continue;

                var key = (byte)tag.ValueRO.Value;
                if (perFactionIncome.TryGetValue(key, out var existing))
                {
                    existing.Veilsteel += perSecond;
                    perFactionIncome[key] = existing;
                }
                else
                {
                    perFactionIncome.TryAdd(key, new IncomeAccumulator { Veilsteel = perSecond });
                }
            }
        }
        
        private void CollectGlowIncome(ref SystemState state,
            ref NativeParallelHashMap<byte, IncomeAccumulator> perFactionIncome)
        {
            foreach (var (tag, income) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<GlowIncome>>()
                    .WithNone<UnderConstruction>())
            {
                int perSecond = income.ValueRO.PerMinute / 60;
                if (perSecond <= 0) continue;

                var key = (byte)tag.ValueRO.Value;
                if (perFactionIncome.TryGetValue(key, out var existing))
                {
                    existing.Glow += perSecond;
                    perFactionIncome[key] = existing;
                }
                else
                {
                    perFactionIncome.TryAdd(key, new IncomeAccumulator { Glow = perSecond });
                }
            }
        }
        
        /// <summary>
        /// Temporary accumulator for per-faction income per second.
        /// </summary>
        private struct IncomeAccumulator
        {
            public int Supplies;
            public int Iron;
            public int Crystal;
            public int Veilsteel;
            public int Glow;
        }
    

        private static bool FactionBankExists(EntityManager em, Faction fac)
        {
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionResources>()
            );
            
            using var banks = query.ToEntityArray(Allocator.Temp);
            for (int b = 0; b < banks.Length; b++)
            {
                var tag = em.GetComponentData<FactionTag>(banks[b]);
                if (tag.Value == fac)
                    return true;
            }
            
            return false;
        }
    }
}