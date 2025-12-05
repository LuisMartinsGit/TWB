using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace TheWaningBorder.World.FogOfWar
{
    /// <summary>
    /// ECS system that iterates all entities with LineOfSight and stamps their
    /// visibility circles into FogOfWarManager each frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FogOfWarSystem : SystemBase
    {
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
            var los = q.ToComponentDataArray<LineOfSight>(Allocator.Temp);
            var xfs = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var facs = q.ToComponentDataArray<FactionTag>(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                if (!em.Exists(ents[i])) continue;
                float r = Mathf.Max(0.01f, los[i].Radius);
                mgr.Stamp(facs[i].Value, (Vector3)xfs[i].Position, r);
            }

            ents.Dispose();
            los.Dispose();
            xfs.Dispose();
            facs.Dispose();

            mgr.EndFrameAndBuild();
        }

        /// <summary>
        /// Static helper to check visibility from outside ECS systems.
        /// </summary>
        public static bool IsVisibleToFaction(Faction f, float3 pos)
        {
            return FogOfWarManager.Instance != null &&
                   FogOfWarManager.Instance.IsVisible(f, new Vector3(pos.x, 0, pos.z));
        }

        /// <summary>
        /// Static helper to check revealed state from outside ECS systems.
        /// </summary>
        public static bool IsRevealedToFaction(Faction f, float3 pos)
        {
            return FogOfWarManager.Instance != null &&
                   FogOfWarManager.Instance.IsRevealed(f, new Vector3(pos.x, 0, pos.z));
        }
    }
}