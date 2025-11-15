using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Player.Selection
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SelectionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Handle box selection
            if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftShift))
            {
                // Box selection logic would go here
            }
            
            // Update selection visuals - using proper component references
            Entities
                .ForEach((Entity entity, in SelectableComponent selectable, in PositionComponent position) =>
                {
                    if (selectable.IsSelected)
                    {
                        // Draw selection indicator
                        DebugExtensions.DrawWireSphere(position.Position, selectable.SelectionRadius, Color.green);
                    }
                })
                .WithoutBurst() // Required for Debug.DrawLine calls
                .Run();
        }
    }
}

// Extension for Debug drawing
public static class DebugExtensions
{
    public static void DrawWireSphere(float3 position, float radius, Color color)
    {
        // Simple debug sphere visualization
        int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i * 2 * Mathf.PI) / segments;
            float angle2 = ((i + 1) * 2 * Mathf.PI) / segments;
            
            Vector3 p1 = new Vector3(position.x + Mathf.Cos(angle1) * radius, position.y, position.z + Mathf.Sin(angle1) * radius);
            Vector3 p2 = new Vector3(position.x + Mathf.Cos(angle2) * radius, position.y, position.z + Mathf.Sin(angle2) * radius);
            
            Debug.DrawLine(p1, p2, color);
        }
    }
}
