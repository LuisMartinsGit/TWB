using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Map.FogOfWar
{
    public struct FogRevealerComponent : IComponentData
    {
        public float RevealRadius;
        public int PlayerId;
        public bool IsActive;
    }
    
    public struct FogCellComponent : IComponentData
    {
        public int2 GridPosition;
        public byte VisibilityMask;  // Bit per player (supports up to 8 players)
        public byte ExploredMask;    // Bit per player - remains even when not visible
        public float LastUpdateTime;
    }
    
    public struct FogGridComponent : IComponentData
    {
        public int GridSizeX;
        public int GridSizeZ;
        public float CellSize;
        public float3 GridOrigin;
    }
    
    public struct FogSettingsComponent : IComponentData
    {
        public bool FogEnabled;
        public float UpdateInterval;
        public float FadeSpeed;
        public int MaxPlayers;
    }
    
    public struct FogTextureDataComponent : IComponentData
    {
        public int TextureWidth;
        public int TextureHeight;
        public float LastTextureUpdate;
    }
    
    public struct VisibilityComponent : IComponentData
    {
        public bool IsVisible;
        public bool IsExplored;
        public int VisibleToPlayerMask;
        public float LastVisibilityCheck;
    }
}
