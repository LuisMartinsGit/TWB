using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FogOfWarSystem))]
public partial class FogVisibilitySyncSystem : SystemBase
{
    Entity _visibleSingleton;

    protected override void OnCreate()
    {
        base.OnCreate();

        // Ensure the singleton with the buffer exists
        var q = EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FogVisibleTag>(),
            ComponentType.ReadWrite<VisibleUnitElement>());

        if (q.IsEmptyIgnoreFilter)
        {
            _visibleSingleton = EntityManager.CreateEntity(typeof(FogVisibleTag));
            EntityManager.AddBuffer<VisibleUnitElement>(_visibleSingleton);
        }
        else
        {
            _visibleSingleton = q.GetSingletonEntity();
        }
    }

    protected override void OnUpdate()
    {
        var mgr = FogOfWarManager.Instance;
        if (mgr == null) return;

        var human = mgr.HumanFaction;
        var evm   = Object.FindObjectOfType<EntityViewManager>();
        if (evm == null) return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Manual query (avoids EA0011 and fluent-API chain issues)
        var q = em.CreateEntityQuery(
            ComponentType.ReadOnly<PresentationId>(),
            ComponentType.ReadOnly<LocalTransform>());

        var ents = q.ToEntityArray(Allocator.Temp);
        var xfs  = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        // Clear + prep the buffer for “visible enemy units this frame”
        var buf = EntityManager.GetBuffer<VisibleUnitElement>(_visibleSingleton);
        buf.Clear();
        buf.EnsureCapacity(ents.Length); // cheap guard against reallocation

        for (int i = 0; i < ents.Length; i++)
        {
            var e  = ents[i];
            var xf = xfs[i];

            if (!evm.TryGetView(e, out var go) || go == null) continue;

            bool isBuilding = em.HasComponent<BuildingTag>(e);
            bool isUnit     = em.HasComponent<UnitTag>(e);

            bool vis  = mgr.IsVisible(human, (Vector3)xf.Position);
            bool rev  = mgr.IsRevealed(human, (Vector3)xf.Position);
            bool mine = em.HasComponent<FactionTag>(e) && em.GetComponentData<FactionTag>(e).Value == human;

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                if (mine) { go.SetActive(true); continue; }
                if (isUnit) { go.SetActive(vis); }
                else { go.SetActive(vis || (isBuilding && rev)); }
            }
            else
            {
                if (mine)
                {
                    go.SetActive(true);
                    var rmpb = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(rmpb);
                    rend.SetPropertyBlock(rmpb);
                }
                else if (isUnit)
                {
                    go.SetActive(vis);
                }
                else if (vis)
                {
                    go.SetActive(true);
                    var mpb = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(mpb);
                    rend.SetPropertyBlock(mpb);
                }
                else if (isBuilding && rev)
                {
                    go.SetActive(true);
                    var mpb = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(mpb);
                    rend.SetPropertyBlock(mpb);
                }
                else
                {
                    go.SetActive(false);
                }
            }

            // 🔑 Record visible ENEMY UNITS only
            if (!mine && isUnit && vis)
            {
                buf.Add(new VisibleUnitElement { Value = e });
            }
        }

        ents.Dispose();
        xfs.Dispose();
    }
}
