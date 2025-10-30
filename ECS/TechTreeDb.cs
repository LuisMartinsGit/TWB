// TechTreeDb_FIXED_TIMING.cs
// This version uses Start() instead of Awake() to fix the timing issue
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
        
        // Don't parse here - wait for Start() so humanTechJson can be assigned first
        Debug.Log("[TechTreeDB] Awake() - Instance set, waiting for Start()");
    }

    void Start()
    {
        // NOW parse the JSON - humanTechJson has been assigned by Bootstrap
        Debug.Log("[TechTreeDB] Start() - Beginning JSON parse");
        
        if (humanTechJson == null || string.IsNullOrWhiteSpace(humanTechJson.text))
        {
            Debug.LogError("[TechTreeDB] ✗ humanTechJson is NULL in Start()!");
            Debug.LogError("[TechTreeDB]   Bootstrap should have assigned it!");
            return;
        }

        Debug.Log($"[TechTreeDB] ✓ humanTechJson assigned! Length: {humanTechJson.text.Length}");

        try
        {
            string json = humanTechJson.text;

            // Find and parse Barracks
            int barracksIndex = json.IndexOf("\"id\": \"Barracks\"");
            if (barracksIndex == -1)
            {
                Debug.LogError("[TechTreeDB] ✗ Could not find Barracks in JSON!");
                return;
            }

            Debug.Log($"[TechTreeDB] Found Barracks at position {barracksIndex}");

            // Find trains array
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
                        {
                            trains.Add(cleaned);
                        }
                    }

                    var barracks = new BuildingDef
                    {
                        id = "Barracks",
                        hp = 800,
                        trains = trains.ToArray(),
                        lineOfSight = 18f,
                        radius = 1.6f,
                        armorType = "structure_human",
                        baseDefense = new DefenseBlock()
                    };

                    _buildingsById["Barracks"] = barracks;
                    Debug.Log($"[TechTreeDB] ✓ Barracks loaded with {trains.Count} trainable units");
                }
            }

            // Add units
            AddUnit("Swordsman", 120, 7.0f);
            AddUnit("Archer", 90, 7.0f);
            AddUnit("Builder", 60, 5.0f);
            AddUnit("Miner", 70, 5.0f);

            Debug.Log("╔═══════════════════════════════════════════════════╗");
            Debug.Log("║   TECHTREEDB LOADED SUCCESSFULLY!                 ║");
            Debug.Log("╚═══════════════════════════════════════════════════╝");
            Debug.Log($"[TechTreeDB] Loaded {_buildingsById.Count} buildings and {_unitsById.Count} units");

            if (TryGetBuilding("Barracks", out var b))
            {
                Debug.Log($"[TechTreeDB] ✓ Barracks: trains {string.Join(", ", b.trains)}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TechTreeDB] Error: {ex.Message}");
            Debug.LogError($"Stack: {ex.StackTrace}");
        }
    }

    void AddUnit(string id, float hp, float trainingTime)
    {
        _unitsById[id] = new UnitDef
        {
            id = id,
            hp = hp,
            trainingTime = trainingTime,
            speed = 5f,
            damage = 10,
            damageType = "melee",
            armorType = "infantry",
            attackRange = 1.5f,
            lineOfSight = 12f,
            defense = new DefenseBlock()
        };
    }
}

[Serializable]
public class UnitDef
{
    public string id;
    public float hp;
    public float speed;
    public float trainingTime;
    public string armorType;
    public float damage;
    public string damageType;
    public DefenseBlock defense;
    public float attackRange;
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