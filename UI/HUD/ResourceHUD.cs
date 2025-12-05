// File: Assets/Scripts/UI/HUD/ResourceHUD.cs
// Multi-Faction Resource Display with Population
// Shows all active factions' resources in colored top bars

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using EntityWorld = Unity.Entities.World;

namespace TheWaningBorder.UI.HUD
{
    /// <summary>
    /// IMGUI-based resource HUD that displays faction resources and population.
    /// Shows a top bar for each active faction with color coding.
    /// </summary>
    public class ResourceHUD : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private Faction humanFaction = GameSettings.LocalPlayerFaction;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private float topBarHeight = 32f;
        [SerializeField] private float leftPadding = 10f;
        [SerializeField] private float pillSpacing = 10f;

        /// <summary>Returns true if the mouse is over the top resource bar.</summary>
        public static bool IsPointerOverTopBar { get; private set; }

        private EntityWorld _world;
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
            _world = EntityWorld.DefaultGameObjectInjectionWorld;
            if (_world == null) return;

            _em = _world.EntityManager;
            _banksQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionResources>());

            _populationQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionPopulation>());

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
            if (world == null || !world.IsCreated) return;
            _em = world.EntityManager;

            // Get resources
            using var entities = _banksQuery.ToEntityArray(Allocator.Temp);
            using var tags = _banksQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var banks = _banksQuery.ToComponentDataArray<FactionResources>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                _cache[tags[i].Value] = banks[i];
            }

            // Get population
            using var popEntities = _populationQuery.ToEntityArray(Allocator.Temp);
            using var popTags = _populationQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var pops = _populationQuery.ToComponentDataArray<FactionPopulation>(Allocator.Temp);

            for (int i = 0; i < popEntities.Length; i++)
            {
                _popCache[popTags[i].Value] = (pops[i].Current, pops[i].Max);
            }
        }

        private void OnGUI()
        {
            if (!_stylesBuilt) BuildStyles();
            if (_cache.Count == 0) return;

            DrawAllFactionsTopBar();
        }

        private void BuildStyles()
        {
            _topBarBg = new GUIStyle { normal = { background = _texTopBar } };
            _pillBg = new GUIStyle { normal = { background = _texPill } };
            _pillText = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            _stylesBuilt = true;
        }

        private static Texture2D MakeTex(int w, int h, Color c)
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

            foreach (var faction in _cache.Keys)
            {
                DrawFactionBar(faction, yOffset);
                yOffset += topBarHeight + 4f;
                factionCount++;
                if (factionCount >= 8) break;
            }

            IsPointerOverTopBar = RTSInput.mousePosition.y >= Screen.height - (topBarHeight + 4f) * factionCount;
        }

        private void DrawFactionBar(Faction faction, float yOffset)
        {
            if (!_cache.TryGetValue(faction, out var res)) return;

            int curPop = 0, maxPop = 0;
            if (_popCache.TryGetValue(faction, out var pop))
            {
                curPop = pop.current;
                maxPop = pop.max;
            }

            Color factionColor = GetFactionColor(faction);
            string factionName = GetFactionName(faction);

            var topBarRect = new Rect(0, yOffset, Screen.width, topBarHeight);
            var tintedColor = new Color(
                factionColor.r * 0.3f,
                factionColor.g * 0.3f,
                factionColor.b * 0.3f,
                0.7f);
            GUI.color = tintedColor;
            GUI.Box(topBarRect, "", _topBarBg);
            GUI.color = Color.white;

            // Faction label
            var labelStyle = new GUIStyle(_pillText)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = factionColor }
            };
            GUI.Label(new Rect(leftPadding, yOffset + 6f, 100f, 20f), factionName, labelStyle);

            // Resource pills
            float xPos = leftPadding + 100f;

            DrawResourcePill(xPos, yOffset, "💰 Supplies", res.Supplies.ToString(), new Color(1f, 0.85f, 0.4f));
            xPos += 110f + pillSpacing;

            DrawResourcePill(xPos, yOffset, "🔩 Iron", res.Iron.ToString(), new Color(0.7f, 0.7f, 0.8f));
            xPos += 90f + pillSpacing;

            DrawResourcePill(xPos, yOffset, "💎 Crystal", res.Crystal.ToString(), new Color(0.6f, 0.8f, 1f));
            xPos += 100f + pillSpacing;

            DrawResourcePill(xPos, yOffset, "⚔️ Veilsteel", res.Veilsteel.ToString(), new Color(0.8f, 0.5f, 1f));
            xPos += 110f + pillSpacing;

            DrawResourcePill(xPos, yOffset, "✨ Glow", res.Glow.ToString(), new Color(1f, 1f, 0.6f));
            xPos += 90f + pillSpacing;

            // Population
            string popText = $"{curPop}/{maxPop}";
            Color popColor = curPop >= maxPop ? new Color(1f, 0.3f, 0.3f) : new Color(0.6f, 1f, 0.6f);
            DrawResourcePill(xPos, yOffset, "👥 Pop", popText, popColor);
        }

        private void DrawResourcePill(float x, float y, string label, string value, Color color)
        {
            var pillRect = new Rect(x, y + 4f, 100f, topBarHeight - 8f);
            GUI.Box(pillRect, "", _pillBg);

            var labelStyle = new GUIStyle(_pillText)
            {
                normal = { textColor = color },
                fontSize = 10
            };

            var valueStyle = new GUIStyle(_pillText)
            {
                normal = { textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

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
            if (faction == GameSettings.LocalPlayerFaction) return "PLAYER";

            return faction switch
            {
                Faction.Blue => "AI (Blue)",
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