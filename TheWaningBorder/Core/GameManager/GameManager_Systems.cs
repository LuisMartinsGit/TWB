using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.Settings;

namespace TheWaningBorder.Core.GameManager
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GameStateSystem : SystemBase
    {
        private Entity _gameStateEntity;
        
        protected override void OnCreate()
        {
            _gameStateEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(_gameStateEntity, new GameStateComponent
            {
                CurrentEra = 1,
                GameTime = 0f,
                IsPaused = false,
                Mode = GameSettings.Mode
            });
        }
        
        protected override void OnUpdate()
        {
            if (!EntityManager.HasComponent<GameStateComponent>(_gameStateEntity))
                return;
            
            var gameState = EntityManager.GetComponentData<GameStateComponent>(_gameStateEntity);
            
            if (!gameState.IsPaused)
            {
                gameState.GameTime += SystemAPI.Time.DeltaTime;
                EntityManager.SetComponentData(_gameStateEntity, gameState);
            }
        }
        
        public void SetPaused(bool paused)
        {
            var gameState = EntityManager.GetComponentData<GameStateComponent>(_gameStateEntity);
            gameState.IsPaused = paused;
            EntityManager.SetComponentData(_gameStateEntity, gameState);
        }
        
        public void AdvanceEra()
        {
            var gameState = EntityManager.GetComponentData<GameStateComponent>(_gameStateEntity);
            if (gameState.CurrentEra < 5)
            {
                gameState.CurrentEra++;
                EntityManager.SetComponentData(_gameStateEntity, gameState);
                Debug.Log($"[GameState] Advanced to Era {gameState.CurrentEra}");
            }
        }
    }
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TimeSystem : SystemBase
    {
        private float _lastUpdateTime;
        
        protected override void OnCreate()
        {
            _lastUpdateTime = 0f;
        }
        
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Update time for all entities that need it
            _lastUpdateTime = currentTime;
        }
        
        public float GetGameTime() => _lastUpdateTime;
    }
}
