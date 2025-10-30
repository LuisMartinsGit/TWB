// Assets/Scripts/MenuBootstrap.cs (replace file)
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class MenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureMenuSceneSetup()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Game") return; // Game scene handled by GameBootstrap

        EnsureMenuCamera();

        if (Object.FindObjectOfType<MainMenu>() == null)
        {
            var go = new GameObject("MainMenu");
            go.AddComponent<MainMenu>();
        }
    }

    public static void EnsureMenuCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("MenuCamera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
        }

        cam.enabled = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.10f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane  = 2000f;

        if (cam.GetComponent<AudioListener>() == null)
            cam.gameObject.AddComponent<AudioListener>();

        foreach (var l in Object.FindObjectsOfType<AudioListener>())
            if (l != null && l.gameObject != cam.gameObject)
                Object.Destroy(l);

        cam.transform.position = new Vector3(0, 10, -10);
        cam.transform.rotation = Quaternion.Euler(20, 0, 0);
    }
}

public class MainMenu : MonoBehaviour
{
    const string GameSceneName = "Game"; // must match your gameplay scene name

    // UI state mirrors GameSettings defaults
    int _players = Mathf.Clamp(GameSettings.TotalPlayers, 2, 8);
    GameMode _mode = GameSettings.Mode;
    bool _fow = GameSettings.FogOfWarEnabled;
    int _mapHalf = Mathf.Clamp(GameSettings.MapHalfSize, 64, 512);

    Rect _win = new Rect(40, 40, 420, 480);
    string _error;

    // NEW:
    SpawnLayout _layout = GameSettings.SpawnLayout;
    TwoSidesPreset _twoSides = GameSettings.TwoSides;
    int _spawnSeed = GameSettings.SpawnSeed;

    void Awake()
    {
        MenuBootstrap.EnsureMenuCamera();
    }

    void OnGUI()
    {
        _win = GUI.Window(10001, _win, Draw, "RTS — Setup");
        if (!string.IsNullOrEmpty(_error))
        {
            GUI.color = new Color(1, 0.5f, 0.5f, 1);
            GUI.Box(new Rect(_win.x, _win.yMax + 8, _win.width, 60), _error);
            GUI.color = Color.white;
        }
    }

    void Draw(int id)
    {
        // --- Game Mode ---
        GUILayout.Label("<b>Game Mode</b>", Rich());
        GUILayout.BeginHorizontal();
        bool solo = _mode == GameMode.SoloVsCurse;
        if (GUILayout.Toggle(solo, " Single Player (vs Curse)", "Button")) _mode = GameMode.SoloVsCurse;
        if (GUILayout.Toggle(!solo, " Free For All", "Button")) _mode = GameMode.FreeForAll;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // --- Players (only for FFA) ---
        using (new GUIEnabledScope(_mode == GameMode.FreeForAll))
        {
            GUILayout.Label("Total Players (including you):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40))) _players = Mathf.Max(2, _players - 1);
            GUILayout.Label(_players.ToString(), GUILayout.Width(40));
            if (GUILayout.Button(" + ", GUILayout.Width(40))) _players = Mathf.Min(8, _players + 1);
            GUILayout.EndHorizontal();
        }

        

        GUILayout.Space(8);
        GUILayout.Label("<b>Spawn Layout</b>", Rich());
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_layout == SpawnLayout.Circle, " Circle", "Button")) _layout = SpawnLayout.Circle;
        if (GUILayout.Toggle(_layout == SpawnLayout.TwoSides, " Two Sides (4)", "Button")) _layout = SpawnLayout.TwoSides;
        if (GUILayout.Toggle(_layout == SpawnLayout.TwoEachSide8, " 2 Each Side (8)", "Button")) _layout = SpawnLayout.TwoEachSide8;
        GUILayout.EndHorizontal();

        if (_layout == SpawnLayout.TwoSides)
        {
            GUILayout.Space(4);
            GUILayout.Label("Two Sides Preset:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftRight,  " LR", "Button")) _twoSides = TwoSidesPreset.LeftRight;
            if (GUILayout.Toggle(_twoSides == TwoSidesPreset.UpDown,     " UD", "Button")) _twoSides = TwoSidesPreset.UpDown;
            if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftUp,     " LU", "Button")) _twoSides = TwoSidesPreset.LeftUp;
            if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftDown,   " LD", "Button")) _twoSides = TwoSidesPreset.LeftDown;
            if (GUILayout.Toggle(_twoSides == TwoSidesPreset.RightUp,    " RU", "Button")) _twoSides = TwoSidesPreset.RightUp;
            if (GUILayout.Toggle(_twoSides == TwoSidesPreset.RightDown,  " RD", "Button")) _twoSides = TwoSidesPreset.RightDown;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Spawn Seed:");
        string seedStr = GUILayout.TextField(_spawnSeed.ToString(), GUILayout.Width(90));
        if (int.TryParse(seedStr, out int parsed)) _spawnSeed = parsed;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        // --- Fog of War ---
        GUILayout.Label("<b>Fog of War</b>", Rich());
        _fow = GUILayout.Toggle(_fow, _fow ? " Enabled" : " Disabled");

        GUILayout.Space(6);

        // --- Map Size ---
        GUILayout.Label("<b>Map Size</b> (total side ≈ 2×HalfSize)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(" - ", GUILayout.Width(40))) _mapHalf = Mathf.Max(64, _mapHalf - 16);
        GUILayout.Label($"Half Size: {_mapHalf}  (Total ≈ {_mapHalf*2})", GUILayout.Width(240));
        if (GUILayout.Button(" + ", GUILayout.Width(40))) _mapHalf = Mathf.Min(512, _mapHalf + 16);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("You = Blue. Others are AI and hostile to each other.");

        GUILayout.Space(10);
        if (GUILayout.Button("Start Game", GUILayout.Height(36)))
        {
            // Apply settings
            GameSettings.Mode = _mode;
            GameSettings.FogOfWarEnabled = _fow;
            GameSettings.MapHalfSize = _mapHalf;

            if (_mode == GameMode.FreeForAll)
                GameSettings.TotalPlayers = Mathf.Clamp(_players, 2, 8);
            else
                GameSettings.TotalPlayers = 1;

            // NEW:
            GameSettings.SpawnLayout = _layout;
            GameSettings.TwoSides = _twoSides;
            GameSettings.SpawnSeed = _spawnSeed;

            // Load scene (as before)...
            int idx = FindSceneIndexByName(GameSceneName);
            if (idx < 0) { _error = "..."; }
            else SceneManager.LoadScene(idx);
        }

        GUI.DragWindow(new Rect(0,0,10000,20));
    }

    static int FindSceneIndexByName(string name)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(sceneName, name))
                return i;
        }
        return -1;
    }

    static GUIStyle _rich;
    static GUIStyle Rich()
    {
        if (_rich == null) _rich = new GUIStyle(GUI.skin.label){ richText = true, wordWrap = true };
        return _rich;
    }

    sealed class GUIEnabledScope : System.IDisposable
    {
        bool _prev;
        public GUIEnabledScope(bool enabled)
        {
            _prev = GUI.enabled;
            GUI.enabled = enabled;
        }
        public void Dispose(){ GUI.enabled = _prev; }
    }
}
