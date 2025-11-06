using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using TheWaningBorder.Player;

public class HealthbarOverlay : MonoBehaviour
{
    World _world;
    EntityManager _em;

    EntityQuery _visQuery; // cache the singleton query

    void OnEnable()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated)
        {
            _em = _world.EntityManager;
            _visQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<FogVisibleTag>(),
                ComponentType.ReadOnly<VisibleUnitElement>());
        }
    }

    bool IsEnemyUnitVisible(Entity e)
    {
        if (_em == default || _visQuery == default || _visQuery.IsEmptyIgnoreFilter) return false;
        var singleton = _visQuery.GetSingletonEntity();
        var buf = _em.GetBuffer<VisibleUnitElement>(singleton);
        // We only check this for the hovered entity → linear scan is fine.
        for (int i = 0; i < buf.Length; i++)
            if (buf[i].Value == e)
                return true;
        return false;
    }

    void OnGUI()
    {
        if (_em == default || Camera.main == null) return;

        var q = _em.CreateEntityQuery(typeof(Health), typeof(LocalTransform));
        var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp);
        var xfs  = q.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
        var hps  = q.ToComponentDataArray<Health>(Unity.Collections.Allocator.Temp);

        var sel = Controls.CurrentSelection;
        var hov = Controls.CurrentHover;

        for (int i = 0; i < ents.Length; i++)
        {
            var e = ents[i];
            if (!_em.Exists(e)) continue;

            bool isBuilding = _em.HasComponent<BuildingTag>(e);
            bool isUnit     = _em.HasComponent<UnitTag>(e);

            bool show = false;

            var isFriendly = _em.HasComponent<FactionTag>(e) && _em.GetComponentData<FactionTag>(e).Value == Faction.Blue;
            bool selected = sel != null && sel.Contains(e);
            bool hovered = hov == e;

            // Friendly rules (your original logic)
            if (isFriendly && isBuilding) show = true;
            else if (isFriendly && isUnit && (selected || hovered)) show = true;

            // 🔑 Enemy hover rule: only show if it’s a unit AND currently visible via FoW
            if (!isFriendly && isUnit && hovered && IsEnemyUnitVisible(e))
                show = true;

            if (!show) continue;

            var hp = hps[i];
            if (hp.Max <= 0) continue;
            float frac = Mathf.Clamp01(hp.Value / (float)hp.Max);

            Vector3 world = (Vector3)xfs[i].Position + Vector3.up * (isBuilding ? 2.5f : 1.6f);
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            if (screen.z <= 0) continue;

            float width = isBuilding ? 80f : 50f;
            float height = 8f;
            var rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height, width, height);

            Color old = GUI.color;
            GUI.color = new Color(0,0,0,0.5f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(new Color(0.8f,0.1f,0.1f), new Color(0.1f,0.9f,0.2f), frac);
            GUI.DrawTexture(new Rect(rect.x+1, rect.y+1, (width-2) * frac, height-2), Texture2D.whiteTexture);

            GUI.color = old;
        }

        ents.Dispose(); xfs.Dispose(); hps.Dispose();
    }
}
