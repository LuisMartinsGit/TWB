// Bootstrap_COMPLETE_FIX.cs
// This version auto-initializes TechTreeDB so you don't need to add it to the scene
// Replace your Bootstrap.cs with this

using UnityEngine;
using UnityEngine.SceneManagement;
using TheWaningBorder.GameCamera;
using TheWaningBorder.Gameplay;

namespace CrystallineRTS.Bootstrap
{
    public static class GameBootstrap
    {
        private static bool _didSetupThisScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedHandler;
            SceneManager.sceneLoaded += OnSceneLoadedHandler;
            OnSceneLoadedHandler(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, "Game")) return;
            if (_didSetupThisScene) return;
            _didSetupThisScene = true;
            EnsureTechTreeDB();                             

            GameCamera.Ensure();

            var go = new GameObject("RTS_Bootstrap");

            TryAddComponent<MinimapFlat>(go);
            TryAddComponent<RTSInput>(go);
            TryAddComponent<SelectionDecalManager>(go);
            TryAddComponent<ProceduralTerrain>(go);
            TryAddComponent<HealthbarOverlay>(go);
            
            HumanFaction.GeneratePlayers(GameSettings.TotalPlayers); 
            EconomyBootstrap.EnsureFactionBanks(GameSettings.TotalPlayers);
            
            TryAddComponent<EntityViewManager>(go);
            TryAddComponent<BuilderCommandPanel>(go);
            
            // Add BarracksPanel
            var barracksPanel = TryAddComponent<BarracksPanel>(go);
            if (barracksPanel != null)
            {

            }
            
            TryAddComponent<TheWaningBorder.UI.ResourceHUD_IMGUI>(go);

            // Ensure FoW exists
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            if (fow == null)
                FogOfWarManager.SetupFogOfWar();

            SyncFoWToTerrain();

        }

        // ═══════════════════════════════════════════════════════════════
        // AUTO-INITIALIZE TECHTREEDB
        // ═══════════════════════════════════════════════════════════════
        private static void EnsureTechTreeDB()
        {

            // Check if already exists
            if (TechTreeDB.Instance != null)
            {

                return;
            }

            // Look for TechTreeDBAuthoring in scene (might have been added manually)
            var authoring = Object.FindObjectOfType<TechTreeDBAuthoring>();
            if (authoring != null)
            {

                return;
            }

            // Auto-create TechTreeDB

            // Try multiple possible locations for the JSON file
            TextAsset json = null;
            
            // Try common locations
            string[] possiblePaths = {
                "TechTree",           // Resources/TechTree.json
                "Data/TechTree",      // Resources/Data/TechTree.json
                "JSON/TechTree",      // Resources/JSON/TechTree.json
                "Config/TechTree"     // Resources/Config/TechTree.json
            };

            foreach (var path in possiblePaths)
            {
                json = Resources.Load<TextAsset>(path);
                if (json != null)
                {

                    break;
                }
            }

            if (json == null)
            {

                return;
            }

            // Create TechTreeDB GameObject
            var go = new GameObject("TechTreeDB_Auto");
            var db = go.AddComponent<TechTreeDB>();
            db.humanTechJson = json;
            Object.DontDestroyOnLoad(go);

            // Wait a frame for Awake to run, then verify
            var verifier = go.AddComponent<TechTreeDBVerifier>();
        }

        static void SyncFoWToTerrain()
        {
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            var terrain = Terrain.activeTerrain;
            if (fow == null || terrain == null) return;

            var td = terrain.terrainData;
            var tpos = terrain.transform.position;
            var tsize = td.size;

            Vector2 newMin = new Vector2(tpos.x, tpos.z);
            Vector2 newMax = new Vector2(tpos.x + tsize.x, tpos.z + tsize.z);

            fow.WorldMin = newMin;
            fow.WorldMax = newMax;
            fow.ForceRebuildGrid(clearRevealed: false);

            if (fow.FogRenderer != null)
                Object.Destroy(fow.FogRenderer.gameObject);

            var mat = fow.FogMaterial != null ? fow.FogMaterial : new Material(Shader.Find("Unlit/FogOfWar"));
            var fogSurface = FogOfWarConformingMesh.Create(fow.WorldMin, fow.WorldMax, 128, mat);
            fogSurface.name = "FogSurface";
            fogSurface.transform.SetParent(fow.transform, false);
            fow.FogRenderer = fogSurface.GetComponent<MeshRenderer>();

            var mi = typeof(FogOfWarManager).GetMethod("EnsureMaterialBound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            mi?.Invoke(fow, null);
            var pi = typeof(FogOfWarManager).GetMethod("PushHumanTexture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi?.Invoke(fow, null);

        }

        private static T TryAddComponent<T>(GameObject go) where T : Component
        {
            try 
            { 
                var comp = go.AddComponent<T>();
                return comp;
            }
            catch (System.Exception e)
            {

                return null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TECHTREEDB VERIFIER - Logs status after initialization
    // ═══════════════════════════════════════════════════════════════
    public class TechTreeDBVerifier : MonoBehaviour
    {
        void Start()
        {
            Invoke(nameof(Verify), 0.5f);
        }

        void Verify()
        {

            if (TechTreeDB.Instance == null)
            {

                return;
            }

            // Check Barracks building
            if (TechTreeDB.Instance.TryGetBuilding("Barracks", out var barracks))
            {

                if (barracks.trains == null)
                {

                }
                else if (barracks.trains.Length == 0)
                {

                }
                else
                {

                    foreach (var unitId in barracks.trains)
                    {

                    }
                }
            }
            else
            {

            }

            // Check units
            if (TechTreeDB.Instance.TryGetUnit("Swordsman", out var swordsman))
            {

            }
            else
            {

            }

            if (TechTreeDB.Instance.TryGetUnit("Archer", out var archer))
            {

            }
            else
            {

            }

            // Destroy this verifier after checking
            Destroy(this);
        }
    }
}