using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Units.Base;
using TheWaningBorder.Buildings.Base;

namespace TheWaningBorder.Player.Commands
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CommandSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            Entities
                .WithoutBurst().ForEach((Entity entity, ref CommandComponent command, ref MovementComponent movement) =>
                {
                    switch (command.Type)
                    {
                        case CommandType.Move:
                            movement.Destination = command.TargetPosition;
                            movement.IsMoving = true;
                            break;
                            
                        case CommandType.Attack:
                            // Set attack target
                            if (EntityManager.HasComponent<CombatComponent>(entity))
                            {
                                var combat = EntityManager.GetComponentData<CombatComponent>(entity);
                                combat.Target = command.TargetEntity;
                                combat.IsAttacking = true;
                                EntityManager.SetComponentData(entity, combat);
                            }
                            break;
                            
                        case CommandType.Stop:
                            movement.IsMoving = false;
                            break;
                    }
                    
                    // Clear command after processing
                    command.Type = CommandType.Stop;
                }).Run();
        }
    }
}