using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class HealthbarOverlay : MonoBehaviour
{
    World _world;
    EntityManager _em;

    void OnEnable()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated)
            _em = _world.EntityManager;
    }

    void OnGUI()
    {
        if (_em == default || Camera.main == null) return;

        var q = _em.CreateEntityQuery(typeof(Health), typeof(LocalTransform));
        var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp);
        var xfs  = q.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
        var hps  = q.ToComponentDataArray<Health>(Unity.Collections.Allocator.Temp);

        var sel = RTSInput.CurrentSelection;
        var hov = RTSInput.CurrentHover;

        for (int i = 0; i < ents.Length; i++)
        {
            var e = ents[i];
            if (!_em.Exists(e)) continue;

            bool isBuilding = _em.HasComponent<BuildingTag>(e);
            bool isUnit     = _em.HasComponent<UnitTag>(e);

            // Visibility rules
            bool show = false;
            if (isBuilding)
            {
                show = true; // always
            }
            else if (isUnit)
            {
                var isFriendly = _em.HasComponent<FactionTag>(e) && _em.GetComponentData<FactionTag>(e).Value == Faction.Blue;
                bool selected = (sel != null && sel.Contains(e));
                bool hovered  = (hov == e);

                if (isFriendly) show = selected || hovered;
                else            show = hovered;
            }

            if (!show) continue;

            var hp = hps[i];
            if (hp.Max <= 0) continue;
            float frac = Mathf.Clamp01(hp.Value / (float)hp.Max);

            // World → Screen
            Vector3 world = (Vector3)xfs[i].Position + Vector3.up * (isBuilding ? 2.5f : 1.6f);
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            if (screen.z <= 0) continue;

            // Draw (bottom-left origin)
            float width = isBuilding ? 80f : 50f;
            float height = 8f;
            var rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height, width, height);

            // Back
            Color old = GUI.color;
            GUI.color = new Color(0,0,0,0.5f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Fill
            GUI.color = Color.Lerp(new Color(0.8f,0.1f,0.1f), new Color(0.1f,0.9f,0.2f), frac);
            GUI.DrawTexture(new Rect(rect.x+1, rect.y+1, (width-2) * frac, height-2), Texture2D.whiteTexture);

            GUI.color = old;
        }

        ents.Dispose(); xfs.Dispose(); hps.Dispose();
    }
}
