// UnifiedUIManager.cs
// Central manager for the unified UI system
// Coordinates EntityInfoPanel and EntityActionPanel

using UnityEngine;
using Unity.Entities;

public class UnifiedUIManager : MonoBehaviour
{
    private static UnifiedUIManager _instance;
    
    private World _world;
    private EntityManager _em;
    private EntityInfoPanel _infoPanel;
    private EntityActionPanel _actionPanel;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated)
            _em = _world.EntityManager;

        _infoPanel = gameObject.AddComponent<EntityInfoPanel>();
        _actionPanel = gameObject.AddComponent<EntityActionPanel>();
        
        Debug.Log("[UnifiedUI] Unified UI Manager initialized successfully");
    }

    void Update()
    {
        // Refresh world/EM if needed
        if (_em.Equals(default(EntityManager)))
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != null && _world.IsCreated)
                _em = _world.EntityManager;
        }
    }

    /// <summary>
    /// Get the first valid selected entity from RTSInput.
    /// Returns Entity.Null if nothing is selected.
    /// </summary>
    public static Entity GetFirstSelectedEntity()
    {
        var sel = RTSInput.CurrentSelection;
        if (sel == null || sel.Count == 0) return Entity.Null;
        
        // Return first existing entity that belongs to the player
        var manager = GetEntityManager();
        if (manager.Equals(default(EntityManager))) return Entity.Null;
        
        for (int i = 0; i < sel.Count; i++)
        {
            var e = sel[i];
            if (!manager.Exists(e)) continue;
            
            // Only show UI for player-owned entities (Blue faction)
            if (manager.HasComponent<FactionTag>(e))
            {
                var faction = manager.GetComponentData<FactionTag>(e).Value;
                if (faction == Faction.Blue)
                    return e;
            }
        }
        
        return Entity.Null;
    }
    
    /// <summary>
    /// Check if mouse pointer is over any UI panel.
    /// Used by RTSInput to prevent click-through.
    /// </summary>
    public static bool IsPointerOverAnyPanel()
    {
        return EntityInfoPanel.IsPointerOver() || EntityActionPanel.IsPointerOver();
    }
    
    /// <summary>
    /// Get the EntityManager for use by other UI components.
    /// </summary>
    public static EntityManager GetEntityManager()
    {
        if (_instance != null && !_instance._em.Equals(default(EntityManager)))
            return _instance._em;
            
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
            return world.EntityManager;
            
        return default;
    }
}