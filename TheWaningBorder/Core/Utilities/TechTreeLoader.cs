using System;
using System.IO;
using UnityEngine;

namespace TheWaningBorder.Core.Utilities
{
    /// <summary>
    /// Utility class for loading and managing TechTree.json data
    /// CRITICAL: All game data must come from JSON - no hardcoded values!
    /// </summary>
    public static class TechTreeLoader
    {
        private static TechTreeData _cachedData;
        private static readonly string JSON_PATH = "StreamingAssets/TechTree.json";
        private static readonly string FULL_PATH = Path.Combine(Application.dataPath, JSON_PATH);

        /// <summary>
        /// Gets the loaded TechTree data, loading it if necessary
        /// </summary>
        public static TechTreeData Data
        {
            get
            {
                if (_cachedData == null)
                {
                    LoadTechTree();
                }
                return _cachedData;
            }
        }

        /// <summary>
        /// Force reload the TechTree.json file
        /// </summary>
        public static void ReloadTechTree()
        {
            _cachedData = null;
            LoadTechTree();
        }

        /// <summary>
        /// Load the TechTree.json file
        /// </summary>
        private static void LoadTechTree()
        {
            if (!File.Exists(FULL_PATH))
            {
                throw new FileNotFoundException(
                    $"CRITICAL ERROR: TechTree.json not found at {FULL_PATH}!\n" +
                    "The game cannot function without configuration data!\n" +
                    "Please ensure TechTree.json is placed in the StreamingAssets folder."
                );
            }

            try
            {
                string jsonContent = File.ReadAllText(FULL_PATH);
                _cachedData = JsonUtility.FromJson<TechTreeData>(jsonContent);

                if (_cachedData == null)
                {
                    throw new InvalidOperationException("TechTree.json deserialization returned null!");
                }

                ValidateTechTreeData();
                Debug.Log($"TechTree.json loaded successfully. Version: {_cachedData.version}, Faction: {_cachedData.faction}");
            }
            catch (ArgumentException ae)
            {
                throw new InvalidOperationException(
                    $"CRITICAL ERROR: Failed to parse TechTree.json!\n" +
                    $"JSON Error: {ae.Message}\n" +
                    "Please verify the JSON file is properly formatted."
                );
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"CRITICAL ERROR: Failed to load TechTree.json!\n" +
                    $"Error: {e.Message}"
                );
            }
        }

        /// <summary>
        /// Validate that the loaded data contains required fields
        /// </summary>
        private static void ValidateTechTreeData()
        {
            if (string.IsNullOrEmpty(_cachedData.faction))
                throw new InvalidOperationException("TechTree.json: 'faction' field is required!");

            if (string.IsNullOrEmpty(_cachedData.version))
                throw new InvalidOperationException("TechTree.json: 'version' field is required!");

            if (_cachedData.eras == null || _cachedData.eras.Count == 0)
                throw new InvalidOperationException("TechTree.json: 'eras' array cannot be empty!");

            if (_cachedData.resources == null || _cachedData.resources.Count == 0)
                throw new InvalidOperationException("TechTree.json: 'resources' array cannot be empty!");

            if (_cachedData.combatProfile == null)
                throw new InvalidOperationException("TechTree.json: 'combatProfile' is required!");

            Debug.Log($"TechTree validation passed. Found {GetTotalUnitCount()} units, {GetTotalBuildingCount()} buildings.");
        }

        /// <summary>
        /// Get a specific unit's data by ID
        /// </summary>
        public static UnitData GetUnitData(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                throw new ArgumentNullException(nameof(unitId), "Unit ID cannot be null or empty!");

            foreach (var era in Data.eras)
            {
                if (era.units != null)
                {
                    foreach (var unit in era.units)
                    {
                        if (unit.id == unitId)
                            return unit;
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
                                if (unit.id == unitId)
                                    return unit;
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException(
                $"CRITICAL ERROR: Unit '{unitId}' not found in TechTree.json!\n" +
                "Cannot create unit without configuration data!"
            );
        }

        /// <summary>
        /// Get a specific building's data by ID
        /// </summary>
        public static Building GetBuildingData(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
                throw new ArgumentNullException(nameof(buildingId), "Building ID cannot be null or empty!");

            foreach (var era in Data.eras)
            {
                if (era.buildings != null)
                {
                    foreach (var building in era.buildings)
                    {
                        if (building.id == buildingId)
                            return building;
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
                                if (building.id == buildingId)
                                    return building;
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException(
                $"CRITICAL ERROR: Building '{buildingId}' not found in TechTree.json!\n" +
                "Cannot create building without configuration data!"
            );
        }

        /// <summary>
        /// Get the damage modifier for a specific damage type against an armor type
        /// </summary>
        public static float GetDamageModifier(string damageType, string armorType)
        {
            if (Data.combatProfile?.modifiers == null)
                throw new InvalidOperationException("Combat profile modifiers not found in TechTree.json!");

            if (!Data.combatProfile.modifiers.ContainsKey(damageType))
                throw new InvalidOperationException($"Damage type '{damageType}' not found in combat modifiers!");

            if (!Data.combatProfile.modifiers[damageType].ContainsKey(armorType))
                throw new InvalidOperationException($"Armor type '{armorType}' not found for damage type '{damageType}'!");

            return Data.combatProfile.modifiers[damageType][armorType];
        }

        /// <summary>
        /// Get total count of units in the tech tree
        /// </summary>
        public static int GetTotalUnitCount()
        {
            int count = 0;
            foreach (var era in Data.eras)
            {
                if (era.units != null)
                    count += era.units.Count;

                if (era.cultures != null)
                {
                    foreach (var culture in era.cultures)
                    {
                        if (culture.units != null)
                            count += culture.units.Count;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Get total count of buildings in the tech tree
        /// </summary>
        public static int GetTotalBuildingCount()
        {
            int count = 0;
            foreach (var era in Data.eras)
            {
                if (era.buildings != null)
                    count += era.buildings.Count;

                if (era.cultures != null)
                {
                    foreach (var culture in era.cultures)
                    {
                        if (culture.buildings != null)
                            count += culture.buildings.Count;
                    }
                }
            }
            return count;
        }
    }
}
