using UnityEngine;
using UnityEngine.SceneManagement;
using TheWaningBorder.Core.Settings;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.UI.MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        private Rect _windowRect = new Rect(50, 50, 400, 500);
        private bool _showMenu = true;
        
        void OnGUI()
        {
            if (!_showMenu) return;
            
            _windowRect = GUI.Window(0, _windowRect, DrawMenu, "The Waning Border - Main Menu");
        }
        
        void DrawMenu(int windowID)
        {
            GUILayout.Label("Game Settings", GUI.skin.box);
            
            GUILayout.Space(10);
            
            // Game Mode
            GUILayout.Label("Game Mode:");
            if (GUILayout.Button(GameSettings.Mode.ToString()))
            {
                GameSettings.Mode = GameSettings.Mode == GameManager.GameMode.SoloVsCurse 
                    ? GameManager.GameMode.FreeForAll 
                    : GameManager.GameMode.SoloVsCurse;
            }
            
            // Player Count
            GUILayout.Label($"Players: {GameSettings.TotalPlayers}");
            GameSettings.TotalPlayers = (int)GUILayout.HorizontalSlider(GameSettings.TotalPlayers, 2, 8);
            
            // Map Size
            GUILayout.Label($"Map Size: {GameSettings.MapHalfSize * 2}");
            GameSettings.MapHalfSize = (int)GUILayout.HorizontalSlider(GameSettings.MapHalfSize, 64, 256);
            
            // Fog of War
            GameSettings.FogOfWarEnabled = GUILayout.Toggle(GameSettings.FogOfWarEnabled, "Enable Fog of War");
            
            // Spawn Layout
            GUILayout.Label("Spawn Layout:");
            if (GUILayout.Button(GameSettings.SpawnLayout.ToString()))
            {
                int current = (int)GameSettings.SpawnLayout;
                current = (current + 1) % 4;
                GameSettings.SpawnLayout = (SpawnLayout)current;
            }
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("Start Game", GUILayout.Height(40)))
            {
                _showMenu = false;
                StartGame();
            }
            
            if (GUILayout.Button("Quit", GUILayout.Height(30)))
            {
                Application.Quit();
            }
            
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
        
        void StartGame()
        {
            SceneManager.LoadScene("Game");
        }
    }
}