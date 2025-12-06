// File: Assets/Scripts/UI/Panels/EntityActionPanel.cs
// Displays context-sensitive action buttons (building placement, unit training, etc.)

using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using TheWaningBorder.Economy;
using TheWaningBorder.UI;
using TheWaningBorder.Core;
using TheWaningBorder.UI.Common;

namespace TheWaningBorder.UI.Panels
{
    /// <summary>
    /// IMGUI panel that displays available actions for the selected entity.
    /// Shows building placement buttons for Builders, training buttons for Barracks/Hall.
    /// Costs are deducted when adding to queue, not when spawning.
    /// </summary>
    public class EntityActionPanel : MonoBehaviour
    {
        public static bool PanelVisible { get; private set; }
        public static Rect PanelRect { get; private set; }

        private const float PanelWidth = 350f;
        private const float PanelHeight = 240f;
        private const float PanelPadding = 10f;
        private const float ButtonSize = 64f;
        private const float ButtonSpacing = 8f;

        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
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

            var actionInfo = EntityActionExtractor.GetActionInfo(entity, em);

            if (actionInfo.Type == ActionType.None) return;

            InitStyles();

            switch (actionInfo.Type)
            {
                case ActionType.BuildingPlacement:
                    DrawBuildingPlacementPanel(entity, actionInfo);
                    break;

                case ActionType.UnitTraining:
                    DrawUnitTrainingPanel(entity, actionInfo);
                    break;
            }
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
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            _stylesInit = true;
        }

        private void DrawBuildingPlacementPanel(Entity entity, EntityActionInfo actionInfo)
        {
            PanelVisible = true;

            var panelRect = new Rect(
                PanelPadding + 280f + PanelPadding,
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

            if (BuilderCommandPanel.IsPlacingBuilding)
                GUILayout.Label("Left-click to place, Right/Esc to cancel", _headerStyle);
            else
                GUILayout.Label("Build Structure", _headerStyle);

            GUILayout.Space(8);

            GUI.enabled = !BuilderCommandPanel.IsPlacingBuilding;

            DrawActionGrid(entity, actionInfo.Actions, (button) =>
            {
                BuilderCommandPanel.TriggerBuildingPlacement(button.Id);
            });

            GUI.enabled = true;

            GUILayout.EndArea();
        }

        private void DrawUnitTrainingPanel(Entity entity, EntityActionInfo actionInfo)
        {
            PanelVisible = true;

            var panelRect = new Rect(
                PanelPadding + 280f + PanelPadding,
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

            GUILayout.Label("Train Units", _headerStyle);
            GUILayout.Space(8);

            DrawActionGrid(entity, actionInfo.Actions, (button) =>
            {
                var em = UnifiedUIManager.GetEntityManager();
                if (!em.Exists(entity)) return;

                Faction faction = GameSettings.LocalPlayerFaction;
                if (em.HasComponent<FactionTag>(entity))
                    faction = em.GetComponentData<FactionTag>(entity).Value;

                // Deduct cost when adding to queue
                if (!FactionEconomy.Spend(em, faction, button.Cost))
                {
                    Debug.LogWarning($"Cannot afford to train {button.Id}");
                    return;
                }

                // Add to training queue
                if (em.HasBuffer<TrainQueueItem>(entity))
                {
                    var queue = em.GetBuffer<TrainQueueItem>(entity);
                    queue.Add(new TrainQueueItem { UnitId = button.Id });
                    Debug.Log($"Queued {button.Id} for training");
                    Event.current.Use();
                }
            });

            GUILayout.Space(8);

            // Training progress bar
            if (actionInfo.TrainingState != null && actionInfo.TrainingState.IsTraining)
            {
                DrawProgressBar(actionInfo.TrainingState);
                GUILayout.Space(6);
            }

            // Training queue
            if (actionInfo.TrainingState?.Queue != null)
            {
                DrawQueue(actionInfo.TrainingState.Queue);
            }

            GUILayout.EndArea();
        }

        private void DrawActionGrid(Entity entity, ActionButton[] actions, System.Action<ActionButton> onClick)
        {
            if (actions == null || actions.Length == 0)
            {
                GUILayout.Label("No actions available", _smallStyle);
                return;
            }

            int buttonsPerRow = 4;
            int row = 0;

            GUILayout.BeginHorizontal();

            for (int i = 0; i < actions.Length; i++)
            {
                var button = actions[i];

                // Disable if can't afford
                bool wasEnabled = GUI.enabled;
                if (!button.CanAfford) GUI.enabled = false;

                // Button content
                string label = button.Icon != null ? "" : button.Label;
                var content = new GUIContent(label, button.Tooltip);

                if (GUILayout.Button(content, _buttonStyle,
                    GUILayout.Width(ButtonSize), GUILayout.Height(ButtonSize)))
                {
                    onClick?.Invoke(button);
                }

                // Draw icon on top
                if (button.Icon != null)
                {
                    var rect = GUILayoutUtility.GetLastRect();
                    var iconRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 20);
                    GUI.DrawTexture(iconRect, button.Icon, ScaleMode.ScaleToFit);

                    // Draw label below icon
                    var labelRect = new Rect(rect.x, rect.y + rect.height - 18, rect.width, 16);
                    GUI.Label(labelRect, button.Label, _smallStyle);
                }

                // Cost indicator
                if (!button.Cost.IsZero)
                {
                    var rect = GUILayoutUtility.GetLastRect();
                    var costStr = FormatShortCost(button.Cost);
                    var costStyle = new GUIStyle(_smallStyle)
                    {
                        normal = { textColor = button.CanAfford ? Color.green : Color.red },
                        alignment = TextAnchor.LowerRight
                    };
                    GUI.Label(rect, costStr, costStyle);
                }

                GUI.enabled = wasEnabled;

                row++;
                if (row >= buttonsPerRow && i < actions.Length - 1)
                {
                    row = 0;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(ButtonSpacing);
                    GUILayout.BeginHorizontal();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawProgressBar(TrainingInfo info)
        {
            GUILayout.Label($"Training: {info.CurrentUnitId}", _labelStyle);

            var rect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));

            // Background
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Fill
            GUI.color = new Color(0.3f, 0.7f, 1f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * info.Progress, rect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Time remaining
            var timeStyle = new GUIStyle(_smallStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, $"{info.TimeRemaining:F1}s", timeStyle);
        }

        private void DrawQueue(string[] queue)
        {
            if (queue.Length == 0) return;

            GUILayout.Label($"Queue ({queue.Length}):", _smallStyle);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Mathf.Min(queue.Length, 8); i++)
            {
                GUILayout.Label(queue[i], _smallStyle, GUILayout.Width(60));
            }
            if (queue.Length > 8)
                GUILayout.Label($"+{queue.Length - 8}", _smallStyle);
            GUILayout.EndHorizontal();
        }

        private string FormatShortCost(Cost cost)
        {
            if (cost.Supplies > 0) return $"{cost.Supplies}S";
            if (cost.Iron > 0) return $"{cost.Iron}Fe";
            return "";
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
            var mousePos = UnityEngine.Input.mousePosition;
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