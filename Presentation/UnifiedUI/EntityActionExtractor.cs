// EntityActionExtractor.cs
// Extracts available actions for selected entities

using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using TheWaningBorder.Economy;

// Helper to convert CostBlock to Cost
public static class CostBlockExtensions
{
    public static Cost ToCost(this CostBlock costBlock)
    {
        return Cost.Of(
            supplies: costBlock.Supplies,
            iron: costBlock.Iron,
            crystal: costBlock.Crystal,
            veilsteel: costBlock.Veilsteel,
            glow: costBlock.Glow
        );
    }
}

public static class EntityActionExtractor
{
    /// <summary>
    /// Main entry point: Get action info for any entity.
    /// </summary>
    public static EntityActionInfo GetActionInfo(Entity entity, EntityManager em)
    {
        if (!em.Exists(entity))
            return CreateNoneInfo();
        
        // Check for builder (building placement)
        if (em.HasComponent<CanBuild>(entity))
            return GetBuilderActions(entity, em);
        
        // Check for training building (unit training)
        if (em.HasComponent<TrainingState>(entity))
            return GetTrainingActions(entity, em);
        
        // No actions available
        return CreateNoneInfo();
    }
    
    // ==================== BUILDER ACTIONS ====================
    
    private static EntityActionInfo GetBuilderActions(Entity entity, EntityManager em)
    {
        var info = new EntityActionInfo();
        info.Type = ActionType.BuildingPlacement;
        
        // Get faction for cost checking
        Faction faction = Faction.Blue;
        if (em.HasComponent<FactionTag>(entity))
            faction = em.GetComponentData<FactionTag>(entity).Value;
        
        // Define buildable structures
        var buildingIds = new[]
        {
            "Hut",
            "GatherersHut",
            "Barracks",
            "TempleOfRidan",    // Shrine
            "VaultOfAlmierra",  // Vault
            "FiendstoneKeep"    // Keep
        };
        
        var actionList = new List<ActionButton>();
        
        foreach (var buildingId in buildingIds)
        {
            // Get cost from BuildCosts
            if (!BuildCosts.TryGet(buildingId, out var cost))
                cost = new Cost(); // Zero cost
            
            // Check if faction can afford it
            bool canAfford = FactionEconomy.CanAfford(em, faction, cost);
            
            // Create button
            var button = new ActionButton
            {
                Id = buildingId,
                Label = GetBuildingLabel(buildingId),
                Icon = LoadIcon(buildingId),
                Cost = cost,
                CanAfford = canAfford,
                Tooltip = CreateBuildingTooltip(buildingId, cost)
            };
            
            actionList.Add(button);
        }
        
        info.Actions = actionList.ToArray();
        return info;
    }
    
    // ==================== TRAINING ACTIONS ====================
    
    private static EntityActionInfo GetTrainingActions(Entity entity, EntityManager em)
    {
        var info = new EntityActionInfo();
        info.Type = ActionType.UnitTraining;
        
        // Get faction for cost checking
        Faction faction = Faction.Blue;
        if (em.HasComponent<FactionTag>(entity))
            faction = em.GetComponentData<FactionTag>(entity).Value;
        
        // Determine what this building can train
        string buildingId = EntityInfoExtractor.DetermineBuildingId(entity, em);
        
        var actionList = new List<ActionButton>();
        
        // Get trainable units from TechTreeDB
        if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding(buildingId, out BuildingDef bdef))
        {
            if (bdef.trains != null && bdef.trains.Length > 0)
            {
                foreach (var unitId in bdef.trains)
                {
                    if (TechTreeDB.Instance.TryGetUnit(unitId, out UnitDef udef))
                    {
                        // Convert CostBlock to Cost
                        Cost unitCost = udef.cost.ToCost();
                        bool canAfford = FactionEconomy.CanAfford(em, faction, unitCost);
                        
                        var button = new ActionButton
                        {
                            Id = unitId,
                            Label = udef.name,
                            Icon = LoadIcon(unitId),
                            Cost = unitCost,
                            CanAfford = canAfford,
                            TrainingTime = udef.trainingTime,
                            Tooltip = CreateUnitTooltip(unitId, udef, unitCost)
                        };
                        
                        actionList.Add(button);
                    }
                }
            }
        }
        
        info.Actions = actionList.ToArray();
        
        // Get training state
        info.TrainingState = GetTrainingState(entity, em);
        
        return info;
    }
    
    // ==================== TRAINING STATE ====================
    
    private static TrainingInfo GetTrainingState(Entity entity, EntityManager em)
    {
        var trainingInfo = new TrainingInfo();
        
        if (!em.HasComponent<TrainingState>(entity))
            return trainingInfo;
        
        var ts = em.GetComponentData<TrainingState>(entity);
        trainingInfo.IsTraining = ts.Busy != 0;
        trainingInfo.TimeRemaining = ts.Remaining;
        
        // Get queue
        if (em.HasBuffer<TrainQueueItem>(entity))
        {
            var queue = em.GetBuffer<TrainQueueItem>(entity);
            var queueList = new List<string>();
            
            for (int i = 0; i < queue.Length; i++)
            {
                queueList.Add(queue[i].UnitId.ToString());
            }
            
            trainingInfo.Queue = queueList.ToArray();
            
            if (queueList.Count > 0)
            {
                trainingInfo.CurrentUnitId = queueList[0];
                
                // Calculate progress
                if (TechTreeDB.Instance != null && 
                    TechTreeDB.Instance.TryGetUnit(trainingInfo.CurrentUnitId, out UnitDef udef))
                {
                    float totalTime = udef.trainingTime > 0 ? udef.trainingTime : 1f;
                    float elapsed = totalTime - ts.Remaining;
                    trainingInfo.Progress = Mathf.Clamp01(elapsed / totalTime);
                }
            }
        }
        
        return trainingInfo;
    }
    
    // ==================== HELPER METHODS ====================
    
    private static string GetBuildingLabel(string buildingId)
    {
        return buildingId switch
        {
            "Hall" => "Hall",
            "Hut" => "Hut",
            "GatherersHut" => "Gatherer's Hut",
            "Barracks" => "Barracks",
            "TempleOfRidan" => "Shrine",
            "VaultOfAlmierra" => "Vault",
            "FiendstoneKeep" => "Keep",
            _ => buildingId
        };
    }
    
    private static Texture2D LoadIcon(string id)
    {
        // Try main icons folder
        var icon = Resources.Load<Texture2D>($"UI/Icons/{id}");
        if (icon != null) return icon;
        
        // Try units subfolder
        icon = Resources.Load<Texture2D>($"UI/Icons/Units/{id}");
        if (icon != null) return icon;
        
        // Try buildings subfolder
        icon = Resources.Load<Texture2D>($"UI/Icons/Buildings/{id}");
        if (icon != null) return icon;
        
        return null;
    }
    
    private static string CreateBuildingTooltip(string buildingId, Cost cost)
    {
        var tooltip = GetBuildingLabel(buildingId);
        
        if (!cost.IsZero)
        {
            tooltip += "\n" + FormatCost(cost);
        }
        
        return tooltip;
    }
    
    private static string CreateUnitTooltip(string unitId, UnitDef udef, Cost cost)
    {
        var tooltip = udef.name;
        
        if (udef.trainingTime > 0)
            tooltip += $"\nTime: {udef.trainingTime:0.0}s";
        
        if (!cost.IsZero)
            tooltip += "\n" + FormatCost(cost);
        
        return tooltip;
    }
    
    private static string FormatCost(Cost cost)
    {
        var parts = new List<string>();
        
        if (cost.Supplies > 0) parts.Add($"S {cost.Supplies}");
        if (cost.Iron > 0) parts.Add($"Fe {cost.Iron}");
        if (cost.Crystal > 0) parts.Add($"Cr {cost.Crystal}");
        if (cost.Veilsteel > 0) parts.Add($"Vs {cost.Veilsteel}");
        if (cost.Glow > 0) parts.Add($"Gl {cost.Glow}");
        
        return string.Join("  ", parts);
    }
    
    private static EntityActionInfo CreateNoneInfo()
    {
        return new EntityActionInfo
        {
            Type = ActionType.None,
            Actions = new ActionButton[0]
        };
    }
}