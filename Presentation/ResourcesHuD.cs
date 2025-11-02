// ResourceHUD_IMGUI.cs - WITH POPULATION INTEGRATION
// Adds population display to the top resource bar
// REPLACE your existing ResourceHUD_IMGUI.cs with this version

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheWaningBorder.UI
{
    public class ResourceHUD_IMGUI : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private Faction humanFaction = Faction.Blue;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private KeyCode toggleDebugKey = KeyCode.F3;
        [SerializeField] private float topBarHeight = 32f;
        [SerializeField] private float leftPadding = 10f;
        [SerializeField] private float pillSpacing = 10f;
        
        public static bool IsPointerOverTopBar { get; private set; }
        
        private World _world;
        private EntityManager _em;
        private EntityQuery _banksQuery;
        private EntityQuery _populationQuery; // NEW

        private readonly Dictionary<Faction, FactionResources> _cache = new();
        private readonly Dictionary<Faction, (int current, int max)> _popCache = new(); // NEW
        private float _timer;
        private bool _showDebug;

        // Styles
        private GUIStyle _topBarBg;
        private GUIStyle _pillBg;
        private GUIStyle _pillText;
        private GUIStyle _debugTitle;
        private Texture2D _texTopBar, _texPill;
        private bool _stylesBuilt = false;
        private Rect _debugRect = new Rect(20, 60, 500, 350); // Slightly wider for population

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
            
            // NEW: Create population query
            _populationQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionPopulation>());

            // Create textures
            _texTopBar = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f));
            _texPill   = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.08f));

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
            _popCache.Clear(); // NEW
            
            var world = _world ?? World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // Refresh resources
            using var ents = _banksQuery.ToEntityArray(Allocator.Temp);
            using var tags = _banksQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var banks = _banksQuery.ToComponentDataArray<FactionResources>(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
                _cache[tags[i].Value] = banks[i];
            
            // NEW: Refresh population
            using var popTags = _populationQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var populations = _populationQuery.ToComponentDataArray<FactionPopulation>(Allocator.Temp);
            
            for (int i = 0; i < popTags.Length; i++)
                _popCache[popTags[i].Value] = (populations[i].Current, populations[i].Max);
        }

        private void OnGUI()
        {
            if (!_stylesBuilt) { BuildStyles(); _stylesBuilt = true; }

            var barRect = new Rect(0, 0, Screen.width, topBarHeight);
            IsPointerOverTopBar = barRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

            DrawTopBar();
            if (_showDebug)
                _debugRect = GUILayout.Window(0xA11733, _debugRect, DrawDebugWindow, "Resources & Population (F3)", _debugTitle);
        }

        private void DrawTopBar()
        {
            var barRect = new Rect(0, 0, Screen.width, topBarHeight);
            GUI.Box(barRect, GUIContent.none, _topBarBg);

            // Read human resources
            _cache.TryGetValue(humanFaction, out var me);
            
            // NEW: Read human population
            _popCache.TryGetValue(humanFaction, out var pop);
            int currentPop = pop.current;
            int maxPop = pop.max;
            bool atCap = maxPop >= FactionPopulation.AbsoluteMax;

            // Place "pills"
            float x = leftPadding;
            float y = 2f;
            float h = topBarHeight - 4f;

            // NEW: Population pill (first for visibility)
            Color popColor = Color.white;
            if (currentPop >= maxPop && maxPop > 0)
                popColor = new Color(1f, 0.3f, 0.3f); // Red - no capacity
            else if (atCap)
                popColor = new Color(1f, 0.8f, 0f); // Orange - at hard cap
            
            string popText = atCap 
                ? $"👥 Pop  {currentPop}/{maxPop} MAX" 
                : $"👥 Pop  {currentPop}/{maxPop}";
            
            DrawColoredPill(ref x, y, 170f, h, popText, popColor);

            // Existing resources
            DrawPill(ref x, y, 150f, h, $"📦 Supplies  {Format(me.Supplies)}");
            DrawPill(ref x, y, 110f, h, $"⚙️ Iron  {Format(me.Iron)}");
            DrawPill(ref x, y, 130f, h, $"💎 Crystal  {Format(me.Crystal)}");
            DrawPill(ref x, y, 150f, h, $"⚫ Veilsteel  {Format(me.Veilsteel)}");
            DrawPill(ref x, y, 110f, h, $"✨ Glow  {Format(me.Glow)}");
        }

        private void DrawDebugWindow(int id)
        {
            GUILayout.BeginVertical();
            
            // Show all factions' resources and population
            foreach (var kvp in _cache)
            {
                var f = kvp.Key;
                var r = kvp.Value;
                
                // Get population for this faction
                _popCache.TryGetValue(f, out var pop);

                bool bold = f == humanFaction;
                var style = new GUIStyle(GUI.skin.label);
                if (bold) style.fontStyle = FontStyle.Bold;

                GUILayout.Label(
                    $"{f,-7} | Pop {pop.current,3}/{pop.max,3} | " +
                    $"Supplies {Format(r.Supplies),5} | Iron {Format(r.Iron),5} | " +
                    $"Crystal {Format(r.Crystal),5} | Veilsteel {Format(r.Veilsteel),5} | " +
                    $"Glow {Format(r.Glow),5}",
                    style);
            }

            if (_cache.Count == 0)
                GUILayout.Label("No resource banks found.");

            GUILayout.Space(8);
            
            // NEW: Show population limits
            GUILayout.Label($"Population Cap: {FactionPopulation.AbsoluteMax}", GUI.skin.box);
            GUILayout.Label("🏠 Hut: +10 pop  |  🏛️ Hall: +20 pop", GUI.skin.box);

            GUILayout.FlexibleSpace();
            GUILayout.Label("Tip: press F3 to hide this window.", GUI.skin.box);
            GUI.DragWindow();
            
            GUILayout.EndVertical();
        }

        // NEW: Helper method to draw colored pills
        private void DrawColoredPill(ref float x, float y, float w, float h, string text, Color color)
        {
            var rect = new Rect(x, y, w, h);
            
            // Save original color
            var oldColor = GUI.color;
            
            // Apply color tint to background
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, _pillBg);
            
            // Restore color for text
            GUI.color = oldColor;
            
            // Draw text with color
            var textStyle = new GUIStyle(_pillText);
            if (color != Color.white)
                textStyle.normal.textColor = color;
                
            GUI.Label(rect, text, textStyle);
            x += w + pillSpacing;
        }

        private void DrawPill(ref float x, float y, float w, float h, string text)
        {
            DrawColoredPill(ref x, y, w, h, text, Color.white);
        }

        private void BuildStyles()
        {
            _topBarBg = new GUIStyle(GUI.skin.box);
            _topBarBg.normal.background = _texTopBar;

            _pillBg = new GUIStyle(GUI.skin.box);
            _pillBg.normal.background = _texPill;

            _pillText = new GUIStyle(GUI.skin.label);
            _pillText.alignment = TextAnchor.MiddleLeft;
            _pillText.fontSize = 13;
            _pillText.fontStyle = FontStyle.Bold;
            _pillText.normal.textColor = Color.white;
            _pillText.padding = new RectOffset(8, 8, 0, 0);

            _debugTitle = new GUIStyle(GUI.skin.label);
            _debugTitle.fontStyle = FontStyle.Bold;
            _debugTitle.normal.textColor = Color.white;
        }

        private string Format(int value)
        {
            if (value >= 1000000)
                return $"{value / 1000000.0:F1}M";
            if (value >= 1000)
                return $"{value / 1000.0:F1}K";
            return value.ToString();
        }

        private Texture2D MakeTex(int w, int h, Color c)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = c;
            var t = new Texture2D(w, h);
            t.SetPixels(pix);
            t.Apply();
            return t;
        }
    }
}

/*
 * INTEGRATION INSTRUCTIONS:
 * 
 * 1. BACKUP your existing ResourceHUD_IMGUI.cs
 * 2. Replace it entirely with this version
 * 3. Key changes:
 *    - Added _populationQuery and _popCache for tracking population
 *    - Added DrawColoredPill() method for colored population display
 *    - Population displayed first in the top bar with color coding:
 *      * White: Normal
 *      * Red: At capacity (can't train units)
 *      * Orange: At absolute max (200)
 *    - Debug window now shows population for all factions
 *    - Added helpful hints about population sources
 * 
 * 4. Make sure you have:
 *    - FactionPopulation component added to faction banks
 *    - CalculatePopulationSystem running
 */