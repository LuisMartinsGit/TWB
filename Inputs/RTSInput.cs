// RTSInput.cs - Refactored to use CommandGateway
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using TheWaningBorder.Core;

public class RTSInput : MonoBehaviour
{
    [SerializeField] private LayerMask clickMask = ~0;

    private World _world;
    private EntityManager _em;
    public static List<Entity> CurrentSelection { get; private set; }
    public static Entity CurrentHover { get; private set; }
    private readonly List<Entity> _selection = new();

    private bool _showHelp = true;
    private bool _rallyMode = false;

    // Drag-select state (screen space, origin = bottom-left)
    private Vector3 _dragStartScreen;
    private bool _isDragging;
    private Rect _dragScreenRect;

    void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated) _em = _world.EntityManager;
        CurrentSelection = _selection;
    }

    void Update()
    {
        if (_world == null || !_world.IsCreated) return;

        // 🛡️ One-frame suppression (e.g., after clicking a GUI button or confirming placement)
        if (BuilderCommandPanel.SuppressClicksThisFrame)
        {
            BuilderCommandPanel.SuppressClicksThisFrame = false;
            _isDragging = false;
            return;
        }

        // 🛡️ Ignore input if mouse is over ANY panel this frame
        if (EntityActionPanel.IsPointerOver() || EntityInfoPanel.IsPointerOver())
        {
            _isDragging = false;
            return;
        }

        // 🛡️ While placing a building, block normal selection/commands
        if (BuilderCommandPanel.IsPlacingBuilding)
        {
            _isDragging = false;
            return;
        }

        CleanSelection();

        if (Input.GetKeyDown(KeyCode.Escape)) { _selection.Clear(); _rallyMode = false; }
        if (Input.GetKeyDown(KeyCode.R)) _rallyMode = !_rallyMode;

        HandleSelection();
        HandleRightClick();

        // Hover
        var hovered = RaycastPickEntity();
        CurrentHover = (_em.Exists(hovered)) ? hovered : Entity.Null;
    }

    // ---------------- Selection ----------------
    void HandleSelection()
    {
        // Safety: don't start/select while pointer is over ANY UI panel
        if (EntityInfoPanel.IsPointerOver() || EntityActionPanel.IsPointerOver()) return;

        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            _dragStartScreen = Input.mousePosition;
            _dragScreenRect = new Rect(_dragStartScreen.x, _dragStartScreen.y, 0, 0);
        }

        if (_isDragging)
        {
            _dragScreenRect = MakeScreenRect(_dragStartScreen, Input.mousePosition);
        }

        if (_isDragging && Input.GetMouseButtonUp(0))
        {
            _isDragging = false;

            if (_dragScreenRect.width < 4f || _dragScreenRect.height < 4f)
            {
                var e = RaycastPickEntity();
                _selection.Clear();
                if (e != Entity.Null && _em.Exists(e) &&
                    _em.HasComponent<FactionTag>(e) &&
                    _em.GetComponentData<FactionTag>(e).Value == Faction.Blue &&
                    (_em.HasComponent<UnitTag>(e) || _em.HasComponent<BuildingTag>(e)))
                {
                    _selection.Add(e);
                }
            }
            else
            {
                BoxSelectEntities(_dragScreenRect);
            }
        }
    }

    void BoxSelectEntities(Rect screenRect)
    {
        var cam = Camera.main;
        if (!cam) return;

        _selection.Clear();

        var q = _em.CreateEntityQuery(typeof(LocalTransform), typeof(FactionTag));
        var ents = q.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < ents.Length; i++)
        {
            var e = ents[i];
            if (!_em.Exists(e)) continue;

            if (!_em.HasComponent<FactionTag>(e) ||
                _em.GetComponentData<FactionTag>(e).Value != Faction.Blue)
                continue;

            if (!_em.HasComponent<UnitTag>(e) && !_em.HasComponent<BuildingTag>(e))
                continue;

            Bounds wb = ComputeEntityWorldBounds(e);
            Rect rb = ProjectWorldBoundsToScreenRect(cam, wb);

            if (rb.width <= 0f || rb.height <= 0f) continue;
            if (screenRect.Overlaps(rb, true))
                _selection.Add(e);
        }

        ents.Dispose();
    }

    // ---------------- Right-click commands (REFACTORED FOR COMMANDGATEWAY) ----------------
    void HandleRightClick()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        CleanSelection();
        if (_selection.Count == 0) return;

        if (!TryGetClickPoint(out float3 clickWorld)) return;

        // Special mode: setting rally point for selected buildings
        if (IsSetRallyPointMode())
        {
            for (int i = 0; i < _selection.Count; i++)
            {
                var e = _selection[i];
                if (!_em.Exists(e)) continue;
                if (!_em.HasComponent<BuildingTag>(e)) continue;

                if (!_em.HasComponent<RallyPoint>(e)) _em.AddComponent<RallyPoint>(e);
                _em.SetComponentData(e, new RallyPoint { Position = clickWorld, Has = 1 });
            }
            return;
        }

        // Check what the user clicked on
        var target = RaycastPickEntity();
        
        // Determine target type
        TargetType targetType = DetermineTargetType(target);
        
        // Determine selected unit capabilities
        UnitCapabilities capabilities = DetermineCapabilities();

        // Route to appropriate command based on target and capabilities
        switch (targetType)
        {
            case TargetType.Enemy:
                // Attack command (if units can attack)
                if (capabilities.CanAttack)
                {
                    for (int i = 0; i < _selection.Count; i++)
                    {
                        var e = _selection[i];
                        if (!_em.Exists(e)) continue;
                        if (_em.HasComponent<BuildingTag>(e)) continue;

                        CommandGateway.IssueAttack(_em, e, target);
                    }
                }
                break;

            case TargetType.FriendlyUnit:
                // Heal command (if healers are selected)
                if (capabilities.CanHeal)
                {
                    for (int i = 0; i < _selection.Count; i++)
                    {
                        var e = _selection[i];
                        if (!_em.Exists(e)) continue;
                        if (!CanHeal(e)) continue;

                        CommandGateway.IssueHeal(_em, e, target);
                    }
                }
                else
                {
                    // Default: move to friendly unit position
                    IssueFormationMove(clickWorld);
                }
                break;

            case TargetType.Resource:
                // Gather command (if miners are selected)
                if (capabilities.CanGather)
                {
                    Entity depositLocation = FindNearestGatherersHut();
                    for (int i = 0; i < _selection.Count; i++)
                    {
                        var e = _selection[i];
                        if (!_em.Exists(e)) continue;
                        if (!_em.HasComponent<TheWaningBorder.Humans.MinerTag>(e)) continue;

                        CommandGateway.IssueGather(_em, e, target, depositLocation);
                    }
                }
                else
                {
                    // Default: move to resource position
                    IssueFormationMove(clickWorld);
                }
                break;

            case TargetType.Ground:
            default:
                // Move command
                IssueFormationMove(clickWorld);
                break;
        }
    }

    private bool IsSetRallyPointMode()
    {
        return _rallyMode;
    }

    private enum TargetType
    {
        Ground,
        Enemy,
        FriendlyUnit,
        Resource
    }

    private TargetType DetermineTargetType(Entity target)
    {
        if (target == Entity.Null || !_em.Exists(target))
            return TargetType.Ground;

        // Check if it's a resource node
        if (_em.HasComponent<TheWaningBorder.AI.IronMineTag>(target))
            return TargetType.Resource;

        // Check if it has a faction
        if (!_em.HasComponent<FactionTag>(target))
            return TargetType.Ground;

        var targetFaction = _em.GetComponentData<FactionTag>(target).Value;

        // Assume player is Faction.Blue for now
        if (targetFaction == Faction.Blue)
            return TargetType.FriendlyUnit;
        else
            return TargetType.Enemy;
    }

    private struct UnitCapabilities
    {
        public bool CanAttack;
        public bool CanHeal;
        public bool CanGather;
        public bool CanBuild;
    }

    private UnitCapabilities DetermineCapabilities()
    {
        var caps = new UnitCapabilities();

        for (int i = 0; i < _selection.Count; i++)
        {
            var e = _selection[i];
            if (!_em.Exists(e)) continue;

            // Check for combat units
            if (_em.HasComponent<Damage>(e))
                caps.CanAttack = true;

            // Check for healers (would have specific tag)
            if (CanHeal(e))
                caps.CanHeal = true;

            // Check for miners
            if (_em.HasComponent<TheWaningBorder.Humans.MinerTag>(e))
                caps.CanGather = true;

            // Check for builders
            if (_em.HasComponent<CanBuild>(e))
                caps.CanBuild = true;
        }

        return caps;
    }

    private bool CanHeal(Entity e)
    {
        // Placeholder: Check if unit has healing capability
        // TODO: Add proper healing tag/component when Litharch is implemented
        return false;
    }

    private void IssueFormationMove(float3 clickWorld)
    {
        // Count units (excluding buildings)
        int count = 0;
        for (int i = 0; i < _selection.Count; i++)
        {
            if (!_em.Exists(_selection[i])) continue;
            if (_em.HasComponent<BuildingTag>(_selection[i])) continue;
            count++;
        }

        if (count == 0) return;

        // Simple formation calculation
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / cols);

        float spacing = 2f;
        float3 forward = new float3(0, 0, 1);
        float3 right = new float3(1, 0, 0);

        float halfWidth = (cols - 1) * spacing * 0.5f;
        float halfDepth = (rows - 1) * spacing * 0.5f;
        float3 topLeft = clickWorld - right * halfWidth + forward * halfDepth;

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (idx >= _selection.Count) break;

                // Find next valid unit
                Entity e = Entity.Null;
                while (idx < _selection.Count)
                {
                    var candidate = _selection[idx];
                    idx++;
                    if (_em.Exists(candidate) && !_em.HasComponent<BuildingTag>(candidate))
                    {
                        e = candidate;
                        break;
                    }
                }

                if (e == Entity.Null) break;

                float3 slot = topLeft + right * (c * spacing) - forward * (r * spacing);
                CommandGateway.IssueMove(_em,e, slot);

                Debug.DrawLine(
                    (Vector3)_em.GetComponentData<LocalTransform>(e).Position,
                    (Vector3)slot, Color.cyan, 1.0f);
            }
        }
    }

    private Entity FindNearestGatherersHut()
    {
        // Find the nearest GatherersHut for the player
        Entity nearest = Entity.Null;
        float nearestDist = float.MaxValue;

        var query = _em.CreateEntityQuery(typeof(GathererHutTag), typeof(FactionTag), typeof(LocalTransform));
        var ents = query.ToEntityArray(Allocator.Temp);
        var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var factions = query.ToComponentDataArray<FactionTag>(Allocator.Temp);

        for (int i = 0; i < ents.Length; i++)
        {
            if (factions[i].Value != Faction.Blue) continue;

            float dist = math.distance(transforms[i].Position, float3.zero); // TODO: use avg selected unit position
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ents[i];
            }
        }

        ents.Dispose();
        transforms.Dispose();
        factions.Dispose();

        return nearest;
    }

    // ---------------- Maintenance ----------------
    void CleanSelection()
    {
        for (int i = _selection.Count - 1; i >= 0; i--)
        {
            var e = _selection[i];
            if (!_em.Exists(e)) { _selection.RemoveAt(i); continue; }
            if (_em.HasComponent<Health>(e) && _em.GetComponentData<Health>(e).Value <= 0)
                _selection.RemoveAt(i);
        }
    }

    // ---------------- Picking helpers ----------------
    Entity RaycastPickEntity()
    {
        if (!RaycastHitObject(out RaycastHit hit)) return Entity.Null;

        var query = _em.CreateEntityQuery(typeof(LocalTransform));
        var ents = query.ToEntityArray(Allocator.Temp);

        Entity best = Entity.Null;
        float bestD = 1.25f;
        for (int i = 0; i < ents.Length; i++)
        {
            var e = ents[i];
            if (!_em.Exists(e)) continue;
            var xf = _em.GetComponentData<LocalTransform>(e);
            float d = Vector3.Distance(hit.point, (Vector3)xf.Position);
            if (d < bestD) { bestD = d; best = e; }
        }
        ents.Dispose();
        return best;
    }

    bool RaycastHitObject(out RaycastHit hit)
    {
        var cam = Camera.main;
        hit = default;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit, 10000f, clickMask, QueryTriggerInteraction.Ignore);
    }

    bool TryGetClickPoint(out float3 world)
    {
        world = default;
        var cam = Camera.main;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 10000f, clickMask, QueryTriggerInteraction.Ignore))
        { world = (float3)hit.point; return true; }

        var terrain = Terrain.activeTerrain;
        if (terrain && terrain.terrainData)
        {
            Plane tp = new Plane(Vector3.up, new Vector3(0, terrain.transform.position.y, 0));
            if (tp.Raycast(ray, out float t))
            {
                var p = ray.GetPoint(t);
                float ty = terrain.SampleHeight(p) + terrain.transform.position.y;
                world = new float3(p.x, ty, p.z);
                return true;
            }
        }

        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float d2))
        {
            var p = ray.GetPoint(d2);
            world = new float3(p.x, 0f, p.z);
            return true;
        }
        return false;
    }

    // ---------------- Rect/bounds helpers ----------------
    static Rect MakeScreenRect(Vector3 a, Vector3 b)
    {
        float xMin = Mathf.Min(a.x, b.x);
        float yMin = Mathf.Min(a.y, b.y);
        float xMax = Mathf.Max(a.x, b.x);
        float yMax = Mathf.Max(a.y, b.y);
        return Rect.MinMaxRect(xMin,yMin, xMax, yMax);
    }

    static Rect ScreenToGuiRect(Rect screenRect)
    {
        return new Rect(
            screenRect.xMin,
            Screen.height - screenRect.yMax,
            screenRect.width,
            screenRect.height
        );
    }

    static Rect ProjectWorldBoundsToScreenRect(Camera cam, Bounds worldBounds)
    {
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;
        Vector3[] corners = new Vector3[8] {
            c + new Vector3( e.x,  e.y,  e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z),
            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3(-e.x, -e.y, -e.z)
        };

        float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity;
        float yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;

        bool anyInFront = false;
        for (int i = 0; i < 8; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(corners[i]);
            if (sp.z <= 0f) continue;
            anyInFront = true;
            xMin = Mathf.Min(xMin, sp.x);
            xMax = Mathf.Max(xMax, sp.x);
            yMin = Mathf.Min(yMin, sp.y);
            yMax = Mathf.Max(yMax, sp.y);
        }

        if (!anyInFront) return new Rect(0, 0, 0, 0);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    Bounds ComputeEntityWorldBounds(Entity e)
    {
        var xf = _em.GetComponentData<LocalTransform>(e);
        Vector3 pos = xf.Position;

        float r = 0.6f;
        if (_em.HasComponent<Radius>(e)) r = _em.GetComponentData<Radius>(e).Value;

        float height = 1.8f;
        if (_em.HasComponent<BuildingTag>(e))
        {
            r = 1.6f;
            height = 3.0f;
        }

        var extents = new Vector3(r, height * 0.5f, r);
        return new Bounds(pos + Vector3.up * (height * 0.5f), extents * 2f);
    }

    // ---------------- IMGUI for drag box ----------------
    static Texture2D _whiteTex;

    static void EnsureWhiteTex()
    {
        if (_whiteTex == null) _whiteTex = Texture2D.whiteTexture;
    }

    static void DrawGuiRect(Rect r, Color c)
    {
        EnsureWhiteTex();
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, _whiteTex);
        GUI.color = old;
    }

    void OnGUI()
    {
        if (_isDragging)
        {
            var guiRect = ScreenToGuiRect(_dragScreenRect);
            DrawGuiRect(guiRect, new Color(0f, 1f, 0f, 0.15f));
        }

        if (_showHelp)
        {
            GUI.Label(new Rect(10, 10, 300, 100),
                "Left-click/drag: Select\nRight-click: Move/Attack\nR: Rally mode\nESC: Clear");
        }
    }
}