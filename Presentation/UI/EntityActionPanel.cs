// EntityActionPanel.cs
// Displays context-sensitive action buttons (building placement, unit training, etc.)
// UPDATED: Deducts unit costs when adding to queue (not when spawning)

using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using TheWaningBorder.Economy;

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
    
    void Awake()
    {
        _padding = new RectOffset(10, 10, 10, 10);
    }
    
    void OnGUI()
    {
        PanelVisible = false;
        
        // Get selected entity
        var entity = UnifiedUIManager.GetFirstSelectedEntity();
        if (entity == Entity.Null) return;
        
        // Get action info
        var em = UnifiedUIManager.GetEntityManager();
        if (em.Equals(default(EntityManager))) return;
        
        var actionInfo = EntityActionExtractor.GetActionInfo(entity, em);
        
        // Only show if there are actions available
        if (actionInfo.Type == ActionType.None) return;
        
        // Initialize styles
        InitStyles();
        
        // Draw panel based on action type
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
    
    // ==================== BUILDING PLACEMENT PANEL ====================
    
    void DrawBuildingPlacementPanel(Entity entity, EntityActionInfo actionInfo)
    {
        PanelVisible = true;
        
        // Panel positioned next to EntityInfoPanel
        var panelRect = new Rect(
            PanelPadding + 280f + PanelPadding, // After EntityInfoPanel + spacing
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
        
        // Header
        if (BuilderCommandPanel.IsPlacingBuilding)
            GUILayout.Label("Left-click to place, Right/Esc to cancel", _headerStyle);
        else
            GUILayout.Label("Build Structure", _headerStyle);
        
        GUILayout.Space(8);
        
        // Disable buttons while placing
        GUI.enabled = !BuilderCommandPanel.IsPlacingBuilding;
        
        // Draw action buttons in a grid
        DrawActionGrid(entity, actionInfo.Actions, (button) =>
        {
            // Handle building placement via BuilderCommandPanel
            BuilderCommandPanel.TriggerBuildingPlacement(button.Id);
        });
        
        GUI.enabled = true;
        
        GUILayout.EndArea();
    }
    
    // ==================== UNIT TRAINING PANEL ====================
    
    void DrawUnitTrainingPanel(Entity entity, EntityActionInfo actionInfo)
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
        
        // Draw training buttons
        DrawActionGrid(entity, actionInfo.Actions, (button) =>
        {
            // CRITICAL: Deduct cost when adding to queue (not when spawning)
            var em = UnifiedUIManager.GetEntityManager();
            if (!em.Exists(entity)) return;
            
            // Get faction
            Faction faction = GameSettings.LocalPlayerFaction;
            if (em.HasComponent<FactionTag>(entity))
                faction = em.GetComponentData<FactionTag>(entity).Value;
            
            // Try to spend the cost
            if (!FactionEconomy.Spend(em, faction, button.Cost))
            {
                Debug.LogWarning($"Cannot afford to train {button.Id} - cost: {button.Cost.Supplies}S {button.Cost.Iron}Fe");
                return;
            }
            
            // Cost paid successfully - add to training queue
            if (em.HasBuffer<TrainQueueItem>(entity))
            {
                var queue = em.GetBuffer<TrainQueueItem>(entity);
                queue.Add(new TrainQueueItem { UnitId = button.Id });
                
                Debug.Log($"Queued {button.Id} for training. Cost paid: {button.Cost.Supplies}S {button.Cost.Iron}Fe");
                
                // Consume event
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
        if (actionInfo.TrainingState != null && actionInfo.TrainingState.Queue != null)
        {
            DrawQueue(actionInfo.TrainingState.Queue);
        }
        
        GUILayout.EndArea();
    }
    
    // ==================== ACTION GRID ====================
    
    void DrawActionGrid(Entity entity, ActionButton[] actions, System.Action<ActionButton> onClick)
    {
        if (actions == null || actions.Length == 0)
        {
            GUILayout.Label("No actions available", _smallStyle);
            return;
        }
        
        // Draw buttons in rows of 4
        int columns = 4;
        int rows = Mathf.CeilToInt(actions.Length / (float)columns);
        
        for (int row = 0; row < rows; row++)
        {
            GUILayout.BeginHorizontal();
            
            for (int col = 0; col < columns; col++)
            {
                int index = row * columns + col;
                if (index >= actions.Length) break;
                
                DrawActionButton(actions[index], onClick);
            }
            
            GUILayout.EndHorizontal();
        }
    }
    
    void DrawActionButton(ActionButton button, System.Action<ActionButton> onClick)
    {
        GUILayout.BeginVertical(GUILayout.Width(ButtonSize + 16f));
        
        // Check affordability
        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && button.CanAfford;
        
        // Button with icon
        GUIContent content;
        if (button.Icon != null)
            content = new GUIContent(button.Icon, button.Tooltip);
        else
            content = new GUIContent(button.Label, button.Tooltip);
        
        if (GUILayout.Button(content, _buttonStyle, 
            GUILayout.Width(ButtonSize), GUILayout.Height(ButtonSize)))
        {
            onClick?.Invoke(button);
        }
        
        GUI.enabled = prevEnabled;
        
        // Label below button
        var labelStyle = new GUIStyle(_labelStyle)
        {
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label(button.Label, labelStyle, GUILayout.Width(ButtonSize + 16f));
        
        // Cost or time below label
        if (button.TrainingTime > 0)
        {
            var timeStyle = new GUIStyle(_smallStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"{button.TrainingTime:0.0}s", timeStyle, GUILayout.Width(ButtonSize + 16f));
        }
        else if (!button.Cost.IsZero)
        {
            var costStyle = new GUIStyle(_smallStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = button.CanAfford ? 
                    new Color(0.8f, 0.8f, 0.8f) : Color.red }
            };
            GUILayout.Label(FormatCost(button.Cost), costStyle, GUILayout.Width(ButtonSize + 16f));
        }
        
        GUILayout.EndVertical();
    }
    
    // ==================== TRAINING PROGRESS ====================
    
    void DrawProgressBar(TrainingInfo training)
    {
        if (!training.IsTraining || string.IsNullOrEmpty(training.CurrentUnitId))
            return;
        
        GUILayout.Label($"Training: {PrettyName(training.CurrentUnitId)}", _smallStyle);
        
        var progressRect = GUILayoutUtility.GetRect(PanelWidth - _padding.horizontal - 20, 20);
        
        // Background
        GUI.Box(progressRect, "");
        
        // Fill
        var fillRect = new Rect(
            progressRect.x + 2,
            progressRect.y + 2,
            (progressRect.width - 4) * training.Progress,
            progressRect.height - 4
        );
        
        var oldColor = GUI.color;
        GUI.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = oldColor;
        
        // Progress text
        var percentText = $"{(training.Progress * 100):0}% ({training.TimeRemaining:0.1}s left)";
        var centeredStyle = new GUIStyle(_smallStyle)
        {
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(progressRect, percentText, centeredStyle);
    }
    
    void DrawQueue(string[] queue)
    {
        if (queue == null || queue.Length == 0)
        {
            GUILayout.Label("Queue: (empty)", _smallStyle);
            return;
        }
        
        string queueText = "Queue: ";
        for (int i = 0; i < queue.Length; i++)
        {
            if (i > 0) queueText += " → ";
            queueText += PrettyName(queue[i]);
        }
        
        GUILayout.Label(queueText, _smallStyle);
    }
    
    // ==================== HELPER METHODS ====================
    
    string FormatCost(Cost cost)
    {
        var parts = new System.Collections.Generic.List<string>();
        
        if (cost.Supplies > 0) parts.Add($"S{cost.Supplies}");
        if (cost.Iron > 0) parts.Add($"Fe{cost.Iron}");
        if (cost.Crystal > 0) parts.Add($"Cr{cost.Crystal}");
        
        return string.Join(" ", parts);
    }
    
    string PrettyName(string id)
    {
        return id.Replace('_', ' ');
    }
    
    void InitStyles()
    {
        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0, 0, 0, 0.85f)) }
            };
        }
        
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }
        
        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow }
            };
        }
        
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = Color.white }
            };
        }
        
        if (_smallStyle == null)
        {
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
        }
    }

    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
        /// <summary>
    /// Check if mouse pointer is over this panel.
    /// </summary>
    public static bool IsPointerOver()
    {
        if (!PanelVisible) return false;
        
        Vector2 mousePos = Input.mousePosition;
        
        // Convert to GUI coordinates (top-left origin)
        Rect guiRect = new Rect(
            PanelRect.x,
            Screen.height - PanelRect.y - PanelRect.height,
            PanelRect.width,
            PanelRect.height
        );
        
        return guiRect.Contains(mousePos);
    }
    
}