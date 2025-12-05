// File: Assets/Scripts/UI/Panels/BuilderCommandPanel.cs
// Building placement UI with preview and cost checking

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Economy;
using TheWaningBorder.Entities;
using EntityWorld = Unity.Entities.World;

namespace TheWaningBorder.UI.Panels
{
    /// <summary>
    /// Handles building placement preview and spawning.
    /// Works with EntityActionPanel for UI integration.
    /// </summary>
    public class BuilderCommandPanel : MonoBehaviour
    {
        // Shared state for RTSInput and other systems
        public static bool PanelVisible;
        public static Rect PanelRectScreenBL;
        public static bool IsPlacingBuilding;
        public static bool SuppressClicksThisFrame;

        private EntityWorld _world;
        private EntityManager _em;

        [Header("Placement")]
        [SerializeField] private LayerMask placementMask = ~0;
        [SerializeField] private float yOffset = 0f;

        // Current placement preview
        private GameObject _placingInstance;

        // Build type
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

        void Awake()
        {
            _world = EntityWorld.DefaultGameObjectInjectionWorld;
            _padding = new RectOffset(10, 10, 10, 10);

            // Load preview prefabs
            _prefabGatherersHut = Resources.Load<GameObject>("Prefabs/Buildings/GatherersHut");
            _prefabHut = Resources.Load<GameObject>("Prefabs/Buildings/Hut");
            _prefabBarracks = Resources.Load<GameObject>("Prefabs/Buildings/Barracks");
            _prefabShrine = Resources.Load<GameObject>("Prefabs/Buildings/TempleOfRidan");
            _prefabVault = Resources.Load<GameObject>("Prefabs/Runai/Buildings/VaultOfAlmierra");
            _prefabKeep = Resources.Load<GameObject>("Prefabs/Feraldis/Buildings/FiendstoneKeep");
        }

        void Update()
        {
            PanelRectScreenBL = new Rect(10f, 10f, PanelWidth, PanelHeight);

            if (IsPlacingBuilding)
            {
                if (_placingInstance == null) { CancelPlacement(); return; }

                if (TryGetMouseWorld(out Vector3 p))
                    _placingInstance.transform.position = p + Vector3.up * yOffset;

                // Confirm placement
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
                }
            }
        }

        /// <summary>
        /// Start building placement mode for a specific building ID.
        /// Called from EntityActionPanel.
        /// </summary>
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

        public void StartPlacement()
        {
            CancelPlacement();

            var prefab = _currentBuild switch
            {
                BuildType.GatherersHut => _prefabGatherersHut,
                BuildType.Hut => _prefabHut,
                BuildType.Barracks => _prefabBarracks,
                BuildType.Shrine => _prefabShrine,
                BuildType.Vault => _prefabVault,
                BuildType.Keep => _prefabKeep,
                _ => _prefabHut
            };

            if (prefab != null)
            {
                _placingInstance = Instantiate(prefab);
                _placingInstance.name = "PlacementPreview";

                // Disable colliders on preview
                foreach (var col in _placingInstance.GetComponentsInChildren<Collider>())
                    col.enabled = false;

                // Make semi-transparent
                foreach (var renderer in _placingInstance.GetComponentsInChildren<Renderer>())
                {
                    foreach (var mat in renderer.materials)
                    {
                        if (mat.HasProperty("_Color"))
                        {
                            var c = mat.color;
                            c.a = 0.5f;
                            mat.color = c;
                        }
                    }
                }
            }

            IsPlacingBuilding = true;
        }

        public void CancelPlacement()
        {
            if (_placingInstance != null) Destroy(_placingInstance);
            _placingInstance = null;
            IsPlacingBuilding = false;
        }

        private void CancelPlacementPreviewOnly()
        {
            if (_placingInstance != null) Destroy(_placingInstance);
            _placingInstance = null;
            IsPlacingBuilding = false;
        }

        private void SpawnSelectedBuilding(float3 pos)
        {
            _em = (_world ?? EntityWorld.DefaultGameObjectInjectionWorld).EntityManager;

            var fac = GetSelectedFactionOrDefault();

            var id = BuildId(_currentBuild);
            if (!BuildCosts.TryGet(id, out var cost)) cost = default;

            if (!FactionEconomy.Spend(_em, fac, cost))
            {
                Debug.LogWarning($"Cannot afford {id}");
                return;
            }

            switch (_currentBuild)
            {
                case BuildType.Hut:
                    var hut = Hut.Create(_em, pos, fac);
                    if (!_em.HasComponent<PopulationProvider>(hut))
                        _em.AddComponentData(hut, new PopulationProvider { Amount = 10 });
                    break;

                case BuildType.GatherersHut:
                    GatherersHut.Create(_em, pos, fac);
                    break;

                case BuildType.Barracks:
                    Barracks.Create(_em, pos, fac);
                    break;

                case BuildType.Shrine:
                    BuildingFactory.Create(_em, "TempleOfRidan", pos, fac);
                    break;

                case BuildType.Vault:
                    BuildingFactory.Create(_em, "VaultOfAlmierra", pos, fac);
                    break;

                case BuildType.Keep:
                    BuildingFactory.Create(_em, "FiendstoneKeep", pos, fac);
                    break;
            }
        }

        private Faction GetSelectedFactionOrDefault()
        {
            var sel = RTSInput.CurrentSelection;
            if (sel != null && sel.Count > 0)
            {
                var e = sel[0];
                if (_em.Exists(e) && _em.HasComponent<FactionTag>(e))
                    return _em.GetComponentData<FactionTag>(e).Value;
            }
            return GameSettings.LocalPlayerFaction;
        }

        private static string BuildId(BuildType t) => t switch
        {
            BuildType.Hut => "Hut",
            BuildType.GatherersHut => "GatherersHut",
            BuildType.Barracks => "Barracks",
            BuildType.Shrine => "TempleOfRidan",
            BuildType.Vault => "VaultOfAlmierra",
            BuildType.Keep => "FiendstoneKeep",
            _ => "Hut"
        };

        private bool TryGetMouseWorld(out Vector3 world)
        {
            world = default;
            var cam = Camera.main;
            if (!cam) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hit, 10000f, placementMask, QueryTriggerInteraction.Ignore))
            {
                world = hit.point;
                return true;
            }

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
    }
}