using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Handles destruction of units when their health reaches 0 or below.
/// Also cleans up any references to dead entities (attack commands, etc.)
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ArrowProjectileSystem))]
public partial struct DeathSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        var em = state.EntityManager;

        // Collect all dead entities (health <= 0)
        var deadSet = new NativeParallelHashSet<Entity>(128, Allocator.Temp);

        foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
        {
            if (health.ValueRO.Value <= 0)
            {
                deadSet.Add(entity);
            }
        }

        if (!deadSet.IsEmpty)
        {
            // Clear Target components pointing to dead entities
            foreach (var (target, entity) in SystemAPI.Query<RefRO<Target>>().WithEntityAccess())
            {
                if (target.ValueRO.Value != Entity.Null && deadSet.Contains(target.ValueRO.Value))
                {
                    ecb.SetComponent(entity, new Target { Value = Entity.Null });
                }
            }
            
            // Destroy all dead entities
            using (var deadList = deadSet.ToNativeArray(Allocator.Temp))
            {
                for (int i = 0; i < deadList.Length; i++)
                {
                    ecb.DestroyEntity(deadList[i]);
                }
            }
        }

        deadSet.Dispose();
    }
}