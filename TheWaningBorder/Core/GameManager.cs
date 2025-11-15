using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheWaningBorder.Core
{
    /// <summary>
    /// Main game manager MonoBehaviour for Unity scene integration
    /// Renamed from GameManager to GameManagerMono to avoid namespace conflicts
    /// </summary>
    public class GameManagerMono : MonoBehaviour
    {
        public static GameManagerMono Instance { get; private set; }
        
        [Header("Game Settings")]
        public bool enableFogOfWar = true;
        public int maxPlayers = 8;
        public float gameSpeed = 1.0f;
        
        [Header("Map Settings")]
        public int mapWidth = 256;
        public int mapHeight = 256;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeGame();
        }
        
        private void InitializeGame()
        {
            Debug.Log("[GameManagerMono] Initializing game...");
            
            // The actual ECS initialization happens through the Bootstrap system
            // This MonoBehaviour is just for Unity scene integration
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        public void SetGameSpeed(float speed)
        {
            gameSpeed = Mathf.Clamp(speed, 0f, 3f);
            Time.timeScale = gameSpeed;
        }
        
        public void PauseGame()
        {
            Time.timeScale = 0f;
        }
        
        public void ResumeGame()
        {
            Time.timeScale = gameSpeed;
        }
    }
}
