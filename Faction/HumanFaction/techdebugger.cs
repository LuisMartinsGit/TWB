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

            return;
        }

        // Try to get Barracks
        if (TechTreeDB.Instance.TryGetBuilding("Barracks", out var barracks))
        {

            if (barracks.trains != null && barracks.trains.Length > 0)
            {

            }
            else
            {

            }
        }
        else
        {

        }

        // Try to get units
        if (TechTreeDB.Instance.TryGetUnit("Swordsman", out var swordsman))
        {

        }
        else
        {

        }

        if (TechTreeDB.Instance.TryGetUnit("Archer", out var archer))
        {

        }
        else
        {

        }
    }
}