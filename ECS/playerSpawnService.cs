// PlayerSpawnSystem.cs
// Add this new file to your ECS folder

using Unity.Entities;
using UnityEngine;
using TheWaningBorder.Gameplay;

namespace CrystallineRTS.Bootstrap
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial struct PlayerSpawnSystem : ISystem
    {
        private bool _hasSpawned;

        public void OnCreate(ref SystemState state)
        {
            _hasSpawned = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_hasSpawned) return;
            
            _hasSpawned = true;

            int playerCount = Mathf.Clamp(GameSettings.TotalPlayers, 2, 10);
            
            if (playerCount <= 0)
            {

                return;
            }

            HumanFaction.GeneratePlayers(state.EntityManager, playerCount);
            EconomyBootstrap.EnsureFactionBanks(playerCount);

        }
    }
}