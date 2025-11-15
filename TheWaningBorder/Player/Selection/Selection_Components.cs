using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Player.Selection
{
    public struct SelectionComponent : IComponentData
    {
        public bool IsSelecting;
        public float2 SelectionStart;
        public float2 SelectionEnd;
        public int SelectedCount;
    }
    
    public struct SelectedTag : IComponentData { }
    
    public struct HoverComponent : IComponentData
    {
        public Entity HoveredEntity;
        public float HoverTime;
    }
}