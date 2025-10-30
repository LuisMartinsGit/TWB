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

            Debug.Log("╔═══════════════════════════════════════╗");
            Debug.Log("║   BOOTSTRAP STARTING                  ║");
            Debug.Log("╚═══════════════════════════════════════╝");

            // *** CRITICAL: Initialize TechTreeDB FIRST ***
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
                Debug.Log("✓ BarracksPanel added successfully");
            }
            
            TryAddComponent<TheWaningBorder.UI.ResourceHUD_IMGUI>(go);

            // Ensure FoW exists
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            if (fow == null)
                FogOfWarManager.SetupFogOfWar();

            SyncFoWToTerrain();
            
            Debug.Log("╔═══════════════════════════════════════╗");
            Debug.Log("║   BOOTSTRAP COMPLETE                  ║");
            Debug.Log("╚═══════════════════════════════════════╝");
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTO-INITIALIZE TECHTREEDB
        // ═══════════════════════════════════════════════════════════════
        private static void EnsureTechTreeDB()
        {
            Debug.Log("[Bootstrap] Initializing TechTreeDB...");

            // Check if already exists
            if (TechTreeDB.Instance != null)
            {
                Debug.Log("[Bootstrap] ✓ TechTreeDB already exists");
                return;
            }

            // Look for TechTreeDBAuthoring in scene (might have been added manually)
            var authoring = Object.FindObjectOfType<TechTreeDBAuthoring>();
            if (authoring != null)
            {
                Debug.Log("[Bootstrap] ✓ Found TechTreeDBAuthoring in scene");
                return;
            }

            // Auto-create TechTreeDB
            Debug.Log("[Bootstrap] Creating TechTreeDB automatically...");

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
                    Debug.Log($"[Bootstrap] ✓ Found TechTree.json at Resources/{path}");
                    break;
                }
            }

            if (json == null)
            {
                Debug.LogError("╔═══════════════════════════════════════════════════╗");
                Debug.LogError("║  ERROR: TechTree.json NOT FOUND!                  ║");
                Debug.LogError("╚═══════════════════════════════════════════════════╝");
                Debug.LogError("Place TechTree.json in one of these locations:");
                Debug.LogError("  - Assets/Resources/TechTree.json");
                Debug.LogError("  - Assets/Resources/Data/TechTree.json");
                Debug.LogError("OR add TechTreeDBAuthoring to your scene manually");
                return;
            }

            // Create TechTreeDB GameObject
            var go = new GameObject("TechTreeDB_Auto");
            var db = go.AddComponent<TechTreeDB>();
            db.humanTechJson = json;
            Object.DontDestroyOnLoad(go);

            Debug.Log("[Bootstrap] ✓ TechTreeDB created successfully!");

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

            Debug.Log($"[FoW] Synced to terrain. Bounds {fow.WorldMin} → {fow.WorldMax}");
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
                Debug.LogError($"[Bootstrap] ✗ Failed to add {typeof(T).Name}: {e.Message}");
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
            Debug.Log("╔═══════════════════════════════════════════════════╗");
            Debug.Log("║        TECHTREEDB VERIFICATION                    ║");
            Debug.Log("╚═══════════════════════════════════════════════════╝");

            if (TechTreeDB.Instance == null)
            {
                Debug.LogError("✗ TechTreeDB.Instance is STILL NULL!");
                Debug.LogError("  TechTreeDB.Awake() failed or didn't run");
                return;
            }

            Debug.Log("✓ TechTreeDB.Instance exists");

            // Check Barracks building
            if (TechTreeDB.Instance.TryGetBuilding("Barracks", out var barracks))
            {
                Debug.Log("✓ Barracks building found in JSON");
                Debug.Log($"  - HP: {barracks.hp}");
                Debug.Log($"  - LineOfSight: {barracks.lineOfSight}");

                if (barracks.trains == null)
                {
                    Debug.LogError("✗ Barracks.trains is NULL!");
                    Debug.LogError("  BuildingDef class doesn't match JSON");
                    Debug.LogError("  Solution: Replace TechTreeDb.cs with TechTreeDb_FIXED.cs");
                }
                else if (barracks.trains.Length == 0)
                {
                    Debug.LogError("✗ Barracks.trains is EMPTY!");
                }
                else
                {
                    Debug.Log($"✓ Barracks.trains array has {barracks.trains.Length} units:");
                    foreach (var unitId in barracks.trains)
                    {
                        Debug.Log($"    - {unitId}");
                    }
                }
            }
            else
            {
                Debug.LogError("✗ Barracks NOT found in TechTreeDB!");
                Debug.LogError("  Check your TechTree.json structure");
            }

            // Check units
            if (TechTreeDB.Instance.TryGetUnit("Swordsman", out var swordsman))
            {
                Debug.Log($"✓ Swordsman unit found (TrainTime: {swordsman.trainingTime}s)");
            }
            else
            {
                Debug.LogWarning("⚠ Swordsman unit NOT found");
            }

            if (TechTreeDB.Instance.TryGetUnit("Archer", out var archer))
            {
                Debug.Log($"✓ Archer unit found (TrainTime: {archer.trainingTime}s)");
            }
            else
            {
                Debug.LogWarning("⚠ Archer unit NOT found");
            }

            Debug.Log("╚═══════════════════════════════════════════════════╝");
            
            // Destroy this verifier after checking
            Destroy(this);
        }
    }
}