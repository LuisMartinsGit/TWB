using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[DefaultExecutionOrder(900)]
public class SelectionDecalManager : MonoBehaviour
{
    [Header("Ring Geometry")]
    public float YOffset = 0.04f;              // lift slightly to avoid z-fighting
    public float MinRadius = 0.45f;            // fallback if no Radius component
    public float BuildingRadius = 1.8f;        // default building ring radius
    public float RingThicknessY = 0.03f;       // cylinder height

    [Header("Colors (alpha is taken from these)")]
    [Tooltip("Alpha used for selection rings. RGB comes from FactionColors.")]
    public Color SelectedColor = new Color(0.2f, 0.75f, 1f, 0.75f);
    [Tooltip("Alpha used for hover rings. RGB comes from FactionColors.")]
    public Color HoverEnemyColor = new Color(1f, 0.25f, 0.25f, 0.85f);

    private World _world; 
    private EntityManager _em;

    // Active rings per entity
    private readonly Dictionary<Entity, GameObject> _rings = new();
    // One transient ring for hover entity
    private GameObject _hoverRing;
    private Entity _hoverFor = Entity.Null;

    private Material _ringMat;
    private FogOfWarManager _fow;
    private Faction _humanFaction = GameSettings.LocalPlayerFaction;

    void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated) _em = _world.EntityManager;

        _ringMat = MakeRingMaterial();

        _fow = FindObjectOfType<FogOfWarManager>();
        if (_fow != null) _humanFaction = _fow.HumanFaction;
    }

    void LateUpdate()
    {
        if (_em.Equals(default(EntityManager)))
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != null && _world.IsCreated) _em = _world.EntityManager;
        }
        if (_world == null || !_world.IsCreated) return;

        // Refresh human faction from FoW in case it changed at runtime
        if (_fow == null) _fow = FindObjectOfType<FogOfWarManager>();
        if (_fow != null) _humanFaction = _fow.HumanFaction;

        // 1) Maintain selection rings (per-faction color)
        var want = RTSInput.CurrentSelection ?? new List<Entity>();
        var still = new HashSet<Entity>();

        for (int i = 0; i < want.Count; i++)
        {
            var e = want[i];
            if (!_em.Exists(e)) continue;

            // Create ring if missing
            if (!_rings.TryGetValue(e, out var go) || go == null)
            {
                var selCol = GetFactionTint(e, SelectedColor.a);
                go = NewRing(selCol);
                _rings[e] = go;
            }

            // Update transform
            UpdateRingTransform(go, e, isBuilding: _em.HasComponent<BuildingTag>(e));

            // Ensure color matches current faction palette (in case of palette changes)
            var wantCol = GetFactionTint(e, SelectedColor.a);
            UpdateRingColor(go, wantCol);

            still.Add(e);
        }

        // Destroy rings for entities no longer selected/alive
        var toRemove = new List<Entity>();
        foreach (var kv in _rings)
        {
            var e = kv.Key;
            if (!still.Contains(e) || !_em.Exists(e))
            {
                if (kv.Value != null) Destroy(kv.Value);
                toRemove.Add(e);
            }
        }
        foreach (var e in toRemove) _rings.Remove(e);

        // 2) Hover ring (per-faction color, FoW-aware)
        var h = RTSInput.CurrentHover;
        bool showHover = false;
        if (h != Entity.Null && _em.Exists(h) && _em.HasComponent<FactionTag>(h))
        {
            var fac = _em.GetComponentData<FactionTag>(h).Value;
            bool mine = fac == _humanFaction;

            // For enemies: only show if visible right now. For self: always show.
            if (mine)
            {
                showHover = true;
            }
            else
            {
                // Need a position to test visibility
                if (_em.HasComponent<LocalTransform>(h))
                {
                    var pos = _em.GetComponentData<LocalTransform>(h).Position;
                    bool visible = FogOfWarSystem.IsVisibleToFaction(_humanFaction, pos);
                    showHover = visible;
                }
            }
        }

        if (showHover)
        {
            if (_hoverRing == null || _hoverFor != h)
            {
                ClearHoverRing();
                var col = GetFactionTint(h, HoverEnemyColor.a); // alpha from HoverEnemyColor, RGB from faction
                _hoverRing = NewRing(col);
                _hoverFor = h;
            }

            UpdateRingTransform(_hoverRing, h, isBuilding: _em.HasComponent<BuildingTag>(h));
            // Keep color fresh (if faction palette changes)
            UpdateRingColor(_hoverRing, GetFactionTint(h, HoverEnemyColor.a));
        }
        else
        {
            ClearHoverRing();
        }
    }

    void OnDestroy()
    {
        foreach (var kv in _rings) if (kv.Value) Destroy(kv.Value);
        _rings.Clear();
        ClearHoverRing();
        if (_ringMat != null) Destroy(_ringMat);
    }

    // --- helpers -------------------------------------------------------------

    private Material MakeRingMaterial()
    {
        // Transparent unlit that works on URP or Built-in
        Shader sh =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");
        var m = new Material(sh);
        // Enable transparency if possible
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1); // URP: Transparent
        if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", 0);
        if (m.HasProperty("_Cull"))    m.SetFloat("_Cull", 2);    // Back
        if (m.HasProperty("_ZWrite"))  m.SetFloat("_ZWrite", 0);
        m.renderQueue = 3000;
        return m;
    }

    private GameObject NewRing(Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "SelectionRing";
        go.transform.rotation = Quaternion.identity;

        var mr = go.GetComponent<MeshRenderer>();
        var mc = go.GetComponent<Collider>();
        if (mc) mc.enabled = false; // no physics

        var mat = new Material(_ringMat);
        SetMatColor(mat, color);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    private void SetMatColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }

    private void UpdateRingColor(GameObject ring, Color color)
    {
        if (ring == null) return;
        var mr = ring.GetComponent<MeshRenderer>();
        if (mr == null) return;

        // Avoid material instancing churn: reuse sharedMaterial we created at NewRing
        var mat = mr.sharedMaterial;
        if (mat != null) SetMatColor(mat, color);
    }

    private void UpdateRingTransform(GameObject ring, Entity e, bool isBuilding)
    {
        if (!_em.HasComponent<LocalTransform>(e)) return;
        var xf = _em.GetComponentData<LocalTransform>(e);
        var pos = (Vector3)xf.Position;
        pos.y += YOffset;

        float r = MinRadius;
        if (_em.HasComponent<Radius>(e))
            r = Mathf.Max(MinRadius, _em.GetComponentData<Radius>(e).Value);
        if (isBuilding)
            r = Mathf.Max(r, BuildingRadius);

        // Scale cylinder so its footprint approximates a thin ring
        ring.transform.position = pos;
        ring.transform.localScale = new Vector3(r * 2f, Mathf.Max(0.01f, RingThicknessY), r * 2f);
    }

    private void ClearHoverRing()
    {
        if (_hoverRing != null) Destroy(_hoverRing);
        _hoverRing = null;
        _hoverFor = Entity.Null;
    }

    private Color GetFactionTint(Entity e, float alpha)
    {
        // Default color if no faction
        Color baseCol = new Color(1f, 1f, 1f, 1f);
        if (_em.Exists(e) && _em.HasComponent<FactionTag>(e))
        {
            var fac = _em.GetComponentData<FactionTag>(e).Value;
            baseCol = FactionColors.Get(fac);
        }
        baseCol.a = Mathf.Clamp01(alpha);
        return baseCol;
    }
}
