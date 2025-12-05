// File: Assets/Scripts/UI/Common/Styles.cs
// Shared UI style definitions for consistent appearance

using UnityEngine;

namespace TheWaningBorder.UI.Common
{
    /// <summary>
    /// Shared UI style definitions for IMGUI.
    /// Provides consistent appearance across all UI panels.
    /// </summary>
    public static class Styles
    {
        private static bool _initialized = false;

        // Panel backgrounds
        public static GUIStyle PanelBox { get; private set; }
        public static GUIStyle DarkBox { get; private set; }
        public static GUIStyle TransparentBox { get; private set; }

        // Text styles
        public static GUIStyle Header { get; private set; }
        public static GUIStyle SubHeader { get; private set; }
        public static GUIStyle Label { get; private set; }
        public static GUIStyle SmallLabel { get; private set; }
        public static GUIStyle TinyLabel { get; private set; }
        public static GUIStyle CenteredLabel { get; private set; }
        public static GUIStyle RichLabel { get; private set; }

        // Button styles
        public static GUIStyle Button { get; private set; }
        public static GUIStyle SmallButton { get; private set; }
        public static GUIStyle IconButton { get; private set; }
        public static GUIStyle ToggleButton { get; private set; }

        // Slot/list styles
        public static GUIStyle SlotBox { get; private set; }
        public static GUIStyle ListItem { get; private set; }

        // Colors
        public static Color PanelBgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        public static Color DarkBgColor = new Color(0.05f, 0.05f, 0.07f, 0.9f);
        public static Color TransparentBgColor = new Color(0f, 0f, 0f, 0.6f);
        public static Color HighlightColor = new Color(0.3f, 0.6f, 1f, 1f);
        public static Color SuccessColor = new Color(0.3f, 1f, 0.3f, 1f);
        public static Color WarningColor = new Color(1f, 0.8f, 0.2f, 1f);
        public static Color ErrorColor = new Color(1f, 0.3f, 0.3f, 1f);
        public static Color DisabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        // Textures
        private static Texture2D _panelTex;
        private static Texture2D _darkTex;
        private static Texture2D _transparentTex;
        private static Texture2D _slotTex;

        /// <summary>
        /// Initialize all styles. Call this in OnGUI before using styles.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            CreateTextures();
            CreatePanelStyles();
            CreateTextStyles();
            CreateButtonStyles();
            CreateSlotStyles();

            _initialized = true;
        }

        private static void CreateTextures()
        {
            _panelTex = UIHelpers.MakeTexture(2, 2, PanelBgColor);
            _darkTex = UIHelpers.MakeTexture(2, 2, DarkBgColor);
            _transparentTex = UIHelpers.MakeTexture(2, 2, TransparentBgColor);
            _slotTex = UIHelpers.MakeTexture(2, 2, new Color(1f, 1f, 1f, 0.05f));
        }

        private static void CreatePanelStyles()
        {
            PanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelTex },
                padding = new RectOffset(10, 10, 10, 10)
            };

            DarkBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _darkTex },
                padding = new RectOffset(8, 8, 8, 8)
            };

            TransparentBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _transparentTex },
                padding = new RectOffset(6, 6, 6, 6)
            };
        }

        private static void CreateTextStyles()
        {
            Header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            SubHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            Label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            SmallLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            TinyLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };

            CenteredLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            RichLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true,
                wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
        }

        private static void CreateButtonStyles()
        {
            Button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                padding = new RectOffset(10, 10, 6, 6)
            };

            SmallButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                padding = new RectOffset(6, 6, 4, 4)
            };

            IconButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 4, 4)
            };

            ToggleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(8, 8, 4, 4)
            };
        }

        private static void CreateSlotStyles()
        {
            SlotBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _slotTex },
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(0, 0, 2, 2)
            };

            ListItem = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _slotTex },
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(0, 0, 1, 1)
            };
        }

        /// <summary>
        /// Get a label style with custom color.
        /// </summary>
        public static GUIStyle GetColoredLabel(Color color, int fontSize = 12, FontStyle fontStyle = FontStyle.Normal)
        {
            Initialize();
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                normal = { textColor = color }
            };
        }

        /// <summary>
        /// Get header style with custom color.
        /// </summary>
        public static GUIStyle GetColoredHeader(Color color)
        {
            Initialize();
            return new GUIStyle(Header)
            {
                normal = { textColor = color }
            };
        }

        /// <summary>
        /// Draw a section header with optional separator line.
        /// </summary>
        public static void DrawSectionHeader(string text, bool drawSeparator = true)
        {
            Initialize();
            GUILayout.Label(text, Header);
            if (drawSeparator)
            {
                var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUILayout.Space(4);
        }

        /// <summary>
        /// Draw a horizontal separator line.
        /// </summary>
        public static void DrawSeparator(float alpha = 0.2f)
        {
            GUILayout.Space(4);
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Space(4);
        }
    }
}