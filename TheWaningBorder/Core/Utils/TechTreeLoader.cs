using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace TheWaningBorder.Core.Utils
{
    public static class TechTreeLoader
    {
        private static TechTreeData _data;
        private static bool _isLoaded = false;
        
        public static TechTreeData Data => _data;
        public static bool IsLoaded => _isLoaded;
        
        public static bool LoadTechTree()
        {
            if (_isLoaded) return true;
            
            // Try multiple paths
            string[] paths = {
                "TechTree",
                "Data/TechTree", 
                "JSON/TechTree",
                "Config/TechTree"
            };
            
            TextAsset jsonAsset = null;
            foreach (var path in paths)
            {
                jsonAsset = UnityEngine.Resources.Load<TextAsset>(path);
                if (jsonAsset != null) break;
            }
            
            if (jsonAsset == null)
            {
                Debug.LogError("[TechTreeLoader] TechTree.json not found in Resources!");
                return false;
            }
            
            try
            {
                _data = JsonUtility.FromJson<TechTreeData>(jsonAsset.text);
                _isLoaded = true;
                Debug.Log($"[TechTreeLoader] Loaded TechTree v{_data.version} for faction: {_data.faction}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TechTreeLoader] Failed to parse TechTree.json: {e.Message}");
                return false;
            }
        }
        
        public static UnitDef GetUnitDef(string unitId)
        {
            if (!_isLoaded || _data == null) return null;
            
            foreach (var era in _data.eras)
            {
                if (era.units != null)
                {
                    foreach (var unit in era.units)
                    {
                        if (unit.id == unitId) return unit;
                    }
                }
                
                if (era.cultures != null)
                {
                    foreach (var culture in era.cultures)
                    {
                        if (culture.units != null)
                        {
                            foreach (var unit in culture.units)
                            {
                                if (unit.id == unitId) return unit;
                            }
                        }
                    }
                }
            }
            return null;
        }
        
        public static BuildingDef GetBuildingDef(string buildingId)
        {
            if (!_isLoaded || _data == null) return null;
            
            foreach (var era in _data.eras)
            {
                if (era.buildings != null)
                {
                    foreach (var building in era.buildings)
                    {
                        if (building.id == buildingId) return building;
                    }
                }
                
                if (era.cultures != null)
                {
                    foreach (var culture in era.cultures)
                    {
                        if (culture.buildings != null)
                        {
                            foreach (var building in culture.buildings)
                            {
                                if (building.id == buildingId) return building;
                            }
                        }
                    }
                }
            }
            return null;
        }
        
        public static float GetDamageModifier(string damageType, string armorType)
        {
            if (!_isLoaded || _data?.combatProfile?.modifiers == null) return 1f;
            
            if (_data.combatProfile.modifiers.TryGetValue(damageType, out var damageModifiers))
            {
                if (damageModifiers.TryGetValue(armorType, out float modifier))
                {
                    return modifier;
                }
            }
            return 1f;
        }
    }
    
    [Serializable]
    public class TechTreeData
    {
        public string faction;
        public string version;
        public List<string> resources;
        public int maxEra;
        public CombatProfile combatProfile;
        public List<Era> eras;
    }
    
    [Serializable]
    public class CombatProfile
    {
        public List<string> damageTypes;
        public List<string> armorTypes;
        public Dictionary<string, Dictionary<string, float>> modifiers;
    }
    
    [Serializable]
    public class Era
    {
        public int era;
        public string theme;
        public List<BuildingDef> buildings;
        public List<UnitDef> units;
        public List<Culture> cultures;
    }
    
    [Serializable]
    public class Culture
    {
        public string id;
        public string style;
        public List<BuildingDef> buildings;
        public List<UnitDef> units;
    }
    
    [Serializable]
    public class UnitDef
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
        
        // Special fields
        public float buildSpeed;
        public float gatheringSpeed;
        public int carryCapacity;
        public float healsPerSecond;
        internal float projectileSpeed;

    }
    
    [Serializable]
    public class BuildingDef
    {
        public string id;
        public string name;
        public string role;
        public float hp;
        public string armorType;
        public Defense baseDefense;
        public float lineOfSight;
        public List<string> trains;
        public Dictionary<string, int> cost;
        public Provides provides;
        public string upgradesTo;
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
    public class Provides
    {
        public int population;
        public int sectPoints;
    }
}
