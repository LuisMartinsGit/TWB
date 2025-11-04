using UnityEngine;
using UnityEngine.SceneManagement;
using TheWaningBorder.Map;
using TheWaningBorder.Player;
using TheWaningBorder.Factions.Humans;


/*
#############
Known issues:
#############

MINIMAP
[CRITICAL] Minimap click no longer moves camera
[VISUAL]   Minimap camera shape not correct when near map edges (consider 5 points or 6)

HEALTHBAR
[IMPORTANT] All Enemy building healthbars are visible, when the building is hidden by FOW
[IMPORTANT] All Enemy building healthbars are visible, when the building is hidden by FOW and user hovers over building
[IMPORTANT] All Enemy units healthbars are visible, when the unit is hidden by FOW and user hovers over unit

UNITS
[CRITICAL] Move Command does not work if unit is engaged in combat.
[CRITICAL] unit movement is flickery when many units are grouped together.
[CRITICAL] UNits can move beyond Map Boundaries

GAIA
[VISUAL] Iron ourtrop missing prefab 

*/

namespace TheWaningBorder.Gameplay
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
            HumanTech.EnsureTechTreeDB(); // OK
            // Other tech trees go here.
            GameCamera.Ensure();          // OK

            var go = new GameObject("RTS_Bootstrap");

            go.AddComponent<ProceduralTerrain>(); // Namespace Map.Terrain -> Change from ProceduralTerrain to Terrain
            go.AddComponent<Minimap>();           // NOK

            go.AddComponent<EntityViewManager>();    

            go.AddComponent<Controls>();          // NOK
            go.AddComponent<SelectionDecalManager>(); // Namespace Player.Selection -> Change from SelectionDecalManager to SelectionManager
            go.AddComponent<HealthbarOverlay>();
            go.AddComponent<UnifiedUIManager>();
            
            go.AddComponent<BuilderCommandPanel>(); // must disapear into human faction stuff
            go.AddComponent<UI.ResourceHUD_IMGUI>();// must disappear into unifiedUIManager

            HumanFaction.GeneratePlayers(GameSettings.TotalPlayers);
            EconomyBootstrap.EnsureFactionBanks(GameSettings.TotalPlayers);

            // TO DO: CURSE!

            FogOfWarManager.SetupFogOfWar();

        }
    }
}