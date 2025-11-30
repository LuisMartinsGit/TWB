using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using UnityEngine;
using TheWaningBorder.Core;

namespace TheWaningBorder.Multiplayer.Systems
{
    /// <summary>
    /// Server-side system that processes NetworkCommandInput from clients.
    /// Validates authority and executes commands via CommandGateway.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnifiedCombatSystem))]
    public partial class CommandProcessingSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Process all NetworkCommandInput components
            foreach (var (input, connection, entity) in 
                SystemAPI.Query<RefRO<NetworkCommandInput>, RefRO<PlayerConnection>>().WithEntityAccess())
            {
                var command = input.ValueRO;
                
                // Skip if no command
                if (command.Type == CommandType.None)
                    continue;

                // Validate that the entity being commanded belongs to this player
                if (!ValidateCommandAuthority(command.TargetEntityNetworkId, connection.ValueRO.AssignedFaction))
                {
                    Debug.LogWarning($"[CommandProcessing] Invalid command authority from player {connection.ValueRO.PlayerId}");
                    continue;
                }

                // Find the entity by network ID
                Entity targetEntity = FindEntityByNetworkId(command.TargetEntityNetworkId);
                if (targetEntity == Entity.Null)
                {
                    Debug.LogWarning($"[CommandProcessing] Entity not found for network ID {command.TargetEntityNetworkId}");
                    continue;
                }

                // Execute the command via CommandGateway
                ExecuteCommand(command, targetEntity);

                // Clear the input after processing
                em.RemoveComponent<NetworkCommandInput>(entity);
            }
        }

        private bool ValidateCommandAuthority(int networkId, Faction playerFaction)
        {
            // Find the entity and check if it belongs to the player's faction
            var targetEntity = FindEntityByNetworkId(networkId);
            if (targetEntity == Entity.Null)
                return false;

            if (!EntityManager.HasComponent<FactionTag>(targetEntity))
                return false;

            var entityFaction = EntityManager.GetComponentData<FactionTag>(targetEntity).Value;
            return entityFaction == playerFaction;
        }

        private Entity FindEntityByNetworkId(int networkId)
        {
            // Search for entity with matching NetworkId
            foreach (var (netId, entity) in SystemAPI.Query<RefRO<NetworkedEntity>>().WithEntityAccess())
            {
                if (netId.ValueRO.NetworkId == networkId)
                    return entity;
            }
            return Entity.Null;
        }

        private void ExecuteCommand(NetworkCommandInput command, Entity targetEntity)
        {
            var em = EntityManager;

            switch (command.Type)
            {
                case CommandType.Move:
                    CommandGateway.IssueMove(em, targetEntity, command.Destination);
                    break;

                case CommandType.Attack:
                    Entity attackTarget = FindEntityByNetworkId(command.SecondaryTargetNetworkId);
                    if (attackTarget != Entity.Null)
                    {
                        CommandGateway.IssueAttack(em, targetEntity, attackTarget);
                    }
                    break;

                case CommandType.Stop:
                    CommandGateway.IssueStop(em, targetEntity);
                    break;

                case CommandType.Build:
                    string buildingId = command.BuildingId.ToString();
                    // We'd need to create the building entity here
                    // For now, passing Entity.Null as the target building
                    CommandGateway.IssueBuild(em, targetEntity, Entity.Null, buildingId, command.Destination);
                    break;

                case CommandType.Gather:
                    Entity resourceNode = FindEntityByNetworkId(command.SecondaryTargetNetworkId);
                    int depositId = (int)command.Destination.x; // Hack: encoded in destination
                    Entity depositLocation = FindEntityByNetworkId(depositId);
                    
                    if (resourceNode != Entity.Null && depositLocation != Entity.Null)
                    {
                        CommandGateway.IssueGather(em, targetEntity, resourceNode, depositLocation);
                    }
                    break;

                case CommandType.Heal:
                    Entity healTarget = FindEntityByNetworkId(command.SecondaryTargetNetworkId);
                    if (healTarget != Entity.Null)
                    {
                        CommandGateway.IssueHeal(em, targetEntity, healTarget);
                    }
                    break;
            }
        }
    }
}
