// EntityInfoPanel.cs
// Displays entity information (portrait, stats, description) in lower left corner

using UnityEngine;
using Unity.Entities;

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
        
        // Get display info
        var em = UnifiedUIManager.GetEntityManager();
        if (em.Equals(default(EntityManager))) return;
        
        var info = EntityInfoExtractor.GetDisplayInfo(entity, em);
        
        // Initialize styles
        InitStyles();
        
        // Draw panel
        DrawPanel(info);
    }
    
    void DrawPanel(EntityDisplayInfo info)
    {
        PanelVisible = true;
        
        // Panel positioned at bottom-left
        var panelRect = new Rect(
            PanelPadding,
            Screen.height - PanelHeight - PanelPadding,
            PanelWidth,
            PanelHeight
        );
        PanelRect = panelRect;
        
        // Draw background box
        GUI.Box(panelRect, "", _boxStyle);
        
        // Inner content area
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
            // Placeholder portrait
            var portraitRect = GUILayoutUtility.GetRect(PortraitSize, PortraitSize, 
                GUILayout.Width(PortraitSize), GUILayout.Height(PortraitSize));
            GUI.Box(portraitRect, "?", _headerStyle);
        }
        
        GUILayout.Space(8);
        
        // Name and Type
        GUILayout.BeginVertical();
        GUILayout.Label(info.Name, _headerStyle);
        GUILayout.Label(info.Type, _smallStyle);
        GUILayout.EndVertical();
        
        GUILayout.EndHorizontal();
        
        GUILayout.Space(8);
        
        // Description
        if (!string.IsNullOrEmpty(info.Description))
        {
            GUILayout.Label(info.Description, _descStyle);
            GUILayout.Space(6);
        }
        
        // Stats section
        DrawStats(info);
        
        // Resource generation (for buildings)
        if (info.HasResourceGeneration)
        {
            GUILayout.Space(6);
            DrawResourceGeneration(info);
        }
        
        GUILayout.EndArea();
    }
    
    void DrawStats(EntityDisplayInfo info)
    {
        bool hasAnyStats = info.Attack.HasValue || info.Defense.HasValue || 
                          info.CurrentHealth.HasValue || info.Speed.HasValue;
        
        if (!hasAnyStats) return;
        
        GUILayout.Label("━━━━━━━━━━━━━━━━━", _smallStyle);
        
        // Combat stats
        if (info.Attack.HasValue)
            GUILayout.Label($"⚔️ Attack: {info.Attack.Value}", _labelStyle);
        
        if (info.Defense.HasValue)
            GUILayout.Label($"🛡️ Defense: {info.Defense.Value}", _labelStyle);
        
        // Health
        if (info.CurrentHealth.HasValue && info.MaxHealth.HasValue)
        {
            GUILayout.Label($"❤️ Health: {info.CurrentHealth.Value} / {info.MaxHealth.Value}", _labelStyle);
        }
        else if (info.CurrentHealth.HasValue)
        {
            GUILayout.Label($"❤️ Health: {info.CurrentHealth.Value}", _labelStyle);
        }
        
        // Speed (for units)
        if (info.Speed.HasValue)
            GUILayout.Label($"🏃 Speed: {info.Speed.Value:0.0}", _labelStyle);
    }
    
    void DrawResourceGeneration(EntityDisplayInfo info)
    {
        GUILayout.Label("━━━━━━━━━━━━━━━━━", _smallStyle);
        GUILayout.Label("Resource Generation:", _labelStyle);
        
        if (info.SuppliesPerMinute.HasValue && info.SuppliesPerMinute.Value > 0)
            GUILayout.Label($"  📦 Supplies: +{info.SuppliesPerMinute.Value}/min", _smallStyle);
        
        if (info.IronPerMinute.HasValue && info.IronPerMinute.Value > 0)
            GUILayout.Label($"  ⛏️ Iron: +{info.IronPerMinute.Value}/min", _smallStyle);
        
        if (info.CrystalPerMinute.HasValue && info.CrystalPerMinute.Value > 0)
            GUILayout.Label($"  💎 Crystal: +{info.CrystalPerMinute.Value}/min", _smallStyle);
        
        if (info.VeilsteelPerMinute.HasValue && info.VeilsteelPerMinute.Value > 0)
            GUILayout.Label($"  ⚫ Veilsteel: +{info.VeilsteelPerMinute.Value}/min", _smallStyle);
        
        if (info.GlowPerMinute.HasValue && info.GlowPerMinute.Value > 0)
            GUILayout.Label($"  ✨ Glow: +{info.GlowPerMinute.Value}/min", _smallStyle);
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
        
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white }
            };
        }
        
        if (_smallStyle == null)
        {
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
        }
        
        if (_descStyle == null)
        {
            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                wordWrap = true
            };
        }
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
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}