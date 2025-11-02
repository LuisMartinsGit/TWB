using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheWaningBorder.UI
{
    /// <summary>
    /// Pure IMGUI resource HUD.
    /// - Top bar: human player's resources (left, always visible).
    /// - Debug window: all factions (F3 to toggle).
    /// </summary>
    public class ResourceHUD_IMGUI : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private Faction humanFaction = Faction.Blue;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private KeyCode toggleDebugKey = KeyCode.F3;
        [SerializeField] private float topBarHeight = 32f;
        [SerializeField] private float leftPadding = 10f;
        [SerializeField] private float pillSpacing = 10f;
        public static bool IsPointerOverTopBar { get; private set; }        private World _world;
        private EntityManager _em;
        private EntityQuery _banksQuery;

        private readonly Dictionary<Faction, FactionResources> _cache = new();
        private float _timer;
        private bool _showDebug;

        // styles
        private GUIStyle _topBarBg;
        private GUIStyle _pill;
        private GUIStyle _pillText;
        private GUIStyle _debugTitle;
        private Texture2D _texTopBar, _texPill;
        private bool _stylesBuilt = false;
        private Rect _debugRect = new Rect(20, 60, 430, 300);

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {

                return;
            }

            _em = _world.EntityManager;
            _banksQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionResources>());

            // DO create textures here (not GUI-dependent)
            _texTopBar = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f));
            _texPill   = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.08f));

            // DO NOT call BuildStyles() here (it uses GUI.skin)
            RefreshNow();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleDebugKey))
                _showDebug = !_showDebug;

            _timer += Time.unscaledDeltaTime;
            if (_timer >= refreshInterval)
            {
                _timer = 0f;
                RefreshNow();
            }
        }

        private void RefreshNow()
        {
            _cache.Clear();
            var world = _world ?? World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            using var ents = _banksQuery.ToEntityArray(Allocator.Temp);
            using var tags = _banksQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var banks = _banksQuery.ToComponentDataArray<FactionResources>(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
                _cache[tags[i].Value] = banks[i];
        }

        private void OnGUI()
        {
            if (!_stylesBuilt) { BuildStyles(); _stylesBuilt = true; }

            var barRect = new Rect(0, 0, Screen.width, topBarHeight);
            IsPointerOverTopBar = barRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

            DrawTopBar();
            if (_showDebug)
                _debugRect = GUILayout.Window(0xA11733, _debugRect, DrawDebugWindow, "Resources (F3)", _debugTitle);
        }

        private void DrawTopBar()
        {
            var barRect = new Rect(0, 0, Screen.width, topBarHeight);
            GUI.Box(barRect, GUIContent.none, _topBarBg);

            // read human bank
            _cache.TryGetValue(humanFaction, out var me);

            // place “pills”
            float x = leftPadding;
            float y = 2f;
            float h = topBarHeight - 4f;

            DrawPill(ref x, y, 150f, h, $"Supplies  {Format(me.Supplies)}");
            DrawPill(ref x, y, 110f, h, $"Iron  {Format(me.Iron)}");
            DrawPill(ref x, y, 130f, h, $"Crystal  {Format(me.Crystal)}");
            DrawPill(ref x, y, 150f, h, $"Veilsteel  {Format(me.Veilsteel)}");
            DrawPill(ref x, y, 110f, h, $"Glow  {Format(me.Glow)}");
        }

        private void DrawDebugWindow(int id)
        {
            GUILayout.BeginVertical();
            foreach (var kvp in _cache)
            {
                var f = kvp.Key;
                var r = kvp.Value;

                bool bold = f == humanFaction;
                var style = new GUIStyle(GUI.skin.label);
                if (bold) style.fontStyle = FontStyle.Bold;

                GUILayout.Label(
                    $"{f,-7} | Supplies {Format(r.Supplies)}  | Iron {Format(r.Iron)}  | Crystal {Format(r.Crystal)}  | Veilsteel {Format(r.Veilsteel)}  | Glow {Format(r.Glow)}",
                    style);
            }

            if (_cache.Count == 0)
                GUILayout.Label("No resource banks found.");

            GUILayout.FlexibleSpace();
            GUILayout.Label("Tip: press F3 to hide this window.");
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawPill(ref float x, float y, float w, float h, string text)
        {
            var r = new Rect(x, y, w, h);
            GUI.Box(r, GUIContent.none, _pill);

            var tr = new Rect(r.x + 8f, r.y, r.width - 16f, r.height);
            GUI.Label(tr, text, _pillText);

            x += w + pillSpacing;
        }

        private static string Format(int v)
        {
            // basic thousands separator without allocations from string.Format
            return v.ToString("#,0");
        }
        private void BuildStyles()
        {
            // SAFE: this runs inside OnGUI()
            _topBarBg = new GUIStyle(GUI.skin.box) {
                normal = { background = _texTopBar },
                margin  = new RectOffset(0,0,0,0),
                padding = new RectOffset(0,0,0,0),
                border  = new RectOffset(0,0,0,0)
            };

            _pill = new GUIStyle(GUI.skin.box) {
                normal = { background = _texPill },
                margin  = new RectOffset(0,0,0,0),
                padding = new RectOffset(0,0,0,0),
                border  = new RectOffset(8,8,8,8)
            };

            _pillText = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleLeft,
                fontSize  = 14,
                padding   = new RectOffset(0,0,0,0)
            };

            _debugTitle = new GUIStyle(GUI.skin.window) {
                alignment = TextAnchor.UpperLeft
            };
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
