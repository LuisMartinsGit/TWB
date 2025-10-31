using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Add this component to arrow entities
public struct ArrowVisual : IComponentData
{
    public Entity VisualEntity;
}

public struct ArrowVisualData : IComponentData
{
    public int GameObjectInstanceID;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class ArrowVisualSystem : SystemBase
{
    private EntityQuery _newArrowsQuery;
    
    protected override void OnCreate()
    {
        // Query for arrows that don't have visuals yet
        _newArrowsQuery = GetEntityQuery(
            ComponentType.ReadOnly<ArrowProjectile>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<ArrowVisual>()
        );
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        // Create visuals for new arrows
        Entities
            .WithStoreEntityQueryInField(ref _newArrowsQuery)
            .WithoutBurst()
            .ForEach((Entity entity, in LocalTransform transform, in ArrowProjectile arrow) =>
            {
                // Create the arrow visual GameObject
                var arrowGO = CreateArrowVisual(transform.Position, transform.Rotation);
                
                // Add component to track this visual
                ecb.AddComponent(entity, new ArrowVisual 
                { 
                    VisualEntity = Entity.Null 
                });
                
                ecb.AddComponent(entity, new ArrowVisualData 
                { 
                    GameObjectInstanceID = arrowGO.GetInstanceID() 
                });
            }).Run();
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
        
        // Update existing arrow visuals - both position and rotation
        Entities
            .WithoutBurst()
            .ForEach((in LocalTransform transform, in ArrowVisualData visualData) =>
            {
                var go = Resources.InstanceIDToObject(visualData.GameObjectInstanceID) as GameObject;
                if (go != null)
                {
                    go.transform.position = transform.Position;
                    go.transform.rotation = transform.Rotation;
                }
            }).Run();
    }

    private GameObject CreateArrowVisual(float3 position, quaternion rotation)
    {
        var arrowRoot = new GameObject("Arrow");
        arrowRoot.transform.position = position;
        arrowRoot.transform.rotation = rotation;
        
        // Create shaft (elongated cylinder pointing forward along X-axis)
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(arrowRoot.transform, false);
        
        // Make it arrow-shaped: thin and long
        // Scale: X = length, Y/Z = thickness
        shaft.transform.localScale = new Vector3(0.03f, 0.6f, 0.03f);
        
        // Rotate cylinder to point along X-axis (forward)
        shaft.transform.localRotation = Quaternion.Euler(0, 0, 90);
        shaft.transform.localPosition = new Vector3(0, 0, 0);
        
        // Brown wood color for shaft
        var shaftRenderer = shaft.GetComponent<MeshRenderer>();
        var shaftMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        shaftMat.color = new Color(0.4f, 0.25f, 0.1f);
        shaftRenderer.material = shaftMat;
        
        // Remove collider
        Object.Destroy(shaft.GetComponent<Collider>());
        
        // Create arrowhead (cone pointing forward)
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(arrowRoot.transform, false);
        
        // Small, pointed cone at the front
        head.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
        
        // Rotate cone to point along X-axis and position at front
        head.transform.localRotation = Quaternion.Euler(0, 0, -90);
        head.transform.localPosition = new Vector3(0.75f, 0, 0);
        
        // Dark metal color for arrowhead
        var headRenderer = head.GetComponent<MeshRenderer>();
        var headMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        headMat.color = new Color(0.2f, 0.2f, 0.2f);
        headRenderer.material = headMat;
        
        // Remove collider
        Object.Destroy(head.GetComponent<Collider>());
        
        // Create fletching (small spheres at back for feathers)
        for (int i = 0; i < 3; i++)
        {
            var fletch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fletch.name = $"Fletching{i}";
            fletch.transform.SetParent(arrowRoot.transform, false);
            fletch.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            
            // Position at back of arrow in a triangular pattern
            float angle = i * 120f * Mathf.Deg2Rad;
            float offset = 0.05f;
            fletch.transform.localPosition = new Vector3(
                -0.5f, // Back of arrow
                Mathf.Cos(angle) * offset,
                Mathf.Sin(angle) * offset
            );
            
            // Light color for feathers
            var fletchRenderer = fletch.GetComponent<MeshRenderer>();
            var fletchMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fletchMat.color = new Color(0.9f, 0.9f, 0.8f);
            fletchRenderer.material = fletchMat;
            
            // Remove collider
            Object.Destroy(fletch.GetComponent<Collider>());
        }
        
        return arrowRoot;
    }
}

// System to clean up arrow visuals when arrows are destroyed
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(ArrowVisualSystem))]
public partial class ArrowVisualCleanupSystem : SystemBase
{
    private EntityQuery _destroyedArrowsQuery;
    
    protected override void OnCreate()
    {
        RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    protected override void OnUpdate()
    {
        // Clean up visuals for arrows that no longer exist
        Entities
            .WithNone<ArrowProjectile>()
            .WithoutBurst()
            .ForEach((Entity entity, in ArrowVisualData visualData) =>
            {
                var go = Resources.InstanceIDToObject(visualData.GameObjectInstanceID) as GameObject;
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }).Run();
    }
}