using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class FogOfWarSystem : SystemBase
{
    static bool s_logged;

    protected override void OnUpdate()
    {
        var mgr = FogOfWarManager.Instance;
        if (mgr == null) return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        mgr.BeginFrame();

        var q = em.CreateEntityQuery(
            ComponentType.ReadOnly<LineOfSight>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<FactionTag>());

        var ents = q.ToEntityArray(Allocator.Temp);
        var los  = q.ToComponentDataArray<LineOfSight>(Allocator.Temp);
        var xfs  = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var facs = q.ToComponentDataArray<FactionTag>(Allocator.Temp);

        int stamped = 0;
        for (int i = 0; i < ents.Length; i++)
        {
            if (!em.Exists(ents[i])) continue;
            // Skip zero/negative radius just in case
            float r = Mathf.Max(0.01f, los[i].Radius);
            mgr.Stamp(facs[i].Value, (Vector3)xfs[i].Position, r);
            stamped++;
        }

        if (!s_logged)
        {
            s_logged = true;
            Debug.Log($"[FogOfWarSystem] LoS sources found: {ents.Length}, stamped: {stamped}. Human faction: {mgr.HumanFaction}");
            if (stamped == 0)
                Debug.LogWarning("[FogOfWarSystem] No LoS sources stamped. Ensure units/bases/outposts have LineOfSight + FactionTag + LocalTransform.");
        }

        ents.Dispose(); los.Dispose(); xfs.Dispose(); facs.Dispose();

        mgr.EndFrameAndBuild();
    }

    public static bool IsVisibleToFaction(Faction f, Unity.Mathematics.float3 pos) =>
        FogOfWarManager.Instance && FogOfWarManager.Instance.IsVisible(f, new Vector3(pos.x, 0, pos.z));
    public static bool IsRevealedToFaction(Faction f, Unity.Mathematics.float3 pos) =>
        FogOfWarManager.Instance && FogOfWarManager.Instance.IsRevealed(f, new Vector3(pos.x, 0, pos.z));
}
