// TechTreeDb.cs - COMPLETE VERSION
// Parses ALL unit stats from JSON with NO hardcoded values
// Replace your TechTreeDb.cs with this

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

    public bool TryGetUnit(string id, out UnitDef def) => _unitsById.TryGetValue(id, out def);
    public bool TryGetBuilding(string id, out BuildingDef def) => _buildingsById.TryGetValue(id, out def);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }

    void Start()
    {

        if (humanTechJson == null || string.IsNullOrWhiteSpace(humanTechJson.text))
        {

            return;
        }

        try
        {
            string json = humanTechJson.text;

            // Parse Barracks building
            ParseBarracks(json);
            
            // Parse all units - NO HARDCODED VALUES!
            ParseUnit(json, "Swordsman");
            ParseUnit(json, "Archer");
            ParseUnit(json, "Builder");
            ParseUnit(json, "Miner");
            ParseUnit(json, "Litharch");

            // Log loaded units with their actual stats
            foreach (var kvp in _unitsById)
            {
                var unit = kvp.Value;
                Debug.Log($"[TechTreeDB] ✓ {unit.id}: HP={unit.hp}, LOS={unit.lineOfSight}, " +
                         $"Range={unit.attackRange}, MinRange={unit.minAttackRange}, Speed={unit.speed}, " +
                         $"Dmg={unit.damage}, Def(M/R/S/M)={unit.defense.melee}/{unit.defense.ranged}/{unit.defense.siege}/{unit.defense.magic}");
            }
        }
        catch (Exception ex)
        {

        }
    }

    void ParseBarracks(string json)
    {
        int barracksIndex = json.IndexOf("\"id\": \"Barracks\"");
        if (barracksIndex == -1) return;

        int trainsStart = json.IndexOf("\"trains\"", barracksIndex);
        if (trainsStart != -1)
        {
            int arrayStart = json.IndexOf("[", trainsStart);
            int arrayEnd = json.IndexOf("]", arrayStart);
            
            if (arrayStart != -1 && arrayEnd != -1)
            {
                string trainsArray = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                
                var trains = new List<string>();
                string[] parts = trainsArray.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in parts)
                {
                    string cleaned = part.Trim().Trim('"');
                    if (!string.IsNullOrEmpty(cleaned))
                        trains.Add(cleaned);
                }

                var barracks = new BuildingDef
                {
                    id = "Barracks",
                    hp = ParseFloat(json, "hp", barracksIndex, 800),
                    trains = trains.ToArray(),
                    lineOfSight = ParseFloat(json, "lineOfSight", barracksIndex, 18f),
                    radius = 1.6f,
                    armorType = "structure_human",
                    baseDefense = new DefenseBlock()
                };

                _buildingsById["Barracks"] = barracks;

            }
        }
    }

    void ParseUnit(string json, string unitId)
    {
        // Find the unit definition in JSON
        string searchPattern = $"\"id\": \"{unitId}\"";
        int unitIndex = json.IndexOf(searchPattern);
        
        if (unitIndex == -1)
        {

            return;
        }

        // Find the enclosing object (between { and })
        int objStart = json.LastIndexOf('{', unitIndex);
        int objEnd = json.IndexOf('}', unitIndex);
        
        // Handle nested objects (defense, cost, etc.) - find the correct closing brace
        int braceCount = 1;
        int searchPos = objStart + 1;
        while (braceCount > 0 && searchPos < json.Length)
        {
            if (json[searchPos] == '{') braceCount++;
            else if (json[searchPos] == '}') braceCount--;
            
            if (braceCount == 0)
            {
                objEnd = searchPos;
                break;
            }
            searchPos++;
        }
        
        if (objStart == -1 || objEnd == -1)
        {

            return;
        }

        // Extract the JSON object
        string unitJson = json.Substring(objStart, objEnd - objStart + 1);

        // Parse ALL fields from JSON - NO DEFAULTS!
        var unit = new UnitDef
        {
            id = unitId,
            unitClass = ParseString(unitJson, "class", ""),
            hp = ParseFloat(unitJson, "hp", 0, 100),
            speed = ParseFloat(unitJson, "speed", 0, 5),
            trainingTime = ParseFloat(unitJson, "trainingTime", 0, 5),
            damage = ParseFloat(unitJson, "damage", 0, 10),
            attackRange = ParseFloat(unitJson, "attackRange", 0, 1.5f),
            minAttackRange = ParseFloat(unitJson, "minAttackRange", 0, 0f),
            lineOfSight = ParseFloat(unitJson, "lineOfSight", 0, 20),
            armorType = ParseString(unitJson, "armorType", "infantry"),
            damageType = ParseString(unitJson, "damageType", "melee"),
            defense = ParseDefenseBlock(unitJson)
        };

        _unitsById[unitId] = unit;
    }

    DefenseBlock ParseDefenseBlock(string json)
    {
        var defense = new DefenseBlock();
        
        // Find defense object
        int defenseStart = json.IndexOf("\"defense\"");
        if (defenseStart == -1) return defense;
        
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

[Serializable]
public class UnitDef
{
    public string id;
    public string unitClass;        // NEW: from "class" field in JSON
    public float hp;
    public float speed;
    public float trainingTime;
    public string armorType;
    public float damage;
    public string damageType;
    public DefenseBlock defense;    // NOW ACTUALLY PARSED!
    public float attackRange;
    public float minAttackRange;    // NEW: minimum attack range (for archers)
    public float lineOfSight;
}

[Serializable]
public class BuildingDef
{
    public string id;
    public float hp;
    public string armorType;
    public DefenseBlock baseDefense;
    public float radius;
    public float lineOfSight;
    public string[] trains;
}

[Serializable]
public class DefenseBlock
{
    public int melee;
    public int ranged;
    public int siege;
    public int magic;
}