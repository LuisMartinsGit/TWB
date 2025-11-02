// BarracksPanel.cs - TRULY FIXED VERSION
// Now with mouse detection to prevent click-through
using System.Text;
using UnityEngine;
using Unity.Entities;

public class BarracksPanel : MonoBehaviour
{
    public static bool PanelVisible;
    public static Rect PanelRectScreenBL;

    private World _world;
    private EntityManager _em;

    public const float PanelWidth = 330f;
    public const float PanelHeight = 220f;
    private RectOffset _pad;
    private GUIStyle _iconBtn, _caption, _small;

    private Texture2D _iconSwordsman, _iconArcher;

    void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        _pad = new RectOffset(10, 10, 10, 10);

        _iconSwordsman = Resources.Load<Texture2D>("UI/Icons/Swordsman");
        _iconArcher    = Resources.Load<Texture2D>("UI/Icons/Archer");
    }


    /// <summary>
    /// CRITICAL: Check if mouse is over the Barracks panel.
    /// RTSInput calls this to avoid deselecting when clicking the panel.
    /// Convert mouse position to GUI coordinates for proper detection.
    /// </summary>
    public static bool IsPointerOverPanel()
    {
        if (!PanelVisible) return false;
        
        // Mouse position is in screen space (bottom-left origin)
        // GUI Rect is also in screen space (bottom-left origin for our panel)
        // But GUI.Window and GUI.Box use top-left origin
        // Since we positioned our panel using Screen.height - y, we need to convert
        
        Vector2 mousePos = Input.mousePosition;
        
        // Convert panel rect to screen coordinates
        Rect screenRect = new Rect(
            PanelRectScreenBL.x,
            Screen.height - PanelRectScreenBL.y - PanelRectScreenBL.height,
            PanelRectScreenBL.width,
            PanelRectScreenBL.height
        );
        
        return screenRect.Contains(mousePos);
    }
}