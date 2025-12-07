// PresentationSpawnSystem.cs
// Spawns and syncs visual GameObjects for ECS entities
// Location: Assets/Scripts/Presentation/PresentationSpawnSystem.cs

using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using TheWaningBorder.Presentation;
using TheWaningBorder.Input;

public class PresentationSpawnSystem : MonoBehaviour
{
    public static PresentationSpawnSystem Instance { get; private set; }

    // Prefab mapping: PresentationId -> Prefab path in Resources
    private static readonly Dictionary<int, string> PrefabPaths = new()
    {
        // Buildings
        { 100, "Prefabs/Buildings/Hall" },
        { 101, "Prefabs/Buildings/Barracks" },
        { 102, "Prefabs/Buildings/Hut" },
        { 103, "Prefabs/Buildings/GatherersHut" },
        
        // Units
        { 200, "Prefabs/Units/Builder" },
        { 201, "Prefabs/Units/Swordsman" },
        { 202, "Prefabs/Units/Archer" },
        { 203, "Prefabs/Units/Miner" },
        { 206, "Prefabs/Units/Scout" },
    };

    // Fallback prefabs if specific one not found
    private GameObject _fallbackUnitPrefab;
    private GameObject _fallbackBuildingPrefab;

    // Track which entities already have visuals
    private HashSet<Entity> _spawnedEntities = new();

    // Cache
    private Unity.Entities.World _world;
    private EntityManager _em;
    private EntityQuery _presentationQuery;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Create fallback primitives
        _fallbackUnitPrefab = CreateFallbackPrefab("FallbackUnit", PrimitiveType.Capsule, 0.5f);
        _fallbackBuildingPrefab = CreateFallbackPrefab("FallbackBuilding", PrimitiveType.Cube, 2f);
    }

    void Start()
    {
        _world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated)
        {
            _em = _world.EntityManager;
            _presentationQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<PresentationId>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }
    }

    void Update()
    {
        if (_world == null || !_world.IsCreated) return;

        SpawnMissingVisuals();
        SyncTransforms();
    }

    private void SpawnMissingVisuals()
    {
        if (_presentationQuery == null) return;

        var entities = _presentationQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        var presentations = _presentationQuery.ToComponentDataArray<PresentationId>(Unity.Collections.Allocator.Temp);
        var transforms = _presentationQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];

            // Skip if already spawned
            if (_spawnedEntities.Contains(entity)) continue;

            // Skip if EntityViewManager already has it
            if (EntityViewManager.Instance != null &&
                EntityViewManager.Instance.TryGetView(entity, out _))
            {
                _spawnedEntities.Add(entity);
                continue;
            }

            var presentationId = presentations[i].Id;
            var transform = transforms[i];

            // Spawn the visual
            var go = SpawnVisual(entity, presentationId, transform);
            if (go != null)
            {
                // Register with EntityViewManager
                if (EntityViewManager.Instance != null)
                    EntityViewManager.Instance.RegisterView(entity, go);

                _spawnedEntities.Add(entity);

                Debug.Log($"[PresentationSpawnSystem] Spawned visual for entity {entity.Index} (PresentationId: {presentationId})");
            }
        }

        entities.Dispose();
        presentations.Dispose();
        transforms.Dispose();
    }

    private GameObject SpawnVisual(Entity entity, int presentationId, LocalTransform transform)
    {
        GameObject prefab = null;

        // Try to load specific prefab
        if (PrefabPaths.TryGetValue(presentationId, out string path))
        {
            prefab = Resources.Load<GameObject>(path);
        }

        // Fallback based on ID range
        if (prefab == null)
        {
            if (presentationId >= 200)
                prefab = _fallbackUnitPrefab;
            else
                prefab = _fallbackBuildingPrefab;

            Debug.LogWarning($"[PresentationSpawnSystem] No prefab for PresentationId {presentationId}, using fallback");
        }

        if (prefab == null) return null;

        // Get position and adjust Y to terrain height
        Vector3 pos = transform.Position;
        pos.y = GetTerrainHeight(pos.x, pos.z);

        var go = Instantiate(prefab);
        go.name = $"Entity_{entity.Index}_{presentationId}";
        go.transform.position = pos;
        go.transform.rotation = transform.Rotation;
        go.transform.localScale = Vector3.one * transform.Scale;

        // Add EntityReference for raycasting/selection
        var entityRef = go.GetComponent<EntityReference>();
        if (entityRef == null)
            entityRef = go.AddComponent<EntityReference>();
        entityRef.Entity = entity;

        return go;
    }
private float GetTerrainHeight(float x, float z)
{
    // Find the terrain with actual data (ProcTerrain)
    Terrain terrain = null;
    
    foreach (var t in Terrain.activeTerrains)
    {
        if (t.terrainData != null)
        {
            terrain = t;
            break;
        }
    }
    
    // Also try finding by name as backup
    if (terrain == null)
    {
        var go = GameObject.Find("ProcTerrain");
        if (go != null)
            terrain = go.GetComponent<Terrain>();
    }
    
    if (terrain != null && terrain.terrainData != null)
    {
        float height = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
        return height;
    }
    
    // Fallback: raycast
    if (Physics.Raycast(new Vector3(x, 1000f, z), Vector3.down, out RaycastHit hit, 2000f))
    {
        return hit.point.y;
    }
    
    return 0f;
}

    private void ApplyFactionColor(GameObject go, Entity entity)
    {
        if (!_em.HasComponent<FactionTag>(entity)) return;

        var faction = _em.GetComponentData<FactionTag>(entity).Value;
        var color = GetFactionColor(faction);

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_Color"))
                    mat.color = color;
            }
        }
    }

    private void SyncTransforms()
    {
        if (EntityViewManager.Instance == null) return;

        var entities = _presentationQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        var transforms = _presentationQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (EntityViewManager.Instance.TryGetView(entities[i], out var go) && go != null)
            {
                var pos = (Vector3)transforms[i].Position;
                pos.y = GetTerrainHeight(pos.x, pos.z);
                go.transform.position = pos;
                go.transform.rotation = transforms[i].Rotation;
            }
        }

        entities.Dispose();
        transforms.Dispose();
    }
    private GameObject CreateFallbackPrefab(string name, PrimitiveType type, float scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.localScale = Vector3.one * scale;
        go.SetActive(false); // Hide the template
        DontDestroyOnLoad(go);
        return go;
    }

    private Color GetFactionColor(Faction faction)
    {
        return faction switch
        {
            Faction.Blue => new Color(0.3f, 0.5f, 1f),
            Faction.Red => new Color(1f, 0.3f, 0.3f),
            Faction.Green => new Color(0.3f, 1f, 0.3f),
            Faction.Yellow => new Color(1f, 1f, 0.3f),
            Faction.Purple => new Color(0.8f, 0.3f, 1f),
            Faction.Orange => new Color(1f, 0.6f, 0.2f),
            Faction.Teal => new Color(0.2f, 0.8f, 0.8f),
            Faction.White => new Color(0.9f, 0.9f, 0.9f),
            _ => Color.gray
        };
    }

    void OnDestroy()
    {
        // Clean up fallback prefabs
        if (_fallbackUnitPrefab != null) Destroy(_fallbackUnitPrefab);
        if (_fallbackBuildingPrefab != null) Destroy(_fallbackBuildingPrefab);

        if (Instance == this) Instance = null;
    }
}