using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Humans;

/// <summary>
/// Helper for spawning initial armies with proper spacing.
/// Use this instead of spawning units at the same position.
/// </summary>
public static class InitialArmySpawner
{
    /// <summary>
    /// Spawn a group of units with automatic spacing in formation.
    /// </summary>
    public static void SpawnFormation(
        EntityManager em,
        float3 centerPos,
        int swordsmenCount,
        int archersCount,
        Faction faction,
        float spacing = 1.5f)
    {
        // Calculate formation dimensions
        int totalUnits = swordsmenCount + archersCount;
        int cols = (int)math.ceil(math.sqrt(totalUnits));
        int rows = (int)math.ceil(totalUnits / (float)cols);
        
        // Formation vectors (facing forward = positive Z)
        float3 right = new float3(1, 0, 0);
        float3 forward = new float3(0, 0, 1);
        
        // Calculate formation start (top-left corner)
        float width = (cols - 1) * spacing;
        float height = (rows - 1) * spacing;
        float3 topLeft = centerPos - right * (width * 0.5f) - forward * (height * 0.5f);
        
        int unitIndex = 0;
        
        // Spawn swordsmen first (front ranks)
        for (int i = 0; i < swordsmenCount && unitIndex < totalUnits; i++)
        {
            int row = unitIndex / cols;
            int col = unitIndex % cols;
            
            float3 spawnPos = topLeft + right * (col * spacing) + forward * (row * spacing);
            
            var unit = Swordsman.Create(em, spawnPos, faction);
            
            // Set stats if TechTreeDB available
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Swordsman", out var udef))
            {
                em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
                em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
            }
            
            unitIndex++;
        }
        
        // Spawn archers (back ranks)
        for (int i = 0; i < archersCount && unitIndex < totalUnits; i++)
        {
            int row = unitIndex / cols;
            int col = unitIndex % cols;
            
            float3 spawnPos = topLeft + right * (col * spacing) + forward * (row * spacing);
            
            var unit = Archer.Create(em, spawnPos, faction);
            
            // Set stats if TechTreeDB available
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Archer", out var udef))
            {
                em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
                em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
                
                var archerState = new ArcherState
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
                };
                em.SetComponentData(unit, archerState);
            }
            
            unitIndex++;
        }
    }
    
    /// <summary>
    /// Quick spawn for testing - 11 swordsmen, 12 archers in formation.
    /// </summary>
    public static void SpawnTestArmy(EntityManager em, float3 position, Faction faction)
    {
        SpawnFormation(em, position, swordsmenCount: 11, archersCount: 12, faction);
    }
}