// RTSInput.cs - FIXED VERSION
// Now checks for both BuilderCommandPanel and BarracksPanel to prevent deselection

using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

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
        if (BuilderCommandPanel.IsPointerOverPanel() || BarracksPanel.IsPointerOverPanel())
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
        if (BuilderCommandPanel.IsPointerOverPanel() || BarracksPanel.IsPointerOverPanel()) return;

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

    // ---------------- Right-click commands ----------------
    void HandleRightClick()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        CleanSelection();
        if (_selection.Count == 0) return;

        if (!TryGetClickPoint(out float3 clickWorld)) return;

        if (_rallyMode)
        {
            _rallyMode = false;
            var bases = _em.CreateEntityQuery(typeof(BuildingTag), typeof(FactionTag))
                           .ToEntityArray(Allocator.Temp);
            foreach (var b in bases)
            {
                if (!_em.Exists(b)) continue;
                if (_em.GetComponentData<FactionTag>(b).Value != Faction.Blue) continue;

                var rp = _em.GetComponentData<RallyPoint>(b);
                rp.Has = 1; rp.Position = clickWorld;
                _em.SetComponentData(b, rp);
            }
            bases.Dispose();
            return;
        }

        var target = RaycastPickEntity();
        bool validEnemy = target != Entity.Null && _em.Exists(target) &&
                          _em.HasComponent<FactionTag>(target) &&
                          _em.GetComponentData<FactionTag>(target).Value != Faction.Blue;

        if (validEnemy)
        {
            for (int i = 0; i < _selection.Count; i++)
            {
                var e = _selection[i];
                if (!_em.Exists(e)) continue;

                // Only units can attack on right-click; skip buildings
                if (_em.HasComponent<BuildingTag>(e)) continue;

                if (!_em.HasComponent<AttackCommand>(e)) _em.AddComponent<AttackCommand>(e);
                _em.SetComponentData(e, new AttackCommand { Target = target });
                if (_em.HasComponent<MoveCommand>(e)) _em.RemoveComponent<MoveCommand>(e);
            }
            return;
        }

        // Formation move to clickWorld (ONLY units)
        int count = _selection.Count;
        float3 center = clickWorld;

        float3 selCenter = float3.zero; int alive = 0;
        for (int i = 0; i < count; i++)
        {
            var e = _selection[i];
            if (!_em.Exists(e) || !_em.HasComponent<LocalTransform>(e)) continue;
            selCenter += _em.GetComponentData<LocalTransform>(e).Position;
            alive++;
        }
        if (alive > 0) selCenter /= alive;

        float3 forward = math.normalizesafe(new float3(center.x - selCenter.x, 0, center.z - selCenter.z));
        if (math.lengthsq(forward) < 1e-4f)
        {
            var cam = Camera.main;
            forward = cam ? new float3(cam.transform.forward.x, 0, cam.transform.forward.z) : new float3(0, 0, 1);
            forward = math.normalizesafe(forward);
            if (math.lengthsq(forward) < 1e-4f) forward = new float3(0, 0, 1);
        }
        float3 right = new float3(forward.z, 0, -forward.x);

        float unitR = 0.6f;
        {
            int samples = math.min(count, 6);
            float acc = 0f; int n = 0;
            for (int i = 0; i < samples; i++)
            {
                var e = _selection[i];
                if (_em.Exists(e) && _em.HasComponent<Radius>(e))
                { acc += _em.GetComponentData<Radius>(e).Value; n++; }
            }
            if (n > 0) unitR = math.max(0.45f, acc / n);
        }
        float spacing = unitR * 2.4f;

        int cols = (int)math.ceil(math.sqrt(count));
        int rows = (int)math.ceil(count / (float)cols);

        float width = (cols - 1) * spacing;
        float height = (rows - 1) * spacing;
        float3 topLeft = center - right * (width * 0.5f) - forward * (height * 0.5f);

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (idx >= count) break;
                var e = _selection[idx++]; 
                if (!_em.Exists(e)) continue;

                // Skip buildings when issuing movement
                if (_em.HasComponent<BuildingTag>(e)) continue;

                float3 slot = topLeft + right * (c * spacing) + forward * (r * spacing);

                if (!_em.HasComponent<MoveCommand>(e)) _em.AddComponent<MoveCommand>(e);

                _em.SetComponentData(e, new MoveCommand { Destination = slot });
                Debug.DrawLine(
                    (Vector3)_em.GetComponentData<LocalTransform>(e).Position,
                    (Vector3)slot, Color.cyan, 1.0f);

                if (_em.HasComponent<AttackCommand>(e)) _em.RemoveComponent<AttackCommand>(e);
                if (_em.HasComponent<Target>(e)) _em.SetComponentData(e, new Target { Value = Entity.Null });
            }
        }
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
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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

    static void DrawGuiRectBorder(Rect r, float thickness, Color c)
    {
        DrawGuiRect(new Rect(r.xMin, r.yMin, r.width, thickness), c);
        DrawGuiRect(new Rect(r.xMin, r.yMin, thickness, r.height), c);
        DrawGuiRect(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), c);
        DrawGuiRect(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), c);
    }

    void OnGUI()
    {
        if (!_isDragging) return;

        var guiRect = ScreenToGuiRect(_dragScreenRect);
        DrawGuiRect(guiRect, new Color(0.2f, 0.6f, 1f, 0.10f));
        DrawGuiRectBorder(guiRect, 2f, new Color(0.2f, 0.6f, 1f, 0.85f));
    }
}