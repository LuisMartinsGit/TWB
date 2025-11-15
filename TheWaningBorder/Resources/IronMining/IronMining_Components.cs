using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Resources.IronMining
{
    public struct IronDepositComponent : IComponentData
    {
        public float3 Position;
        public int RemainingOre;
        public int MaxOre;
        public Entity ClaimedByMiner;
        public int PatchId;
        public bool IsExhausted;

        public float MiningRadius { get; internal set; }

    }
    
    public struct IronPatchComponent : IComponentData
    {
        public float3 CenterPosition;
        public float Radius;
        public int DepositCount;
        public int PatchId;
        public bool IsGuaranteedPatch; // Near starting position
    }
    
    public enum MiningState
    {
        Idle,
        MovingToDeposit,
        Mining,
        Returning,
        Depositing
    }
    
    public struct MiningStateComponent : IComponentData
    {
        public Entity TargetDeposit;
        public Entity ReturnBuilding;
        public int CarriedOre;
        public float MiningProgress;
        public MiningState State;
        public float MiningSpeed;
        public int MaxCarryCapacity;
        public float TimeAtDeposit;

        public Entity TargetDropoff { get; internal set; }

    }
    
    public struct MinerTag : IComponentData
    {
        public int PlayerId;
    }
    
    public struct ResourceDepositTag : IComponentData
    {
        public ResourceType Type;
    }
    
    public enum ResourceType
    {
        Iron,
        Crystal,
        Veilsteel,
        Glow
    }
    
    public struct ResourceDropOffPointComponent : IComponentData
    {
        public bool CanReceiveIron;
        public bool CanReceiveCrystal;
        public bool CanReceiveVeilsteel;
        public bool CanReceiveGlow;
        public float3 DropOffPosition;
        public int OwnerId;
    }
}
