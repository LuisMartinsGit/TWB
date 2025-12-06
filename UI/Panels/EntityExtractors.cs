// EntityExtractors.cs
// Helper classes to extract UI display info from ECS entities
// Location: Assets/Scripts/UI/Common/EntityExtractors.cs

using System.Collections.Generic;
using Unity.Entities;
using TheWaningBorder.Core;
using TheWaningBorder.Economy;

namespace TheWaningBorder.UI
{
    /// <summary>
    /// Extracts display information from entities for EntityInfoPanel.
    /// </summary>
    public static class EntityInfoExtractor
    {
        public static EntityDisplayInfo GetDisplayInfo(Entity entity, EntityManager em)
        {
            var info = new EntityDisplayInfo
            {
                Name = "Unknown",
                Type = "Entity",
                Description = "",
                Portrait = null,
                CurrentHealth = 0,
                MaxHealth = 0,
                Faction = "Neutral",
                HasCombatStats = false,
                Attack = 0,
                Defense = 0,
                Speed = 0,
                HasResourceGeneration = false,
                SuppliesPerMinute = 0,
                IronPerMinute = 0
            };

            if (!em.Exists(entity)) return info;

            // Faction
            if (em.HasComponent<FactionTag>(entity))
                info.Faction = em.GetComponentData<FactionTag>(entity).Value.ToString();

            // Health
            if (em.HasComponent<Health>(entity))
            {
                var health = em.GetComponentData<Health>(entity);
                info.CurrentHealth = (int)health.Value;
                info.MaxHealth = (int)health.Max;
            }

            // Combat stats
            if (em.HasComponent<Damage>(entity))
            {
                info.HasCombatStats = true;
                info.Attack = (int)em.GetComponentData<Damage>(entity).Value;
            }
            if (em.HasComponent<Defense>(entity))
            {
                info.HasCombatStats = true;
                var def = em.GetComponentData<Defense>(entity);
                info.Defense = (int)def.Melee; // or average of all defense types
            }
            if (em.HasComponent<MoveSpeed>(entity))
            {
                info.Speed = em.GetComponentData<MoveSpeed>(entity).Value;
            }

            // Resource generation
            if (em.HasComponent<SuppliesIncome>(entity))
            {
                info.HasResourceGeneration = true;
                info.SuppliesPerMinute = em.GetComponentData<SuppliesIncome>(entity).PerMinute;
            }
            if (em.HasComponent<IronIncome>(entity))
            {
                info.HasResourceGeneration = true;
                info.IronPerMinute = em.GetComponentData<IronIncome>(entity).PerMinute;
            }

            // Type and name
            if (em.HasComponent<BuildingTag>(entity))
            {
                info.Type = "Building";
                info.Name = GetBuildingName(entity, em);
            }
            else if (em.HasComponent<UnitTag>(entity))
            {
                info.Type = "Unit";
                info.Name = GetUnitName(entity, em);
            }

            return info;
        }

        private static string GetBuildingName(Entity entity, EntityManager em)
        {
            if (em.HasComponent<HallTag>(entity)) return "Hall";
            if (em.HasComponent<BarracksTag>(entity)) return "Barracks";
            if (em.HasComponent<GathererHutTag>(entity)) return "Gatherer's Hut";
            if (em.HasComponent<HutTag>(entity)) return "Hut";
            if (em.HasComponent<DepotTag>(entity)) return "Depot";
            if (em.HasComponent<WorkshopTag>(entity)) return "Workshop";
            if (em.HasComponent<TempleTag>(entity)) return "Temple";
            return "Building";
        }

        private static string GetUnitName(Entity entity, EntityManager em)
        {
            if (em.HasComponent<CanBuild>(entity)) return "Builder";
            if (em.HasComponent<MinerTag>(entity)) return "Miner";
            // Add more unit type checks as needed
            return "Unit";
        }
    }

    /// <summary>
    /// Extracts action information from entities for EntityActionPanel.
    /// </summary>
    public static class EntityActionExtractor
    {
        public static EntityActionInfo GetActionInfo(Entity entity, EntityManager em)
        {
            var info = new EntityActionInfo
            {
                Type = ActionType.None,
                Actions = new List<ActionButton>()
            };

            if (!em.Exists(entity)) return info;

            // Check if this is a builder (can place buildings)
            if (em.HasComponent<CanBuild>(entity))
            {
                info.Type = ActionType.BuildingPlacement;
                info.Actions = GetBuildingActions();
                return info;
            }

            // Check if this is a training building
            if (em.HasComponent<BuildingTag>(entity))
            {
                var trainingActions = GetTrainingActions(entity, em);
                if (trainingActions.Count > 0)
                {
                    info.Type = ActionType.UnitTraining;
                    info.Actions = trainingActions;
                    return info;
                }
            }

            return info;
        }

        private static List<ActionButton> GetBuildingActions()
        {
            var actions = new List<ActionButton>();

            // Get available buildings from TechTreeDB
            if (TechTreeDB.Instance != null)
            {
                foreach (var building in TechTreeDB.Instance.GetAllBuildings())
                {
                    actions.Add(new ActionButton
                    {
                        Id = building.id,
                        Label = building.name,
                        Tooltip = building.role ?? "",
                        Cost = building.cost != null ? new Cost
                        {
                            Supplies = building.cost.Supplies,
                            Iron = building.cost.Iron,
                            Crystal = building.cost.Crystal
                        } : default,
                        Enabled = true,
                        Icon = null
                    });
                }
            }

            return actions;
        }

        private static List<ActionButton> GetTrainingActions(Entity entity, EntityManager em)
        {
            var actions = new List<ActionButton>();

            // Determine what this building can train
            if (em.HasComponent<HallTag>(entity) || em.HasComponent<BarracksTag>(entity))
            {
                if (TechTreeDB.Instance != null)
                {
                    // Get units this building can train
                    // You may need to customize based on building type
                    foreach (var unit in TechTreeDB.Instance.GetAllUnits())
                    {
                        actions.Add(new ActionButton
                        {
                            Id = unit.id,
                            Label = unit.name,
                            Tooltip = unit.unitClass ?? "",
                            Cost = unit.cost != null ? new Cost
                            {
                                Supplies = unit.cost.Supplies,
                                Iron = unit.cost.Iron,
                                Crystal = unit.cost.Crystal
                            } : default,
                            Enabled = true,
                            Icon = null
                        });
                    }
                }
            }

            return actions;
        }
    }
}