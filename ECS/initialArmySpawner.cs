using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Humans;
using UnityEngine;

/// <summary>
/// Helper for spawning initial armies with proper spacing and ALL stats applied.
/// Uses the same stat application method as BarracksTrainingSystem.
/// </summary>
public static class InitialArmySpawner
{
    /// <summary>
    /// Spawn a group of units with automatic spacing in formation.
    /// Ensures ALL components and stats are properly applied.
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
            
            // Create unit with all components
            var unit = Swordsman.Create(em, spawnPos, faction);
            
            // Apply ALL stats from JSON (same as BarracksTrainingSystem)
            ApplyUnitStats(em, unit, "Swordsman");
            
            unitIndex++;
        }
        
        // Spawn archers (back ranks)
        for (int i = 0; i < archersCount && unitIndex < totalUnits; i++)
        {
            int row = unitIndex / cols;
            int col = unitIndex % cols;
            
            float3 spawnPos = topLeft + right * (col * spacing) + forward * (row * spacing);
            
            // Create unit with all components
            var unit = Archer.Create(em, spawnPos, faction);
            
            // Apply ALL stats from JSON (same as BarracksTrainingSystem)
            ApplyUnitStats(em, unit, "Archer");
            
            unitIndex++;
        }

    }
    
    /// <summary>
    /// Apply stats from TechTreeDB to a unit - EXACTLY like BarracksTrainingSystem does it.
    /// </summary>
    private static void ApplyUnitStats(EntityManager em, Entity unit, string unitId)
    {
        if (TechTreeDB.Instance == null)
        {

            return;
        }

        if (!TechTreeDB.Instance.TryGetUnit(unitId, out var udef))
        {

            return;
        }

        // Apply basic stats
        em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
        em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
        em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
        em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
        
        // Ensure Radius component exists (for collision/spacing)
        if (!em.HasComponent<Radius>(unit))
        {
            em.AddComponentData(unit, new Radius { Value = 0.5f });
        }
        else
        {
            em.SetComponentData(unit, new Radius { Value = 0.5f });
        }
        
        // Archer-specific stats
        if (unitId == "Archer")
        {
            if (!em.HasComponent<ArcherState>(unit))
            {

                return;
            }
            
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

    }
    public static void SpawnTestArmy(EntityManager em, float3 position, Faction faction)
    {
        SpawnFormation(em, position, swordsmenCount: 0, archersCount: 200, faction);
    }
}