// EntityInfoExtractor.cs
// Extracts display information from ECS entities

using Unity.Entities;
using UnityEngine;
using TheWaningBorder.Factions.Humans;
using TheWaningBorder.Factions.Humans.Era1.Units;
public static class EntityInfoExtractor
{
    /// <summary>
    /// Main entry point: Get display info for any entity.
    /// </summary>
    public static EntityDisplayInfo GetDisplayInfo(Entity entity, EntityManager em)
    {
        if (!em.Exists(entity))
            return CreateUnknownInfo();
        
        // Determine if unit or building
        bool isUnit = em.HasComponent<UnitTag>(entity);
        bool isBuilding = em.HasComponent<BuildingTag>(entity);
        
        if (isUnit)
            return GetUnitInfo(entity, em);
        else if (isBuilding)
            return GetBuildingInfo(entity, em);
        else
            return CreateUnknownInfo();
    }
    
    // ==================== UNIT INFO ====================
    
    private static EntityDisplayInfo GetUnitInfo(Entity entity, EntityManager em)
    {
        var info = new EntityDisplayInfo();
        info.Type = "Unit";
        
        // Determine unit ID
        string unitId = DetermineUnitId(entity, em);
        info.Name = unitId;
        
        // Load from TechTreeDB if available
        if (HumanTech.Instance != null && HumanTech.Instance.TryGetUnit(unitId, out UnitDef udef))
        {
            info.Name = udef.name;
            info.Description = GetUnitDescription(unitId);
        }
        
        // Load portrait
        info.Portrait = LoadPortrait(unitId, "Units");
        
        // Extract current stats from components
        if (em.HasComponent<Damage>(entity))
            info.Attack = em.GetComponentData<Damage>(entity).Value;
        
        if (em.HasComponent<Defense>(entity))
        {
            var def = em.GetComponentData<Defense>(entity);
            // Use melee defense as the primary defense value
            info.Defense = (int)def.Melee;
        }
        
        if (em.HasComponent<Health>(entity))
        {
            var hp = em.GetComponentData<Health>(entity);
            info.CurrentHealth = hp.Value;
            info.MaxHealth = hp.Max;
        }
        
        if (em.HasComponent<MoveSpeed>(entity))
            info.Speed = em.GetComponentData<MoveSpeed>(entity).Value;
        
        return info;
    }
    
    // ==================== BUILDING INFO ====================
    
    private static EntityDisplayInfo GetBuildingInfo(Entity entity, EntityManager em)
    {
        var info = new EntityDisplayInfo();
        info.Type = "Building";
        
        // Determine building ID
        string buildingId = DetermineBuildingId(entity, em);
        info.Name = buildingId;
        
        // Load from HumanTech if available
        if (HumanTech.Instance != null && HumanTech.Instance.TryGetBuilding(buildingId, out BuildingDef bdef))
        {
            info.Name = bdef.name;
            info.Description = GetBuildingDescription(buildingId);
        }
        
        // Load portrait
        info.Portrait = LoadPortrait(buildingId, "Buildings");
        
        // Extract stats
        if (em.HasComponent<Health>(entity))
        {
            var hp = em.GetComponentData<Health>(entity);
            info.CurrentHealth = hp.Value;
            info.MaxHealth = hp.Max;
        }
        
        if (em.HasComponent<Defense>(entity))
        {
            var def = em.GetComponentData<Defense>(entity);
            info.Defense = (int)def.Melee;
        }
        
        // Extract resource generation
        if (em.HasComponent<SuppliesIncome>(entity))
        {
            var income = em.GetComponentData<SuppliesIncome>(entity);
            info.SuppliesPerMinute = income.PerMinute;
        }
        
        // Future: Add other resource types when implemented
        
        return info;
    }
    
    // ==================== UNIT ID DETECTION ====================
    
    public static string DetermineUnitId(Entity entity, EntityManager em)
    {
        // Method 1: Check for specific component tags
        if (em.HasComponent<CanBuild>(entity))
            return "Builder";
        
        if (em.HasComponent<ArcherTag>(entity))
            return "Archer";
        
        // Method 2: Check UnitTag class
        if (em.HasComponent<UnitTag>(entity))
        {
            var unitTag = em.GetComponentData<UnitTag>(entity);
            if (unitTag.Class == UnitClass.Melee)
                return "Swordsman"; // Assumption: melee = swordsman
        }
        
        // Method 3: Check PresentationId
        if (em.HasComponent<PresentationId>(entity))
        {
            int id = em.GetComponentData<PresentationId>(entity).Id;
            return id switch
            {
                200 => "Builder",
                201 => "Swordsman",
                202 => "Archer",
                _ => "Unknown Unit"
            };
        }
        
        return "Unknown Unit";
    }
    
    // ==================== BUILDING ID DETECTION ====================
    
    public static string DetermineBuildingId(Entity entity, EntityManager em)
    {
        // Method 1: Check for specific component tags
        if (em.HasComponent<BarracksTag>(entity))
            return "Barracks";
        
        if (em.HasComponent<HutTag>(entity))
            return "Hut";
        
        if (em.HasComponent<GathererHutTag>(entity))
            return "GatherersHut";
        
        if (em.HasComponent<TempleTag>(entity))
            return "TempleOfRidan";
        
        if (em.HasComponent<VaultTag>(entity))
            return "VaultOfAlmierra";
        
        // Method 2: Check if it's a base building (Hall)
        if (em.HasComponent<BuildingTag>(entity))
        {
            var buildingTag = em.GetComponentData<BuildingTag>(entity);
            if (buildingTag.IsBase == 1)
                return "Hall";
        }
        
        // Method 3: Check PresentationId
        if (em.HasComponent<PresentationId>(entity))
        {
            int id = em.GetComponentData<PresentationId>(entity).Id;
            return id switch
            {
                100 => "Hall",
                500 => "GatherersHut",
                510 => "Barracks",
                505 => "TempleOfRidan",
                506 => "VaultOfAlmierra",
                507 => "FiendstoneKeep",
                _ => "Unknown Building"
            };
        }
        
        return "Unknown Building";
    }
    
    // ==================== DESCRIPTIONS ====================
    
    private static string GetUnitDescription(string unitId)
    {
        return unitId switch
        {
            "Builder" => "A worker unit. Can construct buildings.",
            "Swordsman" => "A melee infantry unit. Strong in close combat.",
            "Archer" => "A ranged unit. Attacks from a distance.",
            _ => "A unit."
        };
    }
    
    private static string GetBuildingDescription(string buildingId)
    {
        return buildingId switch
        {
            "Hall" => "Main base. Trains Builders and generates Supplies.",
            "Hut" => "Housing structure. Generates Supplies.",
            "GatherersHut" => "Resource gathering structure.",
            "Barracks" => "Military building. Trains Swordsmen and Archers.",
            "TempleOfRidan" => "Sacred temple dedicated to Ahridan.",
            "VaultOfAlmierra" => "Secure vault of the Runai culture.",
            "FiendstoneKeep" => "Fortified keep of the Feraldis culture.",
            _ => "A building."
        };
    }
    
    // ==================== RESOURCE LOADING ====================
    
    private static Texture2D LoadPortrait(string id, string category)
    {
        // Try to load from Resources/UI/Portraits/
        var portrait = Resources.Load<Texture2D>($"UI/Portraits/{id}");
        if (portrait != null) return portrait;
        
        // Fallback: Try loading from Icons
        portrait = Resources.Load<Texture2D>($"UI/Icons/{id}");
        if (portrait != null) return portrait;
        
        // Fallback: Try category-specific path
        portrait = Resources.Load<Texture2D>($"UI/Icons/{category}/{id}");
        if (portrait != null) return portrait;
        
        return null;
    }
    
    private static EntityDisplayInfo CreateUnknownInfo()
    {
        return new EntityDisplayInfo
        {
            Name = "Unknown Entity",
            Type = "Unknown",
            Description = "No information available."
        };
    }
}