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
        
        // Update existing arrow visuals
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
        
        // Create shaft (cylinder)
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(arrowRoot.transform, false);
        shaft.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
        shaft.transform.localRotation = Quaternion.Euler(0, 0, 90);
        
        // Brown wood color for shaft
        var shaftRenderer = shaft.GetComponent<MeshRenderer>();
        var shaftMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        shaftMat.color = new Color(0.4f, 0.25f, 0.1f);
        shaftRenderer.material = shaftMat;
        
        // Remove collider (we don't need it)
        Object.Destroy(shaft.GetComponent<Collider>());
        
        // Create arrowhead (cone)
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(arrowRoot.transform, false);
        head.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);
        head.transform.localRotation = Quaternion.Euler(90, 0, 0);
        head.transform.localPosition = new Vector3(0.5f, 0, 0);
        
        // Dark metal color for head
        var headRenderer = head.GetComponent<MeshRenderer>();
        var headMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        headMat.color = new Color(0.2f, 0.2f, 0.2f);
        headRenderer.material = headMat;
        
        // Remove collider
        Object.Destroy(head.GetComponent<Collider>());
        
        // Add trail renderer
        var trail = arrowRoot.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = 0.08f;
        trail.endWidth = 0.01f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(0.8f, 0.8f, 0.6f, 0.8f);
        trail.endColor = new Color(0.8f, 0.8f, 0.6f, 0f);
        trail.numCapVertices = 2;
        trail.numCornerVertices = 2;
        
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
        // This will catch arrows that are about to be destroyed
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