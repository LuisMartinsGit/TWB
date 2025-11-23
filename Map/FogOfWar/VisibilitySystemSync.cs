using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Hides/shows GameObject presentation based on FoW for the human player.
/// Buildings show as ghosts when only revealed.
/// Requires EntityViewManager to have instantiated a view GameObject per entity
/// and to expose TryGetView(Entity, out GameObject).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FogOfWarSystem))]
public partial class FogVisibilitySyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var mgr = FogOfWarManager.Instance;
        if (mgr == null) return;

        var human = mgr.HumanFaction;
        var evm   = Object.FindObjectOfType<EntityViewManager>();
        if (evm == null) return;

        // Manual query (avoids EA0011 and fluent-API chain issues)
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var q = em.CreateEntityQuery(
            ComponentType.ReadOnly<PresentationId>(),
            ComponentType.ReadOnly<LocalTransform>());

        var ents = q.ToEntityArray(Allocator.Temp);
        var xfs  = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        for (int i = 0; i < ents.Length; i++)
        {
            var e  = ents[i];
            var xf = xfs[i];

            if (!evm.TryGetView(e, out var go) || go == null) continue;

            bool isBuilding = em.HasComponent<BuildingTag>(e);

            bool vis = mgr.IsVisible(human, (Vector3)xf.Position);
            bool rev = mgr.IsRevealed(human, (Vector3)xf.Position);
            bool mine = em.HasComponent<FactionTag>(e) && em.GetComponentData<FactionTag>(e).Value == human;
            bool isUnit = em.HasComponent<UnitTag>(e);

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                // No renderer to ghost → apply active/inactive only
                if (mine) { go.SetActive(true); continue; }
                if (isUnit) { go.SetActive(vis); continue; }
                go.SetActive(vis || (isBuilding && rev));
                continue;
            }

            // Player-owned entities ignore FoW (always visible, non-ghost)
            if (mine)
            {
                go.SetActive(true);
                var rmpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(rmpb);
                // Clear any ghost properties if your shader uses them
                rend.SetPropertyBlock(rmpb);
                continue;
            }

            // Enemy/neutral logic:
            if (isUnit && !mine)
            {
                // Units: only visible when currently visible (no ghost-on-reveal)
                go.SetActive(vis);
                continue;
            }

            if (vis)
            {
                go.SetActive(true);
                var mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                // Optional shader knobs if your shader supports them:
                // mpb.SetFloat("_Desaturate", 0f);
                // mpb.SetFloat("_Alpha", 1f);
                rend.SetPropertyBlock(mpb);
            }
            else if (isBuilding && rev)
            {
                go.SetActive(true);
                var mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                // Optional ghosting knobs:
                // mpb.SetFloat("_Desaturate", 1f);
                // mpb.SetFloat("_Alpha", 0.5f);
                rend.SetPropertyBlock(mpb);
            }
            else
            {
                go.SetActive(false);
            }
        }

        ents.Dispose();
        xfs.Dispose();
    }
}
