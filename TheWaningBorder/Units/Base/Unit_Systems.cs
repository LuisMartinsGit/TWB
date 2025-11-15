using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Units.Base
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UnitMovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            Entities.ForEach((ref PositionComponent position, ref MovementComponent movement) =>
            {
                if (!movement.IsMoving) return;
                
                float3 direction = math.normalize(movement.Destination - position.Position);
                float moveDistance = movement.Speed * deltaTime;
                float distanceToTarget = math.distance(position.Position, movement.Destination);
                
                if (distanceToTarget <= movement.StoppingDistance)
                {
                    movement.IsMoving = false;
                }
                else
                {
                    position.Position += direction * math.min(moveDistance, distanceToTarget);
                }
            }).ScheduleParallel();
        }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UnitCombatSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            Entities.ForEach((Entity entity, ref CombatComponent combat, in PositionComponent position) =>
            {
                if (combat.Target == Entity.Null) return;
                
                if (currentTime - combat.LastAttackTime >= combat.AttackCooldown)
                {
                    combat.LastAttackTime = currentTime;
                    // Apply damage logic here
                }
            }).Run();
        }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UnitHealthSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity entity, ref HealthComponent health) =>
            {
                if (health.CurrentHp <= 0)
                {
                    EntityManager.DestroyEntity(entity);
                }
            }).Run();
        }
    }
}