using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Core.GameManager
{
    public struct GameStateComponent : IComponentData
    {
        public int CurrentEra;
        public float GameTime;
        public bool IsPaused;
        public GameMode Mode;
    }

    public enum GameMode
    {
        SoloVsCurse,
        FreeForAll
    }

    public struct PlayerComponent : IComponentData
    {
        public int PlayerId;
        public int TeamId;
        public bool IsHuman;
        public bool IsAlive;
        public FixedString64Bytes PlayerName;
        public FixedString64Bytes SelectedCulture;
        public int CurrentEra;
    }

    public struct ResourcesComponent : IComponentData
    {
        public int Supplies;
        public int Iron;
        public int Crystal;
        public int Veilsteel;
        public int Glow;
        public int Population;
        public int PopulationMax;
    }

    public struct OwnerComponent : IComponentData
    {
        public int PlayerId;
    }

    public struct PositionComponent : IComponentData
    {
        public float3 Position;
    }

    public struct RotationComponent : IComponentData
    {
        public quaternion Rotation;
    }

    public struct HealthComponent : IComponentData
    {
        public float CurrentHp;
        public float MaxHp;
        public float RegenRate;
    }

    public struct SelectableComponent : IComponentData
    {
        public float SelectionRadius;
        public bool IsSelected;
    }

    public struct CommandableComponent : IComponentData
    {
        public bool CanMove;
        public bool CanAttack;
        public bool CanBuild;
        public bool CanGather;
    }
}
