// Miner.cs - Collects iron from iron deposits
// WITH ENTITY COMMAND BUFFER SUPPORT

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Factions.Humans.Era1.Units
{
    public static class Miner
    {
        // Defaults if JSON is missing
        private const float DefaultHP = 50f;
        private const float DefaultSpeed = 3.5f;
        private const float DefaultDamage = 2f;
        private const float DefaultLoS = 10f;
        private const float DefaultGatherSpeed = 1f;
        private const int DefaultCarryCapacity = 1;

        /// <summary>
        /// EntityCommandBuffer version - for use in training systems
        /// Creates entity structure with PLACEHOLDER values
        /// Real stats are applied by the training system from JSON
        /// </summary>
        public static Entity CreateWithECB(EntityCommandBuffer ecb, float3 pos, Faction fac)
        {
            var e = ecb.CreateEntity();
            
            // Add all components
            ecb.AddComponent(e, new PresentationId { Id = 203 }); // Unique ID for miner
            ecb.AddComponent(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            ecb.AddComponent(e, new FactionTag { Value = fac });
            ecb.AddComponent(e, new UnitTag { Class = UnitClass.Economy });
            
            // PLACEHOLDER values - will be overwritten by JSON stats
            ecb.AddComponent(e, new Health { Value = 1, Max = 1 });
            ecb.AddComponent(e, new MoveSpeed { Value = 1f });
            ecb.AddComponent(e, new Damage { Value = 1 });
            ecb.AddComponent(e, new LineOfSight { Radius = 1f });
            
            // Miner-specific components
            ecb.AddComponent(e, new MinerTag());
            ecb.AddComponent(e, new MinerState
            {
                AssignedDeposit = Entity.Null,
                CurrentLoad = 0,
                GatherTimer = 0f,
                State = MinerWorkState.Idle
            });
            
            // Radius for collision/spacing
            ecb.AddComponent(e, new Radius { Value = 0.5f });
            
            return e;
        }

        /// <summary>
        /// EntityManager version - loads stats from JSON immediately
        /// For use in game initialization and direct spawning
        /// </summary>
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            // Try to fetch the "Miner" unit from the tech DB
            float hp = DefaultHP;
            float speed = DefaultSpeed;
            float damage = DefaultDamage;
            float los = DefaultLoS;
            float gatherSpeed = DefaultGatherSpeed;
            int carryCapacity = DefaultCarryCapacity;

            if (HumanTech.Instance != null && HumanTech.Instance.TryGetUnit("Miner", out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.speed > 0) speed = def.speed;
                if (def.damage > 0) damage = def.damage;
                if (def.lineOfSight > 0) los = def.lineOfSight;
                if (def.gatheringSpeed > 0) gatherSpeed = def.gatheringSpeed;
                if (def.carryCapacity > 0) carryCapacity = def.carryCapacity;
            }

            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(LineOfSight),
                typeof(Radius),
                typeof(MinerTag),
                typeof(MinerState)
            );

            em.SetComponentData(e, new PresentationId { Id = 203 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Economy });

            em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(e, new MoveSpeed { Value = speed });
            em.SetComponentData(e, new Damage { Value = (int)damage });
            em.SetComponentData(e, new LineOfSight { Radius = los });
            em.SetComponentData(e, new Radius { Value = 0.5f });
            
            em.SetComponentData(e, new MinerState
            {
                AssignedDeposit = Entity.Null,
                CurrentLoad = 0,
                GatherTimer = 0f,
                State = MinerWorkState.Idle
            });

            return e;
        }
    }

    /// <summary>
    /// Miner unit tag
    /// </summary>
    public struct MinerTag : IComponentData { }

    /// <summary>
    /// Miner work state
    /// </summary>
    public enum MinerWorkState : byte
    {
        Idle = 0,
        MovingToDeposit = 1,
        Gathering = 2,
        ReturningToBase = 3
    }

    /// <summary>
    /// Miner state and behavior
    /// </summary>
    public struct MinerState : IComponentData
    {
        public Entity AssignedDeposit;  // Which deposit to mine
        public int CurrentLoad;          // Iron currently carrying
        public float GatherTimer;        // Time accumulator for gathering
        public MinerWorkState State;     // Current state
    }
}