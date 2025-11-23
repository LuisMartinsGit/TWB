// ResourceHUD_IMGUI.cs - Multi-Faction Resource Display with Population
// Shows all active factions' resources in colored top bars

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
        [SerializeField] private float topBarHeight = 32f;
        [SerializeField] private float leftPadding = 10f;
        [SerializeField] private float pillSpacing = 10f;

        public static bool IsPointerOverTopBar { get; private set; }

        private World _world;
        private EntityManager _em;
        private EntityQuery _banksQuery;
        private EntityQuery _populationQuery;

        private readonly Dictionary<Faction, FactionResources> _cache = new();
        private readonly Dictionary<Faction, (int current, int max)> _popCache = new();
        private float _timer;

        // Styles
        private GUIStyle _topBarBg;
        private GUIStyle _pillBg;
        private GUIStyle _pillText;
        private Texture2D _texTopBar, _texPill;
        private bool _stylesBuilt = false;

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

            _populationQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionPopulation>());

            // Create textures
            _texTopBar = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f));
            _texPill = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.08f));

            RefreshNow();
        }

        private void Update()
        {
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
            _popCache.Clear();

            var world = _world ?? World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // Refresh resources
            using var ents = _banksQuery.ToEntityArray(Allocator.Temp);
            using var tags = _banksQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var banks = _banksQuery.ToComponentDataArray<FactionResources>(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
                _cache[tags[i].Value] = banks[i];

            // Refresh population
            using var popTags = _populationQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var populations = _populationQuery.ToComponentDataArray<FactionPopulation>(Allocator.Temp);

            for (int i = 0; i < popTags.Length; i++)
                _popCache[popTags[i].Value] = (populations[i].Current, populations[i].Max);
        }

        private void OnGUI()
        {
            if (!_stylesBuilt) BuildStyles();

            // Show all active factions in top bar
            DrawAllFactionsTopBar();
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

            _stylesBuilt = true;
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

        private void DrawAllFactionsTopBar()
        {
            float yOffset = 0f;
            int factionCount = 0;

            // Draw each faction's resources
            foreach (var faction in _cache.Keys)
            {
                DrawFactionBar(faction, yOffset);
                yOffset += topBarHeight + 4f; // 4px spacing between bars
                factionCount++;

                if (factionCount >= 8) break; // Max 8 factions
            }

            // Update pointer detection for all bars
            IsPointerOverTopBar = Input.mousePosition.y >= Screen.height - (topBarHeight + 4f) * factionCount;
        }

        private void DrawFactionBar(Faction faction, float yOffset)
        {
            if (!_cache.TryGetValue(faction, out var res))
                return;

            // Get population
            int curPop = 0, maxPop = 0;
            if (_popCache.TryGetValue(faction, out var pop))
            {
                curPop = pop.current;
                maxPop = pop.max;
            }

            // Determine faction color and name
            Color factionColor = GetFactionColor(faction);
            string factionName = GetFactionName(faction);

            // Top bar background (with faction-specific tint)
            var topBarRect = new Rect(0, yOffset, Screen.width, topBarHeight);
            var tintedColor = new Color(
                factionColor.r * 0.3f,
                factionColor.g * 0.3f,
                factionColor.b * 0.3f,
                0.6f
            );

            GUI.color = tintedColor;
            GUI.DrawTexture(topBarRect, _texTopBar);
            GUI.color = Color.white;

            // Start drawing resources
            float xPos = leftPadding;

            // Faction name pill
            var nameRect = new Rect(xPos, yOffset + 4f, 80f, topBarHeight - 8f);
            GUI.Box(nameRect, "", _pillBg);

            var nameStyle = new GUIStyle(_pillText);
            nameStyle.normal.textColor = factionColor;
            nameStyle.fontStyle = FontStyle.Bold;
            GUI.Label(nameRect, factionName, nameStyle);
            xPos += 80f + pillSpacing;

            // Supplies pill
            DrawResourcePill(xPos, yOffset, "📦 Supplies", res.Supplies.ToString(), new Color(0.9f, 0.8f, 0.5f));
            xPos += 120f + pillSpacing;

            // Iron pill
            DrawResourcePill(xPos, yOffset, "⚙️ Iron", res.Iron.ToString(), new Color(0.7f, 0.7f, 0.7f));
            xPos += 90f + pillSpacing;

            // Crystal pill
            DrawResourcePill(xPos, yOffset, "💎 Crystal", res.Crystal.ToString(), new Color(0.5f, 0.8f, 1.0f));
            xPos += 100f + pillSpacing;

            // Veilsteel pill
            DrawResourcePill(xPos, yOffset, "⚫ Veilsteel", res.Veilsteel.ToString(), new Color(0.4f, 0.2f, 0.6f));
            xPos += 110f + pillSpacing;

            // Glow pill
            DrawResourcePill(xPos, yOffset, "✨ Glow", res.Glow.ToString(), new Color(1f, 0.9f, 0.3f));
            xPos += 90f + pillSpacing;

            // Population pill
            string popText = $"{curPop}/{maxPop}";
            Color popColor = curPop >= maxPop ? new Color(1f, 0.3f, 0.3f) : new Color(0.6f, 1f, 0.6f);
            DrawResourcePill(xPos, yOffset, "👥 Pop", popText, popColor);
        }

        private void DrawResourcePill(float x, float y, string label, string value, Color color)
        {
            var pillRect = new Rect(x, y + 4f, 100f, topBarHeight - 8f);
            GUI.Box(pillRect, "", _pillBg);

            var labelStyle = new GUIStyle(_pillText);
            labelStyle.normal.textColor = color;
            labelStyle.fontSize = 10;

            var valueStyle = new GUIStyle(_pillText);
            valueStyle.normal.textColor = Color.white;
            valueStyle.fontSize = 14;
            valueStyle.fontStyle = FontStyle.Bold;

            // Label above value
            var labelRect = new Rect(x + 5f, y + 4f, 90f, 10f);
            var valueRect = new Rect(x + 5f, y + 14f, 90f, 14f);

            GUI.Label(labelRect, label, labelStyle);
            GUI.Label(valueRect, value, valueStyle);
        }

        private Color GetFactionColor(Faction faction)
        {
            return faction switch
            {
                Faction.Blue => new Color(0.3f, 0.5f, 1f),
                Faction.Red => new Color(1f, 0.3f, 0.3f),
                Faction.Green => new Color(0.3f, 1f, 0.3f),
                Faction.Yellow => new Color(1f, 1f, 0.3f),
                Faction.Purple => new Color(0.8f, 0.3f, 1f),
                Faction.Orange => new Color(1f, 0.6f, 0.2f),
                Faction.Teal => new Color(0.2f, 0.8f, 0.8f),
                Faction.White => new Color(0.9f, 0.9f, 0.9f),
                _ => Color.gray
            };
        }

        private string GetFactionName(Faction faction)
        {
            return faction switch
            {
                Faction.Blue => "PLAYER",
                Faction.Red => "AI (Red)",
                Faction.Green => "AI (Green)",
                Faction.Yellow => "AI (Yellow)",
                Faction.Purple => "AI (Purple)",
                Faction.Orange => "AI (Orange)",
                Faction.Teal => "AI (Teal)",
                Faction.White => "AI (White)",
                _ => "Unknown"
            };
        }
    }
}
