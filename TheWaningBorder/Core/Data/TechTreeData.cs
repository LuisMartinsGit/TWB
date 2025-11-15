using System;
using System.Collections.Generic;

namespace TheWaningBorder.Core
{
    /// <summary>
    /// Data structures for deserializing TechTree.json
    /// All values must come from JSON - no hardcoded defaults!
    /// </summary>
    [Serializable]
    public class TechTreeData
    {
        public string faction;
        public string version;
        public List<string> resources;
        public int maxEra;
        public CombatProfile combatProfile;
        public Dictionary<string, int> sectPointsPerEra;
        public Evolution evolution;
        public List<Era> eras;
    }

    [Serializable]
    public class CombatProfile
    {
        public List<string> damageTypes;
        public List<string> armorTypes;
        public Dictionary<string, Dictionary<string, float>> modifiers;
        public string defenseFormulaHint;
    }

    [Serializable]
    public class Evolution
    {
        public EvolutionStep toEra2;
        public EvolutionStep toEra3;
        public EvolutionStep toEra4;
        public EvolutionStep toEra5;
    }

    [Serializable]
    public class EvolutionStep
    {
        public Dictionary<string, int> cost;
        public List<string> requiresOneOfBuildings;
        public int requiresTempleLevel;
    }

    [Serializable]
    public class Era
    {
        public int era;
        public string theme;
        public string buildingStyle;
        public List<Building> buildings;
        public List<UnitData> units;
        public List<Technology> technologies;
        public List<Culture> cultures;
        public string note;
    }

    [Serializable]
    public class Culture
    {
        public string id;
        public string style;
        public Building main;
        public List<Building> buildings;
        public List<UnitData> units;
        public List<Technology> technologies;
    }

    [Serializable]
    public class Building
    {
        public string id;
        public string name;
        public string role;
        public float hp;
        public string armorType;
        public Defense baseDefense;
        public float lineOfSight;
        public List<string> trains;
        public List<string> research;
        public Dictionary<string, int> cost;
        public Provides provides;
        public string upgradesTo;
        public Aura aura;
        public bool uniquePerPlayer;
        public int minEra;
        public int maxEra;
        public int levels;
        public Passive passive;
        public Plots plots;
        public UIHints uiHints;
        public string type;
        public BuildingSystems systems;
        public List<string> abilities;
        public int queues;
        public string notes;
    }

    [Serializable]
    public class UnitData
    {
        public string id;
        public string @class;
        public float hp;
        public float speed;
        public float trainingTime;
        public string armorType;
        public float damage;
        public string damageType;
        public Defense defense;
        public float lineOfSight;
        public float attackRange;
        public float minAttackRange;
        public Dictionary<string, int> cost;
        public int popCost;
        
        // Optional fields for special units
        public float buildSpeed;
        public float gatheringSpeed;
        public int carryCapacity;
        public float healsPerSecond;
        public string projectileType;
        public float projectileSpeed;
        public List<string> abilities;
        public string specialNotes;
    }

    [Serializable]
    public class Defense
    {
        public float melee;
        public float ranged;
        public float siege;
        public float magic;
    }

    [Serializable]
    public class Technology
    {
        public string id;
        public string name;
        public string role;
        public string desc;
        public string effect;
        public Dictionary<string, int> cost;
        public float researchTime;
        public Effects effects;
        public List<string> requires;
        public string researchAt;
        public Prerequisites prereq;
    }

    [Serializable]
    public class Effects
    {
        public float gatherSpeedMult;
        public int carryCapacityBonus;
        public float meleeAttackSpeedMult;
        public int meleeDefenseAdd;
        public float trainSpeedMult;
        public float buildSpeedMult;
    }

    [Serializable]
    public class Prerequisites
    {
        public List<string> requiresOneOfBuildings;
        public List<string> requiresAllBuildings;
        public List<string> requiresTechnologies;
    }

    [Serializable]
    public class Provides
    {
        public int population;
        public int sectPoints;
    }

    [Serializable]
    public class Aura
    {
        public float suppliesPerMinute;
        public float radius;
        public float trainSpeedMult;
    }

    [Serializable]
    public class Passive
    {
        public int sectPointsOnBuild;
    }

    [Serializable]
    public class Plots
    {
        public int slotCount;
        public List<string> allowedModules;
    }

    [Serializable]
    public class UIHints
    {
        public string description;
        public bool showsSlots;
    }

    [Serializable]
    public class BuildingSystems
    {
        public bool deposits;
        public float interestRatePctPerMin;
    }
}
