using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Humans;
using Unity.Transforms;

/// <summary>
/// Helper for spawning initial armies AFTER TechTreeDB is loaded.
/// Ensures all stats from JSON are properly applied.
/// Call this from your Bootstrap/initialization code.
/// </summary>
public static class InitialArmyBootstrap
{
    /// <summary>
    /// Spawn initial armies with proper stats from TechTreeDB.
    /// MUST be called after TechTreeDB.Load() has been called!
    /// </summary>
    public static void SpawnInitialArmies(EntityManager em)
    {
        // CRITICAL: Verify TechTreeDB is loaded
        if (TechTreeDB.Instance == null)
        {
            Debug.LogError("InitialArmyBootstrap: TechTreeDB.Instance is NULL! Call TechTreeDB.Load() first!");
            Debug.LogError("InitialArmyBootstrap: Units will NOT spawn correctly!");
            return;
        }

        // Verify unit definitions exist
        if (!TechTreeDB.Instance.TryGetUnit("Swordsman", out var swordsman))
        {
            Debug.LogError("InitialArmyBootstrap: Swordsman not found in TechTreeDB!");
            return;
        }

        if (!TechTreeDB.Instance.TryGetUnit("Archer", out var archer))
        {
            Debug.LogError("InitialArmyBootstrap: Archer not found in TechTreeDB!");
            return;
        }

        Debug.Log($"InitialArmyBootstrap: TechTreeDB loaded successfully!");
        Debug.Log($"  Swordsman stats: HP={swordsman.hp}, Speed={swordsman.speed}, Damage={swordsman.damage}, LOS={swordsman.lineOfSight}");
        Debug.Log($"  Archer stats: HP={archer.hp}, Speed={archer.speed}, Damage={archer.damage}, LOS={archer.lineOfSight}");

        // Get spawn positions from your game's spawn system
        // Example: Spawn blue army at origin
        var blueSpawnPos = new float3(10, 0, 10);
        
        SpawnArmyWithStats(em, blueSpawnPos, Faction.Blue, swordsmenCount: 11, archersCount: 12);
        
        // Example: Spawn red army opposite
        var redSpawnPos = new float3(-10, 0, -10);
        SpawnArmyWithStats(em, redSpawnPos, Faction.Red, swordsmenCount: 11, archersCount: 12);

        Debug.Log("InitialArmyBootstrap: Initial armies spawned successfully!");
    }

    private static void SpawnArmyWithStats(
        EntityManager em,
        float3 centerPos,
        Faction faction,
        int swordsmenCount,
        int archersCount)
    {
        // Calculate formation
        int totalUnits = swordsmenCount + archersCount;
        int cols = (int)math.ceil(math.sqrt(totalUnits));
        int rows = (int)math.ceil(totalUnits / (float)cols);
        
        float spacing = 1.5f;
        float3 right = new float3(1, 0, 0);
        float3 forward = new float3(0, 0, 1);
        
        float width = (cols - 1) * spacing;
        float height = (rows - 1) * spacing;
        float3 topLeft = centerPos - right * (width * 0.5f) - forward * (height * 0.5f);
        
        int unitIndex = 0;
        
        // Spawn swordsmen
        for (int i = 0; i < swordsmenCount && unitIndex < totalUnits; i++)
        {
            int row = unitIndex / cols;
            int col = unitIndex % cols;
            float3 spawnPos = topLeft + right * (col * spacing) + forward * (row * spacing);
            
            CreateSwordsmanWithStats(em, spawnPos, faction);
            unitIndex++;
        }
        
        // Spawn archers
        for (int i = 0; i < archersCount && unitIndex < totalUnits; i++)
        {
            int row = unitIndex / cols;
            int col = unitIndex % cols;
            float3 spawnPos = topLeft + right * (col * spacing) + forward * (row * spacing);
            
            CreateArcherWithStats(em, spawnPos, faction);
            unitIndex++;
        }
    }

    private static void CreateSwordsmanWithStats(EntityManager em, float3 pos, Faction faction)
    {
        // Create entity with ALL components
        var unit = em.CreateEntity(
            typeof(PresentationId),
            typeof(LocalTransform),
            typeof(FactionTag),
            typeof(UnitTag),
            typeof(Health),
            typeof(MoveSpeed),
            typeof(Damage),
            typeof(LineOfSight),
            typeof(Target),
            typeof(Radius),
            typeof(AttackCooldown)
        );

        em.SetComponentData(unit, new PresentationId { Id = 201 });
        em.SetComponentData(unit, Unity.Transforms.LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
        em.SetComponentData(unit, new FactionTag { Value = faction });
        em.SetComponentData(unit, new UnitTag { Class = UnitClass.Melee });
        em.SetComponentData(unit, new Target { Value = Entity.Null });
        em.SetComponentData(unit, new Radius { Value = 0.5f });
        em.SetComponentData(unit, new AttackCooldown { Cooldown = 1.5f, Timer = 0f });

        // Apply stats from TechTreeDB
        if (TechTreeDB.Instance.TryGetUnit("Swordsman", out var udef))
        {
            em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
            em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
            em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
            em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
            
            Debug.Log($"Swordsman created: HP={udef.hp}, Speed={udef.speed}, Damage={udef.damage}, LOS={udef.lineOfSight}");
        }
        else
        {
            Debug.LogError("Failed to get Swordsman stats from TechTreeDB!");
        }
    }

    private static void CreateArcherWithStats(EntityManager em, float3 pos, Faction faction)
    {
        // Create entity with ALL components
        var unit = em.CreateEntity(
            typeof(PresentationId),
            typeof(LocalTransform),
            typeof(FactionTag),
            typeof(UnitTag),
            typeof(ArcherTag),
            typeof(Health),
            typeof(MoveSpeed),
            typeof(Damage),
            typeof(LineOfSight),
            typeof(ArcherState),
            typeof(Target),
            typeof(Radius),
            typeof(AttackCooldown)
        );

        em.SetComponentData(unit, new PresentationId { Id = 202 });
        em.SetComponentData(unit, Unity.Transforms.LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
        em.SetComponentData(unit, new FactionTag { Value = faction });
        em.SetComponentData(unit, new UnitTag { Class = UnitClass.Ranged });
        em.SetComponentData(unit, new Target { Value = Entity.Null });
        em.SetComponentData(unit, new Radius { Value = 0.5f });
        em.SetComponentData(unit, new AttackCooldown { Cooldown = 1.5f, Timer = 0f });

        // Apply stats from TechTreeDB
        if (TechTreeDB.Instance.TryGetUnit("Archer", out var udef))
        {
            em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
            em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
            em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
            em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
            
            // Archer-specific state
            em.SetComponentData(unit, new ArcherState
            {
                CurrentTarget = Entity.Null,
                AimTimer = 0,
                AimTimeRequired = 0.5f,
                CooldownTimer = 0,
                MinRange = udef.minAttackRange,
                MaxRange = udef.attackRange,
                HeightRangeMod = 4f,
                IsRetreating = 0,
                IsFiring = 0
            });
            
            Debug.Log($"Archer created: HP={udef.hp}, Speed={udef.speed}, Damage={udef.damage}, LOS={udef.lineOfSight}, MinRange={udef.minAttackRange}, MaxRange={udef.attackRange}");
        }
        else
        {
            Debug.LogError("Failed to get Archer stats from TechTreeDB!");
        }
    }
}