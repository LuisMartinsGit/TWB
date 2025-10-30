// TechTreeDebugger.cs
// Add this as a MonoBehaviour to debug what's being loaded
using UnityEngine;

public class TechTreeDebugger : MonoBehaviour
{
    [ContextMenu("Debug TechTree Data")]
    void DebugTechTree()
    {
        if (TechTreeDB.Instance == null)
        {
            Debug.LogError("[TechTreeDebug] TechTreeDB.Instance is NULL!");
            return;
        }

        Debug.Log("[TechTreeDebug] === TechTreeDB Status ===");
        
        // Try to get Barracks
        if (TechTreeDB.Instance.TryGetBuilding("Barracks", out var barracks))
        {
            Debug.Log($"[TechTreeDebug] ✓ Found Barracks building!");
            Debug.Log($"  - ID: {barracks.id}");
            Debug.Log($"  - HP: {barracks.hp}");
            Debug.Log($"  - LineOfSight: {barracks.lineOfSight}");
            Debug.Log($"  - ArmorType: {barracks.armorType}");
            
            if (barracks.trains != null && barracks.trains.Length > 0)
            {
                Debug.Log($"  - Trains: {string.Join(", ", barracks.trains)}");
            }
            else
            {
                Debug.LogWarning("  - Trains array is NULL or EMPTY! <-- THIS IS THE PROBLEM");
            }
        }
        else
        {
            Debug.LogError("[TechTreeDebug] ✗ Barracks NOT FOUND in TechTreeDB!");
        }

        // Try to get units
        if (TechTreeDB.Instance.TryGetUnit("Swordsman", out var swordsman))
        {
            Debug.Log($"[TechTreeDebug] ✓ Found Swordsman unit!");
            Debug.Log($"  - Training Time: {swordsman.trainingTime}s");
        }
        else
        {
            Debug.LogWarning("[TechTreeDebug] ✗ Swordsman NOT FOUND");
        }

        if (TechTreeDB.Instance.TryGetUnit("Archer", out var archer))
        {
            Debug.Log($"[TechTreeDebug] ✓ Found Archer unit!");
            Debug.Log($"  - Training Time: {archer.trainingTime}s");
        }
        else
        {
            Debug.LogWarning("[TechTreeDebug] ✗ Archer NOT FOUND");
        }
    }
}