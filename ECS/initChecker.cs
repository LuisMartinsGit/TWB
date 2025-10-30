// InitializationChecker.cs
// Add this to your Bootstrap or any GameObject that loads early
// This will diagnose ALL potential initialization issues

using UnityEngine;

public class InitializationChecker : MonoBehaviour
{
    void Start()
    {
        // Wait a moment for everything to initialize
        Invoke(nameof(RunChecks), 0.5f);
    }

    void RunChecks()
    {
        Debug.Log("========================================");
        Debug.Log("INITIALIZATION DIAGNOSTIC REPORT");
        Debug.Log("========================================");

        CheckTechTreeDB();
        CheckBarracksPanel();
        CheckBootstrap();
        CheckEntityManager();

        Debug.Log("========================================");
        Debug.Log("END DIAGNOSTIC REPORT");
        Debug.Log("========================================");
    }

    void CheckTechTreeDB()
    {
        Debug.Log("\n=== TechTreeDB Check ===");

        // Check if TechTreeDBAuthoring exists in scene
        var authoring = FindObjectOfType<TechTreeDBAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("✗ NO TechTreeDBAuthoring found in scene!");
            Debug.LogError("  FIX: Create a GameObject and add TechTreeDBAuthoring component");
            Debug.LogError("       Then assign your TechTree.json to the humanTechJson field");
            return;
        }

        Debug.Log("✓ TechTreeDBAuthoring found in scene");

        if (authoring.humanTechJson == null)
        {
            Debug.LogError("✗ TechTreeDBAuthoring.humanTechJson is NOT assigned!");
            Debug.LogError("  FIX: In Inspector, drag TechTree.json into the humanTechJson field");
            return;
        }

        Debug.Log($"✓ humanTechJson assigned: {authoring.humanTechJson.name}");

        // Check if Instance was created
        if (TechTreeDB.Instance == null)
        {
            Debug.LogError("✗ TechTreeDB.Instance is NULL!");
            Debug.LogError("  This means TechTreeDB.Awake() hasn't run yet or failed");
            return;
        }

        Debug.Log("✓ TechTreeDB.Instance exists");

        // Check if Barracks data loaded
        if (TechTreeDB.Instance.TryGetBuilding("Barracks", out var barracks))
        {
            Debug.Log($"✓ Barracks building loaded");
            Debug.Log($"  - HP: {barracks.hp}");
            Debug.Log($"  - LineOfSight: {barracks.lineOfSight}");

            if (barracks.trains == null)
            {
                Debug.LogError("✗ Barracks.trains is NULL!");
                Debug.LogError("  This means the BuildingDef class doesn't match JSON");
                Debug.LogError("  FIX: Replace TechTreeDb.cs with TechTreeDb_FIXED.cs");
            }
            else if (barracks.trains.Length == 0)
            {
                Debug.LogError("✗ Barracks.trains is EMPTY!");
                Debug.LogError("  Check your TechTree.json - does Barracks have trains array?");
            }
            else
            {
                Debug.Log($"✓ Barracks.trains has {barracks.trains.Length} units:");
                foreach (var unit in barracks.trains)
                {
                    Debug.Log($"    - {unit}");
                }
            }
        }
        else
        {
            Debug.LogError("✗ Barracks NOT found in TechTreeDB!");
            Debug.LogError("  Check TechTree.json - does Era 1 have a Barracks building?");
        }

        // Check units
        bool swordsmanFound = TechTreeDB.Instance.TryGetUnit("Swordsman", out var swordsman);
        bool archerFound = TechTreeDB.Instance.TryGetUnit("Archer", out var archer);

        if (swordsmanFound)
            Debug.Log($"✓ Swordsman unit loaded (TrainTime: {swordsman.trainingTime}s)");
        else
            Debug.LogWarning("✗ Swordsman unit NOT found");

        if (archerFound)
            Debug.Log($"✓ Archer unit loaded (TrainTime: {archer.trainingTime}s)");
        else
            Debug.LogWarning("✗ Archer unit NOT found");
    }

    void CheckBarracksPanel()
    {
        Debug.Log("\n=== BarracksPanel Check ===");

        var panel = FindObjectOfType<BarracksPanel>();
        if (panel == null)
        {
            Debug.LogError("✗ NO BarracksPanel component found in scene!");
            Debug.LogError("  FIX: Add this line to Bootstrap.cs:");
            Debug.LogError("       TryAddComponent<BarracksPanel>(go);");
            return;
        }

        Debug.Log($"✓ BarracksPanel found on: {panel.gameObject.name}");
    }

    void CheckBootstrap()
    {
        Debug.Log("\n=== Bootstrap Check ===");

        var bootstrap = GameObject.Find("RTS_Bootstrap");
        if (bootstrap == null)
        {
            Debug.LogWarning("⚠ RTS_Bootstrap GameObject not found");
            Debug.LogWarning("  This is OK if your bootstrap uses a different name");
            return;
        }

        Debug.Log($"✓ RTS_Bootstrap found");

        var components = bootstrap.GetComponents<MonoBehaviour>();
        Debug.Log($"  Components on RTS_Bootstrap: {components.Length}");
        foreach (var comp in components)
        {
            if (comp != null)
                Debug.Log($"    - {comp.GetType().Name}");
        }

        bool hasBarracksPanel = bootstrap.GetComponent<BarracksPanel>() != null;
        if (hasBarracksPanel)
            Debug.Log("  ✓ Has BarracksPanel");
        else
            Debug.LogError("  ✗ Missing BarracksPanel!");
    }

    void CheckEntityManager()
    {
        Debug.Log("\n=== ECS Check ===");

        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogError("✗ DefaultGameObjectInjectionWorld is NULL or not created!");
            return;
        }

        Debug.Log("✓ ECS World exists");

        var em = world.EntityManager;
        var barracksQuery = em.CreateEntityQuery(typeof(BarracksTag));
        int barracksCount = barracksQuery.CalculateEntityCount();

        Debug.Log($"  Barracks entities in world: {barracksCount}");

        if (barracksCount == 0)
        {
            Debug.LogWarning("  ⚠ No barracks exist yet (build one to test)");
        }
    }
}