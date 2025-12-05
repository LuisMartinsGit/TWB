// File: Assets/Scripts/UI/Panels/EntityInfoPanel.cs
// Displays entity information (portrait, stats, description) in lower left corner

using UnityEngine;
using Unity.Entities;
using TheWaningBorder.UI;

namespace TheWaningBorder.UI.Panels
{
    /// <summary>
    /// IMGUI panel that displays information about the currently selected entity.
    /// Shows portrait, name, type, stats, and description.
    /// </summary>
    public class EntityInfoPanel : MonoBehaviour
    {
        public static bool PanelVisible { get; private set; }
        public static Rect PanelRect { get; private set; }

        private const float PanelWidth = 280f;
        private const float PanelHeight = 240f;
        private const float PanelPadding = 10f;
        private const float PortraitSize = 80f;

        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _descStyle;
        private RectOffset _padding;
        private bool _stylesInit = false;

        void Awake()
        {
            _padding = new RectOffset(10, 10, 10, 10);
        }

        void OnGUI()
        {
            PanelVisible = false;

            var entity = UnifiedUIManager.GetFirstSelectedEntity();
            if (entity == Entity.Null) return;

            var em = UnifiedUIManager.GetEntityManager();
            if (em.Equals(default(EntityManager))) return;

            var info = EntityInfoExtractor.GetDisplayInfo(entity, em);

            InitStyles();
            DrawPanel(info);
        }

        private void InitStyles()
        {
            if (_stylesInit) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.12f, 0.95f)) }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };

            _stylesInit = true;
        }

        private void DrawPanel(EntityDisplayInfo info)
        {
            PanelVisible = true;

            var panelRect = new Rect(
                PanelPadding,
                Screen.height - PanelHeight - PanelPadding,
                PanelWidth,
                PanelHeight
            );
            PanelRect = panelRect;

            GUI.Box(panelRect, "", _boxStyle);

            var innerRect = new Rect(
                panelRect.x + _padding.left,
                panelRect.y + _padding.top,
                panelRect.width - _padding.horizontal,
                panelRect.height - _padding.vertical
            );

            GUILayout.BeginArea(innerRect);

            // Top section: Portrait + Name/Type
            GUILayout.BeginHorizontal();

            // Portrait
            if (info.Portrait != null)
            {
                var portraitRect = GUILayoutUtility.GetRect(PortraitSize, PortraitSize,
                    GUILayout.Width(PortraitSize), GUILayout.Height(PortraitSize));
                GUI.DrawTexture(portraitRect, info.Portrait, ScaleMode.ScaleToFit);
            }
            else
            {
                var portraitRect = GUILayoutUtility.GetRect(PortraitSize, PortraitSize,
                    GUILayout.Width(PortraitSize), GUILayout.Height(PortraitSize));
                GUI.Box(portraitRect, "?");
            }

            GUILayout.Space(10);

            // Name and type
            GUILayout.BeginVertical();
            GUILayout.Label(info.Name, _headerStyle);
            GUILayout.Label(info.Type, _smallStyle);

            // Health bar
            if (info.CurrentHealth.HasValue && info.MaxHealth.HasValue)
            {
                GUILayout.Space(5);
                DrawHealthBar(info.CurrentHealth.Value, info.MaxHealth.Value);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Stats section
            if (info.HasCombatStats)
            {
                GUILayout.BeginHorizontal();
                if (info.Attack.HasValue)
                    GUILayout.Label($"⚔ Attack: {info.Attack.Value}", _labelStyle, GUILayout.Width(100));
                if (info.Defense.HasValue)
                    GUILayout.Label($"🛡 Defense: {info.Defense.Value}", _labelStyle, GUILayout.Width(100));
                GUILayout.EndHorizontal();

                if (info.Speed.HasValue)
                    GUILayout.Label($"🏃 Speed: {info.Speed.Value:F1}", _labelStyle);
            }

            // Resource generation (buildings)
            if (info.HasResourceGeneration)
            {
                GUILayout.Space(5);
                GUILayout.Label("Resource Generation:", _smallStyle);
                GUILayout.BeginHorizontal();
                if (info.SuppliesPerMinute.HasValue && info.SuppliesPerMinute.Value > 0)
                    GUILayout.Label($"💰 {info.SuppliesPerMinute}/min", _labelStyle, GUILayout.Width(80));
                if (info.IronPerMinute.HasValue && info.IronPerMinute.Value > 0)
                    GUILayout.Label($"🔩 {info.IronPerMinute}/min", _labelStyle, GUILayout.Width(80));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // Description
            GUILayout.Label(info.Description, _descStyle);

            GUILayout.EndArea();
        }

        private void DrawHealthBar(int current, int max)
        {
            var rect = GUILayoutUtility.GetRect(PortraitSize, 8);

            // Background
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Fill
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0;
            Color fillColor = ratio > 0.5f ? Color.green : (ratio > 0.25f ? Color.yellow : Color.red);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Text
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, $"{current}/{max}", labelStyle);
        }

        private static Texture2D MakeTexture(int w, int h, Color c)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = c;
            var t = new Texture2D(w, h);
            t.SetPixels(pix);
            t.Apply();
            return t;
        }

        /// <summary>
        /// Check if pointer is over this panel.
        /// </summary>
        public static bool IsPointerOver()
        {
            if (!PanelVisible) return false;
            var mousePos = Input.mousePosition;
            var screenRect = new Rect(
                PanelRect.x,
                Screen.height - PanelRect.y - PanelRect.height,
                PanelRect.width,
                PanelRect.height
            );
            return screenRect.Contains(mousePos);
        }
    }
}