// FactionResources.cs
// ECS components for faction resource and income tracking
// Part of: Economy/

using Unity.Entities;
using Unity.Collections;
using EntityWorld = Unity.Entities.World;

namespace TheWaningBorder.Economy
{
    // ═══════════════════════════════════════════════════════════════════════
    // FACTION RESOURCES COMPONENT
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Stores the current resource amounts for a faction.
    /// Attached to a "bank" entity alongside FactionTag.
    /// </summary>
    public struct FactionResources : IComponentData
    {
        /// <summary>Basic resource - used for most construction and training</summary>
        public int Supplies;
        
        /// <summary>Industrial resource - used for military and advanced structures</summary>
        public int Iron;
        
        /// <summary>Magical resource - used for temples and magical units</summary>
        public int Crystal;
        
        /// <summary>Rare resource - used for elite units and advanced technologies</summary>
        public int Veilsteel;
        
        /// <summary>Energy resource - used for special abilities and ultimate powers</summary>
        public int Glow;
        
        // ==================== Helpers ====================
        
        /// <summary>
        /// Create resources with specified values.
        /// </summary>
        public static FactionResources Of(int supplies = 0, int iron = 0, int crystal = 0,
                                          int veilsteel = 0, int glow = 0)
        {
            return new FactionResources
            {
                Supplies = supplies,
                Iron = iron,
                Crystal = crystal,
                Veilsteel = veilsteel,
                Glow = glow
            };
        }
        
        /// <summary>
        /// Default starting resources for a new faction.
        /// </summary>
        public static FactionResources DefaultStart => Of(supplies: 400, iron: 150);
        
        /// <summary>
        /// Check if faction has at least the specified resources.
        /// </summary>
        public bool HasAtLeast(int supplies = 0, int iron = 0, int crystal = 0,
                               int veilsteel = 0, int glow = 0)
        {
            return Supplies >= supplies &&
                   Iron >= iron &&
                   Crystal >= crystal &&
                   Veilsteel >= veilsteel &&
                   Glow >= glow;
        }
        
        /// <summary>
        /// Get total resource value (simple weighted sum).
        /// </summary>
        public int TotalValue => Supplies + (Iron * 2) + (Crystal * 3) + 
                                 (Veilsteel * 5) + (Glow * 4);
        
        public override string ToString()
        {
            return $"S:{Supplies} Fe:{Iron} Cr:{Crystal} Vs:{Veilsteel} Gl:{Glow}";
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════
    // RESOURCE TICK STATE
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Tracks the last game-time second when resource income was applied.
    /// Used by ApplySuppliesIncomeSystem to prevent duplicate income ticks.
    /// </summary>
    public struct ResourceTickState : IComponentData
    {
        /// <summary>The floor(ElapsedTime) value when income was last applied</summary>
        public int LastWholeSecond;
    }
    
    // ═══════════════════════════════════════════════════════════════════════
    // INCOME PROVIDERS
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Attach to any building that provides passive Supplies income.
    /// Example: Hall provides 180 supplies per minute.
    /// </summary>
    public struct SuppliesIncome : IComponentData
    {
        /// <summary>Supplies generated per minute (divided by 60 for per-second rate)</summary>
        public int PerMinute;
        
        /// <summary>Per-second income rate (calculated from PerMinute)</summary>
        public int PerSecond => PerMinute / 60;
    }
    
    /// <summary>
    /// Attach to any building that provides passive Iron income.
    /// Example: Foundry provides iron from nearby deposits.
    /// </summary>
    public struct IronIncome : IComponentData
    {
        /// <summary>Iron generated per minute</summary>
        public int PerMinute;
        
        public int PerSecond => PerMinute / 60;
    }
    
    /// <summary>
    /// Attach to any building that provides passive Crystal income.
    /// Example: Crystal Shrine generates crystal over time.
    /// </summary>
    public struct CrystalIncome : IComponentData
    {
        /// <summary>Crystal generated per minute</summary>
        public int PerMinute;
        
        public int PerSecond => PerMinute / 60;
    }
    
    /// <summary>
    /// Attach to any building that provides passive Veilsteel income.
    /// Example: Advanced smeltery with veilsteel processing.
    /// </summary>
    public struct VeilsteelIncome : IComponentData
    {
        /// <summary>Veilsteel generated per minute</summary>
        public int PerMinute;
        
        public int PerSecond => PerMinute / 60;
    }
    
    /// <summary>
    /// Attach to any building that provides passive Glow income.
    /// Example: Ley line nexus building.
    /// </summary>
    public struct GlowIncome : IComponentData
    {
        /// <summary>Glow generated per minute</summary>
        public int PerMinute;
        
        public int PerSecond => PerMinute / 60;
    }
    
    // ═══════════════════════════════════════════════════════════════════════
    // RESOURCE HELPERS
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Static helper methods for working with faction resources.
    /// </summary>
    public static class FactionResourcesHelper
    {
        /// <summary>
        /// Get resources for a specific faction.
        /// </summary>
        public static bool TryGetFactionResources(Faction faction, out FactionResources resources)
        {
            resources = default;
            
            var world = EntityWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return false;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionResources>()
            );

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var tags = query.ToComponentDataArray<FactionTag>(Allocator.Temp);
            using var resourceData = query.ToComponentDataArray<FactionResources>(Allocator.Temp);

            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Value == faction)
                {
                    resources = resourceData[i];
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Get a formatted display string for resources.
        /// </summary>
        public static string GetDisplayString(FactionResources res)
        {
            return $"📦{res.Supplies} ⚙️{res.Iron} 💎{res.Crystal} ⚫{res.Veilsteel} ✨{res.Glow}";
        }
    }
}