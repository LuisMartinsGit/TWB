using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;               // float3
using Unity.Transforms;
using TheWaningBorder.Humans;         // (we keep using your GatherersHut factory if you prefer)
                                      // but this file can spawn others directly too
using TheWaningBorder.Economy;

public class BuilderCommandPanel : MonoBehaviour
{
    // ======= Shared state used by RTSInput =======
    public static bool PanelVisible;
    public static Rect PanelRectScreenBL;
    public static bool IsPlacingBuilding;
    public static bool SuppressClicksThisFrame;

    private World _world;
    private EntityManager _em;

    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float yOffset = 0f;

    // Current placement GO preview (mouse-follow)
    private GameObject _placingInstance;

    // Which building are we placing
    public enum BuildType { Hut, GatherersHut, Barracks, Shrine, Vault, Keep }
    private BuildType _currentBuild = BuildType.Hut;

    // Prefab previews
    private GameObject _prefabGatherersHut;
    private GameObject _prefabHut;
    private GameObject _prefabBarracks;
    private GameObject _prefabShrine;
    private GameObject _prefabVault;
    private GameObject _prefabKeep;

    // Panel sizing
    public const float PanelWidth = 300f;
    public const float PanelHeight = 170f;
    private RectOffset _padding;

    // Icons & styles
    private Texture2D _iconGatherersHut, _iconHut, _iconBarracks, _iconShrine, _iconVault, _iconKeep;
    private GUIStyle _iconBtn, _caption;
    private static string BuildId(BuildType t) => t switch
    {
        BuildType.Hut           => "Hut",
        BuildType.GatherersHut  => "GatherersHut",
        BuildType.Barracks      => "Barracks",
        BuildType.Shrine        => "Shrine",
        BuildType.Vault         => "Vault",
        BuildType.Keep          => "Keep",
        _ => "Hut"
    };

    private static string CostText(in Cost c)
    {
        // compact human-readable cost line
        System.Text.StringBuilder sb = new System.Text.StringBuilder(48);
        void add(string n, int v) { if (v>0) { if (sb.Length>0) sb.Append("  "); sb.Append(n).Append(' ').Append(v); } }
        add("S", c.Supplies); add("Fe", c.Iron); add("Cr", c.Crystal); add("Vs", c.Veilsteel); add("Gl", c.Glow);
        return sb.Length==0 ? "Free" : sb.ToString();
    }
    void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        _padding = new RectOffset(10, 10, 10, 10);

        // ---- Load preview prefabs (Resources paths; omit ".prefab") ----
        _prefabGatherersHut = Resources.Load<GameObject>("Prefabs/Buildings/GatherersHut");
        _prefabHut = Resources.Load<GameObject>("Prefabs/Buildings/Hut");
        _prefabBarracks = Resources.Load<GameObject>("Prefabs/Buildings/Barracks");
        _prefabShrine = Resources.Load<GameObject>("Prefabs/Buildings/TempleOfRidan"); // "Shrine to Ahridan"
        _prefabVault = Resources.Load<GameObject>("Prefabs/Runai/Buildings/VaultOfAlmierra");
        _prefabKeep = Resources.Load<GameObject>("Prefabs/Feraldis/Buildings/FiendstoneKeep");

        if (_prefabGatherersHut == null) Debug.LogWarning("[BuilderMenu] Missing preview: Prefabs/Buildings/GatherersHut");
        if (_prefabHut == null) Debug.LogWarning("[BuilderMenu] Missing preview: Prefabs/Buildings/Hut");
        if (_prefabBarracks == null) Debug.LogWarning("[BuilderMenu] Missing preview: Prefabs/Buildings/Barracks");
        if (_prefabShrine == null) Debug.LogWarning("[BuilderMenu] Missing preview: Prefabs/Buildings/TempleOfRidan");
        if (_prefabVault == null) Debug.LogWarning("[BuilderMenu] Missing preview: Prefabs/Runai/Buildings/VaultOfAlmierra");
        if (_prefabKeep == null) Debug.LogWarning("[BuilderMenu] Missing preview: Prefabs/Feraldis/Buildings/FiendstoneKeep");

        // ---- Load icons (textures) ----
        _iconGatherersHut = Resources.Load<Texture2D>("UI/Icons/GatherersHut");
        _iconHut = Resources.Load<Texture2D>("UI/Icons/Hut");
        _iconBarracks = Resources.Load<Texture2D>("UI/Icons/Barracks");
        _iconShrine = Resources.Load<Texture2D>("UI/Icons/Shrine");
        _iconVault = Resources.Load<Texture2D>("UI/Icons/Vault");
        _iconKeep = Resources.Load<Texture2D>("UI/Icons/FiendstoneKeep");

        if (_iconGatherersHut == null) Debug.LogWarning("[BuilderMenu] Missing icon: UI/Icons/GatherersHut");
        if (_iconGatherersHut == null) Debug.LogWarning("[BuilderMenu] Missing icon: UI/Icons/Hut");
        if (_iconBarracks == null) Debug.LogWarning("[BuilderMenu] Missing icon: UI/Icons/Barracks");
        if (_iconShrine == null) Debug.LogWarning("[BuilderMenu] Missing icon: UI/Icons/Shrine");
        if (_iconVault == null) Debug.LogWarning("[BuilderMenu] Missing icon: UI/Icons/Vault");
        if (_iconKeep == null) Debug.LogWarning("[BuilderMenu] Missing icon: UI/Icons/FiendstoneKeep");
    }

    void Update()
    {
        PanelRectScreenBL = new Rect(10f, 10f, PanelWidth, PanelHeight);

        if (IsPlacingBuilding)
        {
            if (_placingInstance == null) { CancelPlacement(); return; }

            if (TryGetMouseWorld(out Vector3 p))
                _placingInstance.transform.position = p + Vector3.up * yOffset;

            // Confirm -> spawn ECS, destroy preview
            if (Input.GetMouseButtonDown(0))
            {
                var pos = _placingInstance.transform.position;
                SpawnSelectedBuilding((float3)pos);
                CancelPlacementPreviewOnly();
                SuppressClicksThisFrame = true;
            }

            // Cancel
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
                SuppressClicksThisFrame = true;
            }
        }
    }


    void StartPlacement()
    {

        Debug.Log("placing building");

        if (IsPlacingBuilding) return;

        var preview = GetPreviewFor(_currentBuild);
        if (preview == null)
        {

            return;
        }

        _placingInstance = Instantiate(preview);
        _placingInstance.name = "[Preview] " + preview.name;

        foreach (var c in _placingInstance.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var r in _placingInstance.GetComponentsInChildren<Renderer>())
        {
            if (r.material.HasProperty("_Color"))
            {
                var col = r.material.color; col.a = 0.6f; r.material.color = col;
            }
        }

        if (TryGetMouseWorld(out Vector3 p))
            _placingInstance.transform.position = p + Vector3.up * yOffset;

        IsPlacingBuilding = true;

    }

    GameObject GetPreviewFor(BuildType t)
    {
        switch (t)
        {
            case BuildType.Hut: return _prefabHut;
            case BuildType.GatherersHut: return _prefabGatherersHut;
            case BuildType.Barracks: return _prefabBarracks;
            case BuildType.Shrine: return _prefabShrine;
            case BuildType.Vault: return _prefabVault;
            case BuildType.Keep: return _prefabKeep;
            default: return null;
        }
    }

    // Spawn ECS entity with gameplay components
    void SpawnSelectedBuilding(float3 pos)
    {
        if (_em.Equals(default(EntityManager)))
            _em = (_world ?? World.DefaultGameObjectInjectionWorld).EntityManager;

        var fac = GetSelectedFactionOrDefault();

        var id = BuildId(_currentBuild);
        if (!BuildCosts.TryGet(id, out var cost)) cost = default;

        if (!FactionEconomy.Spend(_em, fac, cost))
        {
            return;
        }

        switch (_currentBuild)
        {
            case BuildType.Hut:
                // Use your existing factory
                var hut = Hut.Create(_em, pos, fac);
                // NEW: Add population provider (10 population)
                if (!_em.HasComponent<PopulationProvider>(hut))
                {
                    _em.AddComponentData(hut, new PopulationProvider { Amount = 10 });
                }
                break;

            case BuildType.GatherersHut:
                // Use your existing factory (no population)
                GatherersHut.Create(_em, pos, fac);
                break;

            case BuildType.Barracks:

                Barracks.Create(_em,pos,fac);
                CreateSimpleBuilding(_em, pos, fac,
                    buildingId: "Barracks",
                    defaultHp: 900f, defaultLoS: 12f, defaultRadius: 1.8f,
                    addSpecificTag: (e) =>
                    {
                        if (!_em.HasComponent<BarracksTag>(e)) _em.AddComponent<BarracksTag>(e);
                        if (!_em.HasComponent<TrainingState>(e))
                            _em.AddComponentData(e, new TrainingState { Busy = 0, Remaining = 0 });
                        if (!_em.HasComponent<TrainQueueItem>(e))
                            _em.AddBuffer<TrainQueueItem>(e);
                    });
                break;

            case BuildType.Shrine: // Temple of Ridan
                CreateSimpleBuilding(_em, pos, fac,
                    buildingId: "TempleOfRidan",
                    defaultHp: 800f, defaultLoS: 16f, defaultRadius: 1.8f,
                    addSpecificTag: (e) => { if (!_em.HasComponent<TempleTag>(e)) _em.AddComponent<TempleTag>(e); });
                break;

            case BuildType.Vault: // Vault of Almierra
                CreateSimpleBuilding(_em, pos, fac,
                    buildingId: "VaultOfAlmierra",
                    defaultHp: 1200f, defaultLoS: 14f, defaultRadius: 2.0f,
                    addSpecificTag: (e) => { if (!_em.HasComponent<VaultTag>(e)) _em.AddComponent<VaultTag>(e); });
                break;

            case BuildType.Keep: // Fiendstone Keep (Feraldis capital)
                CreateSimpleBuilding(_em, pos, fac,
                    buildingId: "FiendstoneKeep",
                    defaultHp: 2000f, defaultLoS: 18f, defaultRadius: 2.4f,
                    addSpecificTag: (e) =>
                    {
                        // Mark as base so EntityViewManager can choose the culture capital model
                        var bt = _em.HasComponent<BuildingTag>(e) 
                            ? _em.GetComponentData<BuildingTag>(e) 
                            : new BuildingTag();
                        bt.IsBase = 1;
                        if (!_em.HasComponent<BuildingTag>(e))
                            _em.AddComponentData(e, bt);
                        else
                            _em.SetComponentData(e, bt);
                    });
                break;
        }
    }

    // Generic spawner that reads TechTreeDB if available; else uses provided defaults
    void CreateSimpleBuilding(EntityManager em, float3 pos, Faction fac,
                              string buildingId, float defaultHp, float defaultLoS, float defaultRadius,
                              System.Action<Entity> addSpecificTag)
    {
        float hp = defaultHp, los = defaultLoS, radius = defaultRadius;

        if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding(buildingId, out var bdef))
        {
            if (bdef.hp > 0) hp = bdef.hp;
            if (bdef.lineOfSight > 0) los = bdef.lineOfSight;
            if (bdef.radius > 0) radius = bdef.radius;
        }

        var e = em.CreateEntity(
            typeof(PresentationId),
            typeof(LocalTransform),
            typeof(FactionTag),
            typeof(BuildingTag),
            typeof(Health),
            typeof(LineOfSight),
            typeof(Radius)
        );

        // PresentationId is optional; you can map IDs if you want
        // Different IDs per type (in case you later map by PID in EntityViewManager)
        int pid = buildingId switch
        {
            "Barracks" => 510,
            "TempleOfRidan" => 520,
            "VaultOfAlmierra" => 530,
            "FiendstoneKeep" => 540,
            _ => 505
        };

        em.SetComponentData(e, new PresentationId { Id = pid });
        em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
        em.SetComponentData(e, new FactionTag { Value = fac });
        em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
        em.SetComponentData(e, new LineOfSight { Radius = los });
        em.SetComponentData(e, new Radius { Value = radius });

        // Add the specific tag the resolver looks for
        addSpecificTag?.Invoke(e);
    }

    // --------- placement cancel helpers ----------
    void CancelPlacementPreviewOnly()
    {
        if (_placingInstance != null) Destroy(_placingInstance);
        _placingInstance = null;
        IsPlacingBuilding = false;
    }

    void CancelPlacement()
    {
        CancelPlacementPreviewOnly();
    }

    // --------- shared helpers ----------
    Faction GetSelectedFactionOrDefault()
    {
        if (TryGetFirstSelectedBuilder(out var builder) && _em.Exists(builder) && _em.HasComponent<FactionTag>(builder))
            return _em.GetComponentData<FactionTag>(builder).Value;

        return GameSettings.LocalPlayerFaction; // fallback if nothing selected
    }

    bool TryGetFirstSelectedBuilder(out Entity builder)
    {
        builder = Entity.Null;
        var sel = RTSInput.CurrentSelection;
        if (sel == null || sel.Count == 0) return false;
        if (_em.Equals(default(EntityManager))) return false;

        for (int i = 0; i < sel.Count; i++)
        {
            var e = sel[i];
            if (!_em.Exists(e)) continue;
            if (_em.HasComponent<CanBuild>(e) && _em.GetComponentData<CanBuild>(e).Value)
            {
                builder = e;
                return true;
            }
        }
        return false;
    }

    bool TryGetMouseWorld(out Vector3 world)
    {
        world = default;
        var cam = Camera.main;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 10000f, placementMask, QueryTriggerInteraction.Ignore))
        { world = hit.point; return true; }

        var terrain = Terrain.activeTerrain;
        if (terrain && terrain.terrainData)
        {
            Plane tp = new Plane(Vector3.up, new Vector3(0, terrain.transform.position.y, 0));
            if (tp.Raycast(ray, out float t))
            {
                var p = ray.GetPoint(t);
                float ty = terrain.SampleHeight(p) + terrain.transform.position.y;
                world = new Vector3(p.x, ty, p.z);
                return true;
            }
        }

        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float d2))
        {
            var p = ray.GetPoint(d2);
            world = new Vector3(p.x, 0f, p.z);
            return true;
        }
        return false;
    }

    public static bool IsPointerOverPanel()
    {
        if (!PanelVisible) return false;
        return PanelRectScreenBL.Contains(Input.mousePosition);
    }

public static void TriggerBuildingPlacement(string buildingId)
{
    var instance = FindObjectOfType<BuilderCommandPanel>();
    if (instance == null) return;
    
    instance._currentBuild = buildingId switch
    {
        "Hut" => BuildType.Hut,
        "GatherersHut" => BuildType.GatherersHut,
        "Barracks" => BuildType.Barracks,
        "TempleOfRidan" => BuildType.Shrine,
        "VaultOfAlmierra" => BuildType.Vault,
        "FiendstoneKeep" => BuildType.Keep,
        _ => BuildType.Hut
    };
    
    instance.StartPlacement();
    SuppressClicksThisFrame = true;
}
}
