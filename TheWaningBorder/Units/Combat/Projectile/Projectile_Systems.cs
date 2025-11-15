using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Units.Combat.Projectile
{
    public struct ProjectileComponent : IComponentData
    {
        public float3 StartPosition;
        public float3 TargetPosition;
        public Entity Target;
        public float Speed;
        public float Damage;
        public Unity.Collections.FixedString64Bytes DamageType;
        public int OwnerId;
    }
    
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProjectileSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (projectile, position, entity) in 
                     SystemAPI.Query<RefRW<ProjectileComponent>, RefRW<PositionComponent>>()
                     .WithEntityAccess())
            {
                float3 direction = math.normalize(projectile.ValueRO.TargetPosition - position.ValueRO.Position);
                float moveDistance = projectile.ValueRO.Speed * deltaTime;
                float distanceToTarget = math.distance(position.ValueRO.Position, projectile.ValueRO.TargetPosition);
                
                if (distanceToTarget <= moveDistance)
                {
                    // Hit target - apply damage and destroy projectile
                    state.EntityManager.DestroyEntity(entity);
                }
                else
                {
                    position.ValueRW.Position += direction * moveDistance;
                }
            }
        }
    }
}