// Assets/Scripts/Inputs/RTSInput.cs
// Player input handler - routes all commands through CommandRouter
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using TheWaningBorder.Core;
using TheWaningBorder.Multiplayer;
using TheWaningBorder.AI;

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

    void CleanSelection()
    {
        for (int i = _selection.Count - 1; i >= 0; i--)
        {
            if (!_em.Exists(_selection[i]))
                _selection.RemoveAt(i);
        }
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
                    _em.GetComponentData<FactionTag>(e).Value == GameSettings.LocalPlayerFaction &&
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
                _em.GetComponentData<FactionTag>(e).Value != GameSettings.LocalPlayerFaction)
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

    // ---------------- Right-click commands (ALL GO THROUGH COMMANDROUTER) ----------------
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

                // Route through CommandRouter (handles lockstep automatically)
                CommandRouter.SetRallyPoint(e, clickWorld, CommandRouter.CommandSource.LocalPlayer);
            }
            return;
        }

        var target = RaycastPickEntity();
        TargetType targetType = DetermineTargetType(target);
        UnitCapabilities capabilities = DetermineCapabilities();

        switch (targetType)
        {
            case TargetType.Enemy:
                if (capabilities.CanAttack)
                {
                    for (int i = 0; i < _selection.Count; i++)
                    {
                        var e = _selection[i];
                        if (!_em.Exists(e)) continue;
                        if (_em.HasComponent<BuildingTag>(e)) continue;

                        // Route through CommandRouter
                        CommandRouter.IssueAttack(e, target, CommandRouter.CommandSource.LocalPlayer);
                    }
                }
                break;

            case TargetType.FriendlyUnit:
                if (capabilities.CanHeal)
                {
                    for (int i = 0; i < _selection.Count; i++)
                    {
                        var e = _selection[i];
                        if (!_em.Exists(e)) continue;
                        if (!CanHeal(e)) continue;

                        // Route through CommandRouter
                        CommandRouter.IssueHeal(e, target, CommandRouter.CommandSource.LocalPlayer);
                    }
                }
                else
                {
                    IssueFormationMove(clickWorld);
                }
                break;

            case TargetType.Resource:
                if (capabilities.CanGather)
                {
                    Entity depositLocation = FindNearestGatherersHut();
                    for (int i = 0; i < _selection.Count; i++)
                    {
                        var e = _selection[i];
                        if (!_em.Exists(e)) continue;
                        if (!_em.HasComponent<TheWaningBorder.Humans.MinerTag>(e)) continue;

                        // Route through CommandRouter
                        CommandRouter.IssueGather(e, target, depositLocation, CommandRouter.CommandSource.LocalPlayer);
                    }
                }
                else
                {
                    IssueFormationMove(clickWorld);
                }
                break;

            case TargetType.Ground:
            default:
                IssueFormationMove(clickWorld);
                break;
        }
    }

    // Formation movement - all units through CommandRouter
    private void IssueFormationMove(float3 clickWorld)
    {
        int count = 0;
        for (int i = 0; i < _selection.Count; i++)
        {
            if (_em.Exists(_selection[i]) && !_em.HasComponent<BuildingTag>(_selection[i]))
                count++;
        }

        if (count == 0) return;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / cols);
        float spacing = 2.0f;

        var cam = Camera.main;
        Vector3 camForward = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, camForward).normalized;
        float3 forward = new float3(camForward.x, camForward.y, camForward.z);
        float3 rightF3 = new float3(right.x, right.y, right.z);

        float3 topLeft = clickWorld - rightF3 * ((cols - 1) * spacing * 0.5f) + forward * ((rows - 1) * spacing * 0.5f);

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (idx >= count) break;

                Entity e = Entity.Null;
                int searchIdx = 0;
                for (int i = 0; i < _selection.Count; i++)
                {
                    if (_em.Exists(_selection[i]) && !_em.HasComponent<BuildingTag>(_selection[i]))
                    {
                        if (searchIdx == idx)
                        {
                            e = _selection[i];
                            break;
                        }
                        searchIdx++;
                    }
                }

                if (e == Entity.Null) { idx++; continue; }

                float3 slot = topLeft + rightF3 * (c * spacing) - forward * (r * spacing);

                // Route through CommandRouter (handles lockstep automatically)
                CommandRouter.IssueMove(e, slot, CommandRouter.CommandSource.LocalPlayer);

                idx++;
            }
        }
    }

    // ---------------- Helper Methods ----------------
    
    bool IsSetRallyPointMode()
    {
        return _rallyMode;
    }

    enum TargetType { Ground, Enemy, FriendlyUnit, Resource }

    TargetType DetermineTargetType(Entity target)
    {
        if (target == Entity.Null || !_em.Exists(target))
            return TargetType.Ground;

        // Check if it's a resource node (Iron Mine)
        if (_em.HasComponent<IronMineTag>(target))
            return TargetType.Resource;

        // Check faction
        if (_em.HasComponent<FactionTag>(target))
        {
            var faction = _em.GetComponentData<FactionTag>(target).Value;
            if (faction == GameSettings.LocalPlayerFaction)
            {
                // It's ours - check if it's a unit we can heal
                if (_em.HasComponent<UnitTag>(target))
                    return TargetType.FriendlyUnit;
            }
            else
            {
                // Enemy (any non-local faction)
                return TargetType.Enemy;
            }
        }

        return TargetType.Ground;
    }

    struct UnitCapabilities
    {
        public bool CanAttack;
        public bool CanGather;
        public bool CanHeal;
        public bool CanBuild;
    }

    UnitCapabilities DetermineCapabilities()
    {
        var caps = new UnitCapabilities();

        for (int i = 0; i < _selection.Count; i++)
        {
            var e = _selection[i];
            if (!_em.Exists(e)) continue;
            if (_em.HasComponent<BuildingTag>(e)) continue;

            if (_em.HasComponent<Damage>(e))
                caps.CanAttack = true;
            if (_em.HasComponent<TheWaningBorder.Humans.MinerTag>(e))
                caps.CanGather = true;
            // Check for healer capability - currently not implemented
            if (CanHeal(e))
                caps.CanHeal = true;
            if (_em.HasComponent<CanBuild>(e))
                caps.CanBuild = true;
        }

        return caps;
    }

    bool CanHeal(Entity e)
    {
        // Placeholder: Check if unit has healing capability
        // TODO: Add proper healing tag/component when Litharch is implemented
        return false;
    }

    Entity FindNearestGatherersHut()
    {
        Entity nearest = Entity.Null;
        float nearestDist = float.MaxValue;

        // Get average position of selected miners
        float3 avgPos = float3.zero;
        int count = 0;
        for (int i = 0; i < _selection.Count; i++)
        {
            if (_em.Exists(_selection[i]) && _em.HasComponent<LocalTransform>(_selection[i]))
            {
                avgPos += _em.GetComponentData<LocalTransform>(_selection[i]).Position;
                count++;
            }
        }
        if (count > 0) avgPos /= count;

        var query = _em.CreateEntityQuery(typeof(GathererHutTag), typeof(LocalTransform), typeof(FactionTag));
        var ents = query.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < ents.Length; i++)
        {
            var e = ents[i];
            if (!_em.Exists(e)) continue;
            if (_em.GetComponentData<FactionTag>(e).Value != GameSettings.LocalPlayerFaction) continue;

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            float dist = math.distance(avgPos, pos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = e;
            }
        }

        ents.Dispose();
        return nearest;
    }

    // ---------------- Raycasting ----------------
    
    Entity RaycastPickEntity()
    {
        var cam = Camera.main;
        if (!cam) return Entity.Null;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickMask))
        {
            var go = hit.collider.gameObject;
            // Check for EntityReference component (your project's version of EntityLink)
            var link = go.GetComponent<EntityReference>();
            if (link != null && _em.Exists(link.Entity))
                return link.Entity;
            
            // Check parent
            if (go.transform.parent != null)
            {
                link = go.transform.parent.GetComponent<EntityReference>();
                if (link != null && _em.Exists(link.Entity))
                    return link.Entity;
            }
        }
        return Entity.Null;
    }

    bool TryGetClickPoint(out float3 point)
    {
        point = float3.zero;
        var cam = Camera.main;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickMask))
        {
            point = hit.point;
            return true;
        }
        return false;
    }

    Rect MakeScreenRect(Vector3 a, Vector3 b)
    {
        float minX = Mathf.Min(a.x, b.x);
        float maxX = Mathf.Max(a.x, b.x);
        float minY = Mathf.Min(a.y, b.y);
        float maxY = Mathf.Max(a.y, b.y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    Bounds ComputeEntityWorldBounds(Entity e)
    {
        if (!_em.HasComponent<LocalTransform>(e))
            return new Bounds(Vector3.zero, Vector3.zero);

        var pos = _em.GetComponentData<LocalTransform>(e).Position;
        return new Bounds(new Vector3(pos.x, pos.y, pos.z), Vector3.one * 2f);
    }

    Rect ProjectWorldBoundsToScreenRect(Camera cam, Bounds wb)
    {
        Vector3[] corners = new Vector3[8];
        corners[0] = wb.min;
        corners[1] = new Vector3(wb.min.x, wb.min.y, wb.max.z);
        corners[2] = new Vector3(wb.min.x, wb.max.y, wb.min.z);
        corners[3] = new Vector3(wb.min.x, wb.max.y, wb.max.z);
        corners[4] = new Vector3(wb.max.x, wb.min.y, wb.min.z);
        corners[5] = new Vector3(wb.max.x, wb.min.y, wb.max.z);
        corners[6] = new Vector3(wb.max.x, wb.max.y, wb.min.z);
        corners[7] = wb.max;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(corners[i]);
            if (sp.z < 0) continue;
            if (sp.x < minX) minX = sp.x;
            if (sp.x > maxX) maxX = sp.x;
            if (sp.y < minY) minY = sp.y;
            if (sp.y > maxY) maxY = sp.y;
        }

        if (minX > maxX || minY > maxY)
            return new Rect(0, 0, 0, 0);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    // ---------------- GUI ----------------
    
    void OnGUI()
    {
        // Draw selection rectangle
        if (_isDragging && (_dragScreenRect.width > 4f || _dragScreenRect.height > 4f))
        {
            Rect r = new Rect(
                _dragScreenRect.x,
                Screen.height - _dragScreenRect.y - _dragScreenRect.height,
                _dragScreenRect.width,
                _dragScreenRect.height
            );
            GUI.Box(r, "");
        }

        // Help text
        if (_showHelp)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Controls:");
            GUILayout.Label("Left-click: Select unit");
            GUILayout.Label("Left-drag: Box select");
            GUILayout.Label("Right-click: Move/Attack/Gather");
            GUILayout.Label("R: Toggle rally point mode");
            GUILayout.Label("ESC: Clear selection");
            if (GameSettings.IsMultiplayer)
            {
                GUILayout.Label($"Faction: {GameSettings.LocalPlayerFaction}");
                GUILayout.Label("Multiplayer: Active");
            }
            GUILayout.EndArea();
        }

        // Rally mode indicator
        if (_rallyMode)
        {
            GUI.Label(new Rect(Screen.width / 2 - 50, 10, 100, 20), "RALLY MODE");
        }
    }
}

// Helper component for entity references on GameObjects
// If you already have this in your project with a different name, just use yours
public class EntityReference : MonoBehaviour
{
    public Entity Entity;
}