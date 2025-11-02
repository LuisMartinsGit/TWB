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

        CheckTechTreeDB();
        CheckBarracksPanel();
        CheckBootstrap();
        CheckEntityManager();

    }

    void CheckTechTreeDB()
    {

        // Check if TechTreeDBAuthoring exists in scene
        var authoring = FindObjectOfType<TechTreeDBAuthoring>();
        if (authoring == null)
        {

            return;
        }

        if (authoring.humanTechJson == null)
        {

            return;
        }

        // Check if Instance was created
        if (TechTreeDB.Instance == null)
        {

            return;
        }

        // Check if Barracks data loaded
        if (TechTreeDB.Instance.TryGetBuilding("Barracks", out var barracks))
        {

            if (barracks.trains == null)
            {

            }
            else if (barracks.trains.Length == 0)
            {

            }
            else
            {

                foreach (var unit in barracks.trains)
                {

                }
            }
        }

    }

    void CheckBarracksPanel()
    {

        var panel = FindObjectOfType<BarracksPanel>();
        if (panel == null)
        {

            return;
        }

    }

    void CheckBootstrap()
    {

        var bootstrap = GameObject.Find("RTS_Bootstrap");
        if (bootstrap == null)
        {

            return;
        }

    }

    void CheckEntityManager()
    {

        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {

            return;
        }

        var em = world.EntityManager;
        var barracksQuery = em.CreateEntityQuery(typeof(BarracksTag));
        int barracksCount = barracksQuery.CalculateEntityCount();

        if (barracksCount == 0)
        {

        }
    }
}