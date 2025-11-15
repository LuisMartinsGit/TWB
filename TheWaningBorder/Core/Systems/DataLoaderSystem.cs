using Unity.Entities;
using UnityEngine;
using TheWaningBorder.Core.Utils;

namespace TheWaningBorder.Core.Systems
{
    /// <summary>
    /// Base system class for all systems that need to load data from TechTree.json
    /// </summary>
    public abstract partial class DataLoaderSystem : SystemBase
    {
        protected TheWaningBorder.Core.Utils.TechTreeData TechTreeData { get; private set; }
        protected bool IsDataLoaded { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            LoadTechTreeData();
        }

        protected void LoadTechTreeData()
        {
            if (!TechTreeLoader.IsLoaded)
            {
                if (!TechTreeLoader.LoadTechTree())
                {
                    Debug.LogError("[DataLoaderSystem] Failed to load TechTree.json!");
                    IsDataLoaded = false;
                    return;
                }
            }
            
            TechTreeData = TechTreeLoader.Data;
            IsDataLoaded = true;
            Debug.Log($"[DataLoaderSystem] TechTree data loaded successfully");
        }

        protected UnitDef GetUnitData(string unitId)
        {
            if (!IsDataLoaded)
            {
                Debug.LogError($"[DataLoaderSystem] Cannot get unit data - TechTree not loaded!");
                return null;
            }
            
            var unitDef = TechTreeLoader.GetUnitDef(unitId);
            if (unitDef == null)
            {
                Debug.LogError($"[DataLoaderSystem] Unit definition not found: {unitId}");
            }
            
            return unitDef;
        }
        
        protected BuildingDef GetBuildingData(string buildingId)
        {
            if (!IsDataLoaded)
            {
                Debug.LogError($"[DataLoaderSystem] Cannot get building data - TechTree not loaded!");
                return null;
            }
            
            var buildingDef = TechTreeLoader.GetBuildingDef(buildingId);
            if (buildingDef == null)
            {
                Debug.LogError($"[DataLoaderSystem] Building definition not found: {buildingId}");
            }
            
            return buildingDef;
        }
    }
}
