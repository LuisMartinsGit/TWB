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

    void OnGUI()
    {
        PanelVisible = false;

        var world = _world ?? World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        if (_em.Equals(default(EntityManager))) _em = world.EntityManager;

        InitStyles();

        // --- DEBUG STRIP: show what selection looks like
        var sel = RTSInput.CurrentSelection;
        GUI.Label(new Rect(10, 170, 400, 18),
            $"[BarracksPanel] Selected: {(sel==null?0:sel.Count)} entities");

        if (!TryGetFirstSelectedBarracks(out var barracks))
        {
            GUI.Label(new Rect(10, 188, 500, 18),
                "[BarracksPanel] No Barracks found in selection.");
            return;
        }

        PanelVisible = true;

        // Panel area
        var area = new Rect(10f, Screen.height - PanelHeight - 200f, PanelWidth, PanelHeight);
        PanelRectScreenBL = area;

        GUI.Box(area, "Barracks");
        var inner = new Rect(area.x+_pad.left, area.y+_pad.top, 
                            area.width-_pad.horizontal, area.height-_pad.vertical);
        GUILayout.BeginArea(inner);

        GUILayout.Label("Train Units");

        // Training buttons
        GUILayout.BeginHorizontal();
        if (TechTreeDB.Instance != null &&
            TechTreeDB.Instance.TryGetBuilding("Barracks", out var bdef) &&
            bdef.trains != null && bdef.trains.Length > 0)
        {
            foreach (var unitId in bdef.trains)
            {
                var icon = unitId == "Swordsman" ? _iconSwordsman :
                           unitId == "Archer"   ? _iconArcher : null;
                DrawTrainButton(barracks, unitId, icon);
            }
        }
        else
        {
            GUILayout.Label("No trainable units in JSON for Barracks.", _small);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Progress bar (if training)
        DrawProgressBar(barracks);

        GUILayout.Space(4);

        // Queue display
        DrawQueue(barracks);

        GUILayout.EndArea();
    }

    void DrawTrainButton(Entity barracks, string unitId, Texture2D icon)
    {
        var db = TechTreeDB.Instance;
        db.TryGetUnit(unitId, out var udef);

        var tip = new StringBuilder(64)
            .Append(Pretty(unitId))
            .Append(" • ")
            .Append(udef.trainingTime > 0 ? $"{udef.trainingTime:0.0}s" : "—")
            .ToString();

        var content = icon ? new GUIContent(icon, tip) : new GUIContent(unitId, tip);

        GUILayout.BeginVertical(GUILayout.Width(80f));
        
        if (GUILayout.Button(content, _iconBtn))
        {
            var buf = _em.GetBuffer<TrainQueueItem>(barracks);
            buf.Add(new TrainQueueItem { UnitId = unitId });
            
            // Consume the event
            Event.current.Use();
        }

        GUILayout.Label(Pretty(unitId), _caption, GUILayout.Width(80f));
        GUILayout.Label(udef.trainingTime > 0 ? $"{udef.trainingTime:0.0}s" : "—", _small, GUILayout.Width(80f));
        GUILayout.EndVertical();
    }

    void DrawProgressBar(Entity barracks)
    {
        var ts = _em.GetComponentData<TrainingState>(barracks);
        
        if (ts.Busy == 0) return;

        var q = _em.GetBuffer<TrainQueueItem>(barracks);
        if (q.Length == 0) return;

        var currentUnit = q[0].UnitId.ToString();
        
        // Get total training time
        float totalTime = 1f;
        if (TechTreeDB.Instance != null && 
            TechTreeDB.Instance.TryGetUnit(currentUnit, out var udef))
        {
            totalTime = udef.trainingTime > 0 ? udef.trainingTime : 1f;
        }

        // Calculate progress (0 to 1)
        float elapsed = totalTime - ts.Remaining;
        float progress = Mathf.Clamp01(elapsed / totalTime);

        // Draw progress bar
        GUILayout.Label($"Training: {Pretty(currentUnit)}", _small);
        
        var progressRect = GUILayoutUtility.GetRect(PanelWidth - _pad.horizontal - 20, 20);
        
        // Background (empty bar)
        GUI.Box(progressRect, "");
        
        // Fill (progress)
        var fillRect = new Rect(
            progressRect.x + 2, 
            progressRect.y + 2, 
            (progressRect.width - 4) * progress, 
            progressRect.height - 4
        );
        
        // Green fill for progress
        var oldColor = GUI.color;
        GUI.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = oldColor;
        
        // Progress text (centered)
        var percentText = $"{(progress * 100):0}% ({ts.Remaining:0.1}s left)";
        var textRect = new Rect(progressRect.x, progressRect.y, progressRect.width, progressRect.height);
        
        var centeredStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };
        
        GUI.Label(textRect, percentText, centeredStyle);
    }

    void DrawQueue(Entity barracks)
    {
        var q = _em.GetBuffer<TrainQueueItem>(barracks);

        var sb = new StringBuilder("Queue: ");
        for (int i = 0; i < q.Length; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append(Pretty(q[i].UnitId.ToString()));
        }
        if (q.Length == 0) sb.Append("(empty)");

        GUILayout.Label(sb.ToString(), _small);
    }

    void InitStyles()
    {
        if (_iconBtn == null)
        {
            _iconBtn = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 64,
                fixedWidth = 64
            };
        }
        if (_caption == null)
        {
            _caption = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
        }
        if (_small == null)
        {
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10
            };
        }
    }

    static string Pretty(string id) => id.Replace('_', ' ');

    bool TryGetFirstSelectedBarracks(out Entity e)
    {
        e = Entity.Null;
        var sel = RTSInput.CurrentSelection;
        if (sel == null || sel.Count == 0 || _em.Equals(default(EntityManager))) return false;

        for (int i = 0; i < sel.Count; i++)
        {
            var ent = sel[i];
            if (!_em.Exists(ent)) continue;

            // 1) Explicit tag
            if (_em.HasComponent<BarracksTag>(ent)) { e = ent; return true; }

            // 2) Fallback: PresentationId 510 + BuildingTag
            if (_em.HasComponent<BuildingTag>(ent) && _em.HasComponent<PresentationId>(ent))
            {
                var pid = _em.GetComponentData<PresentationId>(ent).Id;
                if (pid == 510) { e = ent; return true; }
            }
        }
        return false;
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