using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitGroundingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var terrain = Terrain.activeTerrain;
        if (!terrain) return;

        // Cache only value types to capture into the lambda
        var td   = terrain.terrainData;
        var tpos = terrain.transform.position;
        var tsize = td.size;
        const float offset = 0.01f;

        // Main-thread because TerrainData sampling is UnityEngine API
        // Exclude arrow projectiles - they should fly through the air
        Entities
            .WithNone<ArrowProjectile>()
            .ForEach((ref LocalTransform xf) =>
            {
                float3 p = xf.Position;
                float u = math.unlerp(tpos.x, tpos.x + tsize.x, p.x);
                float v = math.unlerp(tpos.z, tpos.z + tsize.z, p.z);

                float y = td.GetInterpolatedHeight(u, v) + offset;
                xf.Position = new float3(p.x, y, p.z);
            })
            .WithName("UnitGrounding")
            .WithoutBurst()
            .Run();
    }
}