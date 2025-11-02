// UnifiedUI_Diagnostics.cs
// Attach this to your UnifiedUI GameObject to debug why panels aren't showing

using UnityEngine;
using Unity.Entities;

public class UnifiedUI_Diagnostics : MonoBehaviour
{
    [Header("Enable Debug Output")]
    public bool showDebugInfo = true;
    
    private float _lastCheckTime;
    private const float CHECK_INTERVAL = 1f;
    
    void Update()
    {
        if (!showDebugInfo) return;
        
        // Check every second
        if (Time.time - _lastCheckTime < CHECK_INTERVAL) return;
        _lastCheckTime = Time.time;
        
        PerformDiagnostics();
    }
    
    void PerformDiagnostics()
    {
        Debug.Log("=== UnifiedUI Diagnostics ===");
        
        // 1. Check if RTSInput exists and has selection
        var rtsInput = FindObjectOfType<RTSInput>();
        if (rtsInput == null)
        {
            Debug.LogError("❌ RTSInput not found in scene!");
            return;
        }
        
        var selection = RTSInput.CurrentSelection;
        if (selection == null)
        {
            Debug.LogWarning("⚠️ RTSInput.CurrentSelection is null");
            return;
        }
        
        Debug.Log($"✅ RTSInput found, selection count: {selection.Count}");
        
        // 2. Check if we have valid entities
        if (selection.Count == 0)
        {
            Debug.Log("ℹ️ No entities selected (this is normal if you haven't selected anything)");
            return;
        }
        
        // 3. Check EntityManager
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogError("❌ World not found or not created!");
            return;
        }
        
        var em = world.EntityManager;
        var entity = selection[0];
        
        if (!em.Exists(entity))
        {
            Debug.LogError("❌ Selected entity does not exist!");
            return;
        }
        
        Debug.Log($"✅ Selected entity exists: {entity}");
        
        // 4. Check entity components
        bool hasUnit = em.HasComponent<UnitTag>(entity);
        bool hasBuilding = em.HasComponent<BuildingTag>(entity);
        bool hasFaction = em.HasComponent<FactionTag>(entity);
        
        Debug.Log($"   - UnitTag: {hasUnit}");
        Debug.Log($"   - BuildingTag: {hasBuilding}");
        Debug.Log($"   - FactionTag: {hasFaction}");
        
        if (hasFaction)
        {
            var faction = em.GetComponentData<FactionTag>(entity).Value;
            Debug.Log($"   - Faction: {faction}");
        }
        
        // 5. Check panel components
        var infoPanel = GetComponent<EntityInfoPanel>();
        var actionPanel = GetComponent<EntityActionPanel>();
        
        Debug.Log($"✅ EntityInfoPanel: {(infoPanel != null ? "Found" : "MISSING")}");
        Debug.Log($"✅ EntityActionPanel: {(actionPanel != null ? "Found" : "MISSING")}");
        
        // 6. Check panel visibility
        if (infoPanel != null)
        {
            Debug.Log($"   - InfoPanel visible: {EntityInfoPanel.PanelVisible}");
        }
        if (actionPanel != null)
        {
            Debug.Log($"   - ActionPanel visible: {EntityActionPanel.PanelVisible}");
        }
        
        // 7. Check if entity would show actions
        if (hasUnit && em.HasComponent<CanBuild>(entity))
        {
            Debug.Log("✅ Entity is a Builder - should show building menu");
        }
        else if (hasBuilding && em.HasComponent<TrainingState>(entity))
        {
            Debug.Log("✅ Entity is a training building - should show training menu");
        }
        else
        {
            Debug.Log("ℹ️ Entity has no actions (only info panel will show)");
        }
        
        Debug.Log("=== End Diagnostics ===");
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        // Show simple overlay
        var rect = new Rect(10, 10, 300, 80);
        GUI.Box(rect, "UnifiedUI Debug");
        
        var selection = RTSInput.CurrentSelection;
        int count = selection != null ? selection.Count : 0;
        
        GUI.Label(new Rect(20, 30, 280, 20), $"Selected: {count} entities");
        GUI.Label(new Rect(20, 50, 280, 20), $"InfoPanel: {EntityInfoPanel.PanelVisible}");
        GUI.Label(new Rect(20, 70, 280, 20), $"ActionPanel: {EntityActionPanel.PanelVisible}");
    }
}