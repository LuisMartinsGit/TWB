// TechTreeDB.cs - COMPLETE COMPREHENSIVE VERSION
// Parses ALL buildings, units, technologies, sects, and game data from JSON
// Replace your TechTreeDB.cs with this

using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class TechTreeDB : MonoBehaviour
{
    public static TechTreeDB Instance { get; private set; }

    [Header("Human tech JSON (TextAsset)")]
    public TextAsset humanTechJson;

    private readonly Dictionary<string, UnitDef> _unitsById = new();
    private readonly Dictionary<string, BuildingDef> _buildingsById = new();
    private readonly Dictionary<string, TechnologyDef> _technologiesById = new();
    private readonly Dictionary<string, SectDef> _sectsById = new();
    
    private CombatProfile _combatProfile;
    private string _faction;
    private List<string> _resources = new();

    public bool TryGetUnit(string id, out UnitDef def) => _unitsById.TryGetValue(id, out def);
    public bool TryGetBuilding(string id, out BuildingDef def) => _buildingsById.TryGetValue(id, out def);
    public bool TryGetTechnology(string id, out TechnologyDef def) => _technologiesById.TryGetValue(id, out def);
    public bool TryGetSect(string id, out SectDef def) => _sectsById.TryGetValue(id, out def);
    
    public CombatProfile CombatProfile => _combatProfile;
    public string Faction => _faction;
    public List<string> Resources => _resources;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (humanTechJson == null || string.IsNullOrWhiteSpace(humanTechJson.text))
        {
            Debug.LogError("[TechTreeDB] No JSON provided!");
            return;
        }

        try
        {
            string json = humanTechJson.text;
            
            // Parse faction info
            _faction = ParseString(json, "faction", "Human");
            
            // Parse resources
            ParseResourcesArray(json);
            
            // Parse combat profile
            ParseCombatProfile(json);

            // Parse Era 1 buildings
            Debug.Log("[TechTreeDB] Parsing Era 1 buildings...");
            ParseBuilding(json, "Hall");
            ParseBuilding(json, "Hut");
            ParseBuilding(json, "GatherersHut");
            ParseBuilding(json, "Barracks");
            ParseBuilding(json, "ShrineOfAhridan");
            ParseBuilding(json, "WarriorsHall");
            ParseBuilding(json, "VaultOfLymwerra");

            // Parse Era 1 units
            Debug.Log("[TechTreeDB] Parsing Era 1 units...");
            ParseUnit(json, "Builder");
            ParseUnit(json, "Miner");
            ParseUnit(json, "Swordsman");
            ParseUnit(json, "Archer");
            ParseUnit(json, "Litharch");
            ParseUnit(json, "Scout");

            // Parse Era 1 technologies
            Debug.Log("[TechTreeDB] Parsing Era 1 technologies...");
            ParseTechnology(json, "Research_Era2");
            ParseTechnology(json, "ImprovedTools");
            ParseTechnology(json, "StorageCarts");
            ParseTechnology(json, "BasicDrills");
            ParseTechnology(json, "WoodenArmor");

            // Parse Era 2 - Runai
            Debug.Log("[TechTreeDB] Parsing Runai (Era 2)...");
            ParseBuilding(json, "ThessarasBazaar");
            ParseBuilding(json, "Runai_Outpost");
            ParseBuilding(json, "Runai_TradeHub");
            ParseBuilding(json, "Runai_Vault");
            ParseBuilding(json, "Runai_VeilsteelFoundry");
            ParseBuilding(json, "Runai_SiegeWorkshop");
            
            ParseUnit(json, "Runai_Spearman");
            ParseUnit(json, "Runai_Skirmisher");
            ParseUnit(json, "Runai_Raider");
            ParseUnit(json, "Runai_SandBallista");
            ParseUnit(json, "Runai_Caravan");
            ParseUnit(json, "Runai_Escort");

            // Parse Era 2 - Feraldis
            Debug.Log("[TechTreeDB] Parsing Feraldis (Era 2)...");
            ParseBuilding(json, "FiendstoneKeep");
            ParseBuilding(json, "Feraldis_HuntingLodge");
            ParseBuilding(json, "Feraldis_LoggingStation");
            ParseBuilding(json, "Feraldis_Foundry");
            ParseBuilding(json, "Feraldis_Tower");
            ParseBuilding(json, "Feraldis_Longhouse");
            ParseBuilding(json, "Feraldis_SiegeYard");
            
            ParseUnit(json, "Feraldis_Berserker");
            ParseUnit(json, "Feraldis_Hunter");
            ParseUnit(json, "Feraldis_WarboarRider");
            ParseUnit(json, "Feraldis_SiegeRam");

            // Parse Era 2 - Alanthor
            Debug.Log("[TechTreeDB] Parsing Alanthor (Era 2)...");
            ParseBuilding(json, "KingsCourt");
            ParseBuilding(json, "Alanthor_Wall");
            ParseBuilding(json, "Alanthor_Tower");
            ParseBuilding(json, "Alanthor_Garrison");
            ParseBuilding(json, "Alanthor_Stable");
            ParseBuilding(json, "Alanthor_SiegeYard");
            ParseBuilding(json, "Alanthor_Smelter");
            ParseBuilding(json, "Alanthor_Crucible");
            
            ParseUnit(json, "Alanthor_Sentinel");
            ParseUnit(json, "Alanthor_Crossbowman");
            ParseUnit(json, "Alanthor_Cataphract");
            ParseUnit(json, "Alanthor_Ballista");

            // Parse Sects
            Debug.Log("[TechTreeDB] Parsing Sects...");
            ParseAllSects(json);

            // Log summary
            Debug.Log($"[TechTreeDB] ✓ Loaded {_buildingsById.Count} buildings, {_unitsById.Count} units, " +
                     $"{_technologiesById.Count} technologies, {_sectsById.Count} sects");

            // Log sample units with stats
            foreach (var kvp in _unitsById)
            {
                if (kvp.Key == "Swordsman" || kvp.Key == "Archer" || kvp.Key == "Litharch")
                {
                    var unit = kvp.Value;
                    Debug.Log($"[TechTreeDB] {unit.id}: HP={unit.hp}, Speed={unit.speed}, " +
                             $"Dmg={unit.damage}, Range={unit.attackRange}, LOS={unit.lineOfSight}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TechTreeDB] Parse error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARSE RESOURCES
    // ═══════════════════════════════════════════════════════════════════════
    void ParseResourcesArray(string json)
    {
        int resourcesIndex = json.IndexOf("\"resources\":");
        if (resourcesIndex == -1) return;
        
        int arrayStart = json.IndexOf("[", resourcesIndex);
        int arrayEnd = json.IndexOf("]", arrayStart);
        
        if (arrayStart == -1 || arrayEnd == -1) return;
        
        string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
        string[] parts = arrayContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            string cleaned = part.Trim().Trim('"');
            if (!string.IsNullOrEmpty(cleaned))
                _resources.Add(cleaned);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARSE COMBAT PROFILE
    // ═══════════════════════════════════════════════════════════════════════
    void ParseCombatProfile(string json)
    {
        _combatProfile = new CombatProfile();
        
        int profileIndex = json.IndexOf("\"combatProfile\":");
        if (profileIndex == -1) return;
        
        // This is complex - for now just store the hint
        _combatProfile.defenseFormulaHint = ParseString(json.Substring(profileIndex), 
            "defenseFormulaHint", "");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARSE BUILDING
    // ═══════════════════════════════════════════════════════════════════════
    void ParseBuilding(string json, string buildingId)
    {
        string searchPattern = $"\"id\": \"{buildingId}\"";
        int buildingIndex = json.IndexOf(searchPattern);
        
        if (buildingIndex == -1)
        {
            Debug.LogWarning($"[TechTreeDB] Building not found: {buildingId}");
            return;
        }

        // Find enclosing object
        int objStart = json.LastIndexOf('{', buildingIndex);
        int objEnd = FindMatchingBrace(json, objStart);
        
        if (objStart == -1 || objEnd == -1) return;
        
        string buildingJson = json.Substring(objStart, objEnd - objStart + 1);

        var building = new BuildingDef
        {
            id = buildingId,
            name = ParseString(buildingJson, "name", buildingId),
            role = ParseString(buildingJson, "role", ""),
            hp = ParseFloat(buildingJson, "hp", 0, 1000),
            armorType = ParseString(buildingJson, "armorType", "structure_human"),
            lineOfSight = ParseFloat(buildingJson, "lineOfSight", 0, 20),
            radius = ParseFloat(buildingJson, "radius", 0, 1.6f),
            defense = ParseDefenseBlock(buildingJson),
            trains = ParseStringArray(buildingJson, "trains"),
            research = ParseStringArray(buildingJson, "research"),
            cost = ParseCostBlock(buildingJson)
        };

        _buildingsById[buildingId] = building;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARSE UNIT
    // ═══════════════════════════════════════════════════════════════════════
    void ParseUnit(string json, string unitId)
    {
        string searchPattern = $"\"id\": \"{unitId}\"";
        int unitIndex = json.IndexOf(searchPattern);
        
        if (unitIndex == -1)
        {
            Debug.LogWarning($"[TechTreeDB] Unit not found: {unitId}");
            return;
        }

        int objStart = json.LastIndexOf('{', unitIndex);
        int objEnd = FindMatchingBrace(json, objStart);
        
        if (objStart == -1 || objEnd == -1) return;
        
        string unitJson = json.Substring(objStart, objEnd - objStart + 1);

        var unit = new UnitDef
        {
            id = unitId,
            unitClass = ParseString(unitJson, "class", ""),
            name = ParseString(unitJson, "name", unitId),
            hp = ParseFloat(unitJson, "hp", 0, 100),
            speed = ParseFloat(unitJson, "speed", 0, 5),
            trainingTime = ParseFloat(unitJson, "trainingTime", 0, 5),
            damage = ParseFloat(unitJson, "damage", 0, 10),
            attackRange = ParseFloat(unitJson, "attackRange", 0, 1.5f),
            minAttackRange = ParseFloat(unitJson, "minAttackRange", 0, 0f),
            lineOfSight = ParseFloat(unitJson, "lineOfSight", 0, 20),
            armorType = ParseString(unitJson, "armorType", "infantry"),
            damageType = ParseString(unitJson, "damageType", "melee"),
            defense = ParseDefenseBlock(unitJson),
            cost = ParseCostBlock(unitJson),
            buildSpeed = ParseFloat(unitJson, "buildSpeed", 0, 0f),
            gatheringSpeed = ParseFloat(unitJson, "gatheringSpeed", 0, 0f),
            carryCapacity = (int)ParseFloat(unitJson, "carryCapacity", 0, 0f),
            healsPerSecond = ParseFloat(unitJson, "healsPerSecond", 0, 0f)
        };

        _unitsById[unitId] = unit;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARSE TECHNOLOGY
    // ═══════════════════════════════════════════════════════════════════════
    void ParseTechnology(string json, string techId)
    {
        string searchPattern = $"\"id\": \"{techId}\"";
        int techIndex = json.IndexOf(searchPattern);
        
        if (techIndex == -1)
        {
            Debug.LogWarning($"[TechTreeDB] Technology not found: {techId}");
            return;
        }

        int objStart = json.LastIndexOf('{', techIndex);
        int objEnd = FindMatchingBrace(json, objStart);
        
        if (objStart == -1 || objEnd == -1) return;
        
        string techJson = json.Substring(objStart, objEnd - objStart + 1);

        var tech = new TechnologyDef
        {
            id = techId,
            name = ParseString(techJson, "name", techId),
            effect = ParseString(techJson, "effect", ""),
            desc = ParseString(techJson, "desc", ""),
            role = ParseString(techJson, "role", ""),
            researchTime = ParseFloat(techJson, "researchTime", 0, 30),
            researchAt = ParseString(techJson, "researchAt", ""),
            cost = ParseCostBlock(techJson)
        };

        _technologiesById[techId] = tech;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARSE SECTS
    // ═══════════════════════════════════════════════════════════════════════
    void ParseAllSects(string json)
    {
        int sectsIndex = json.IndexOf("\"sects\":");
        if (sectsIndex == -1) return;
        
        // Find the list array
        int listIndex = json.IndexOf("\"list\":", sectsIndex);
        if (listIndex == -1) return;
        
        int arrayStart = json.IndexOf("[", listIndex);
        if (arrayStart == -1) return;
        
        // Find all sect objects in the array
        int searchPos = arrayStart;
        while (true)
        {
            int sectStart = json.IndexOf("{", searchPos);
            if (sectStart == -1 || sectStart > json.IndexOf("]", arrayStart)) break;
            
            int sectEnd = FindMatchingBrace(json, sectStart);
            if (sectEnd == -1) break;
            
            string sectJson = json.Substring(sectStart, sectEnd - sectStart + 1);
            string sectId = ParseString(sectJson, "id", "");
            
            if (!string.IsNullOrEmpty(sectId))
            {
                var sect = new SectDef
                {
                    id = sectId,
                    order = ParseString(sectJson, "order", ""),
                    affinity = ParseString(sectJson, "affinity", "")
                };
                
                _sectsById[sectId] = sect;
            }
            
            searchPos = sectEnd + 1;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER PARSERS
    // ═══════════════════════════════════════════════════════════════════════
    DefenseBlock ParseDefenseBlock(string json)
    {
        var defense = new DefenseBlock();
        
        int defenseStart = json.IndexOf("\"defense\":");
        if (defenseStart == -1) 
        {
            // Try baseDefense
            defenseStart = json.IndexOf("\"baseDefense\":");
            if (defenseStart == -1) return defense;
        }
        
        int objStart = json.IndexOf('{', defenseStart);
        int objEnd = json.IndexOf('}', objStart);
        
        if (objStart == -1 || objEnd == -1) return defense;
        
        string defenseJson = json.Substring(objStart, objEnd - objStart + 1);
        
        defense.melee = (int)ParseFloat(defenseJson, "melee", 0, 0);
        defense.ranged = (int)ParseFloat(defenseJson, "ranged", 0, 0);
        defense.siege = (int)ParseFloat(defenseJson, "siege", 0, 0);
        defense.magic = (int)ParseFloat(defenseJson, "magic", 0, 0);
        
        return defense;
    }

    CostBlock ParseCostBlock(string json)
    {
        var cost = new CostBlock();
        
        int costStart = json.IndexOf("\"cost\":");
        if (costStart == -1) return cost;
        
        int objStart = json.IndexOf('{', costStart);
        int objEnd = json.IndexOf('}', objStart);
        
        if (objStart == -1 || objEnd == -1) return cost;
        
        string costJson = json.Substring(objStart, objEnd - objStart + 1);
        
        cost.Supplies = (int)ParseFloat(costJson, "Supplies", 0, 0);
        cost.Crystal = (int)ParseFloat(costJson, "Crystal", 0, 0);
        cost.Iron = (int)ParseFloat(costJson, "Iron", 0, 0);
        cost.Veilsteel = (int)ParseFloat(costJson, "Veilsteel", 0, 0);
        cost.Glow = (int)ParseFloat(costJson, "Glow", 0, 0);
        
        return cost;
    }

    string[] ParseStringArray(string json, string fieldName)
    {
        var result = new List<string>();
        
        string pattern = $"\"{fieldName}\":";
        int index = json.IndexOf(pattern);
        if (index == -1) return result.ToArray();
        
        int arrayStart = json.IndexOf("[", index);
        int arrayEnd = json.IndexOf("]", arrayStart);
        
        if (arrayStart == -1 || arrayEnd == -1) return result.ToArray();
        
        string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
        string[] parts = arrayContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            string cleaned = part.Trim().Trim('"');
            if (!string.IsNullOrEmpty(cleaned))
                result.Add(cleaned);
        }
        
        return result.ToArray();
    }

    int FindMatchingBrace(string json, int openBraceIndex)
    {
        int braceCount = 1;
        int searchPos = openBraceIndex + 1;
        
        while (braceCount > 0 && searchPos < json.Length)
        {
            if (json[searchPos] == '{') braceCount++;
            else if (json[searchPos] == '}') braceCount--;
            
            if (braceCount == 0) return searchPos;
            searchPos++;
        }
        
        return -1;
    }

    float ParseFloat(string json, string fieldName, int startIndex, float defaultValue)
    {
        string pattern = $"\"{fieldName}\":";
        int index = json.IndexOf(pattern, startIndex);
        
        if (index == -1) return defaultValue;
        
        int start = index + pattern.Length;
        int end = json.IndexOfAny(new[] { ',', '}', '\n', '\r' }, start);
        
        if (end == -1) return defaultValue;
        
        string valueStr = json.Substring(start, end - start).Trim();
        
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        
        return defaultValue;
    }

    string ParseString(string json, string fieldName, string defaultValue)
    {
        string pattern = $"\"{fieldName}\":";
        int index = json.IndexOf(pattern);
        
        if (index == -1) return defaultValue;
        
        int start = json.IndexOf('"', index + pattern.Length);
        if (start == -1) return defaultValue;
        
        int end = json.IndexOf('"', start + 1);
        if (end == -1) return defaultValue;
        
        return json.Substring(start + 1, end - start - 1);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// DATA STRUCTURES
// ═══════════════════════════════════════════════════════════════════════

[Serializable]
public class UnitDef
{
    public string id;
    public string name;
    public string unitClass;
    public float hp;
    public float speed;
    public float trainingTime;
    public string armorType;
    public float damage;
    public string damageType;
    public DefenseBlock defense;
    public float attackRange;
    public float minAttackRange;
    public float lineOfSight;
    public CostBlock cost;
    
    // Support unit fields
    public float buildSpeed;
    public float gatheringSpeed;
    public int carryCapacity;
    public float healsPerSecond;
}

[Serializable]
public class BuildingDef
{
    public string id;
    public string name;
    public string role;
    public float hp;
    public string armorType;
    public DefenseBlock defense;
    public float radius;
    public float lineOfSight;
    public string[] trains;
    public string[] research;
    public CostBlock cost;
}

[Serializable]
public class TechnologyDef
{
    public string id;
    public string name;
    public string effect;
    public string desc;
    public string role;
    public float researchTime;
    public string researchAt;
    public CostBlock cost;
}

[Serializable]
public class SectDef
{
    public string id;
    public string order;
    public string affinity;
}

[Serializable]
public class DefenseBlock
{
    public int melee;
    public int ranged;
    public int siege;
    public int magic;
}

[Serializable]
public class CostBlock
{
    public int Supplies;
    public int Crystal;
    public int Iron;
    public int Veilsteel;
    public int Glow;
}

[Serializable]
public class CombatProfile
{
    public string defenseFormulaHint;
    // Can expand with damage type modifiers, etc.
}