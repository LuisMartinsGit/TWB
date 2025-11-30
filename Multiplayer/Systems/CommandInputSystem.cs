using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Multiplayer.Systems
{
    /// <summary>
    /// Client-side system that collects player input and creates NetworkCommandInput.
    /// This is called from RTSInput when the player issues commands.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class CommandInputSystem : SystemBase
    {
        private Entity _localPlayerEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            // Find local player connection if we don't have it
            if (_localPlayerEntity == Entity.Null)
            {
                foreach (var (connection, entity) in SystemAPI.Query<RefRO<PlayerConnection>>().WithAll<LocalPlayerTag>().WithEntityAccess())
                {
                    _localPlayerEntity = entity;
                    break;
                }
            }

            // Process any pending commands would happen here
            // But since we're driven by RTSInput, we don't need to do anything in OnUpdate
            // The public methods below are called from RTSInput
        }

        /// <summary>
        /// Issue a move command (called from RTSInput).
        /// </summary>
        public void IssueMoveCommand(int entityNetworkId, float3 destination)
        {
            if (_localPlayerEntity == Entity.Null) return;

            var input = new NetworkCommandInput
            {
                Type = CommandType.Move,
                TargetEntityNetworkId = entityNetworkId,
                Destination = destination
            };

            // Add as a component to the connection entity
            // Netcode will automatically send it to the server
            EntityManager.AddComponentData(_localPlayerEntity, input);
        }

        /// <summary>
        /// Issue an attack command (called from RTSInput).
        /// </summary>
        public void IssueAttackCommand(int attackerNetworkId, int targetNetworkId)
        {
            if (_localPlayerEntity == Entity.Null) return;

            var input = new NetworkCommandInput
            {
                Type = CommandType.Attack,
                TargetEntityNetworkId = attackerNetworkId,
                SecondaryTargetNetworkId = targetNetworkId
            };

            EntityManager.AddComponentData(_localPlayerEntity, input);
        }

        /// <summary>
        /// Issue a stop command (called from RTSInput).
        /// </summary>
        public void IssueStopCommand(int entityNetworkId)
        {
            if (_localPlayerEntity == Entity.Null) return;

            var input = new NetworkCommandInput
            {
                Type = CommandType.Stop,
                TargetEntityNetworkId = entityNetworkId
            };

            EntityManager.AddComponentData(_localPlayerEntity, input);
        }

        /// <summary>
        /// Issue a build command (called from RTSInput).
        /// </summary>
        public void IssueBuildCommand(int builderNetworkId, string buildingId, float3 position)
        {
            if (_localPlayerEntity == Entity.Null) return;

            var input = new NetworkCommandInput
            {
                Type = CommandType.Build,
                TargetEntityNetworkId = builderNetworkId,
                Destination = position,
                BuildingId = new FixedString64Bytes(buildingId)
            };

            EntityManager.AddComponentData(_localPlayerEntity, input);
        }

        /// <summary>
        /// Issue a gather command (called from RTSInput).
        /// </summary>
        public void IssueGatherCommand(int minerNetworkId, int resourceNodeNetworkId, int depositLocationNetworkId)
        {
            if (_localPlayerEntity == Entity.Null) return;

            var input = new NetworkCommandInput
            {
                Type = CommandType.Gather,
                TargetEntityNetworkId = minerNetworkId,
                SecondaryTargetNetworkId = resourceNodeNetworkId,
                // We'd need another field for deposit location, or encode it differently
                // For now, using the destination float3 to pass the deposit entity ID (hack)
                Destination = new float3(depositLocationNetworkId, 0, 0)
            };

            EntityManager.AddComponentData(_localPlayerEntity, input);
        }

        /// <summary>
        /// Issue a heal command (called from RTSInput).
        /// </summary>
        public void IssueHealCommand(int healerNetworkId, int targetNetworkId)
        {
            if (_localPlayerEntity == Entity.Null) return;

            var input = new NetworkCommandInput
            {
                Type = CommandType.Heal,
                TargetEntityNetworkId = healerNetworkId,
                SecondaryTargetNetworkId = targetNetworkId
            };

            EntityManager.AddComponentData(_localPlayerEntity, input);
        }
    }
}
