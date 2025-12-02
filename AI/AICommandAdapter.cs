// Assets/Scripts/AI/AICommandAdapter.cs
// Unified command interface for AI systems - routes through CommandRouter
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using TheWaningBorder.Core;
using TheWaningBorder.Multiplayer;

namespace TheWaningBorder.AI
{
    /// <summary>
    /// AICommandAdapter provides command methods for AI systems.
    /// 
    /// Instead of AI systems directly manipulating ECS components like:
    ///   ecb.SetComponent(unit, new DesiredDestination { ... });
    /// 
    /// They should use:
    ///   AICommandAdapter.IssueMove(unit, destination);
    /// 
    /// This ensures:
    /// 1. AI commands go through the same path as player commands
    /// 2. Commands are properly synchronized in multiplayer
    /// 3. All game logic (clearing conflicting commands, etc.) is applied
    /// 
    /// IMPORTANT FOR MULTIPLAYER:
    /// In multiplayer, AI only runs on the HOST. The host's AI decisions
    /// are then synchronized to clients via lockstep. This prevents
    /// AI decisions from diverging between clients.
    /// </summary>
    public static class AICommandAdapter
    {
        /// <summary>
        /// Check if AI should issue commands (only on host in multiplayer)
        /// </summary>
        public static bool ShouldAIIssueCommands()
        {
            // In single-player, always allow AI
            if (!GameSettings.IsMultiplayer)
                return true;
            
            // In multiplayer, only the host runs AI
            if (LockstepManager.Instance != null)
                return LockstepManager.Instance.IsHost;
            
            // Fallback: don't issue commands if we can't determine
            return false;
        }
        
        // ==================== Movement ====================
        
        /// <summary>
        /// Issue a move command from AI. Use this instead of setting DesiredDestination directly.
        /// </summary>
        public static void IssueMove(EntityManager em, Entity unit, float3 destination)
        {
            if (!ShouldAIIssueCommands()) return;
            if (unit == Entity.Null || !em.Exists(unit)) return;
            
            CommandRouter.IssueMove(em, unit, destination, CommandRouter.CommandSource.AI);
        }
        
        /// <summary>
        /// Issue move to multiple units (formation-style)
        /// </summary>
        public static void IssueMoveFormation(EntityManager em, Unity.Collections.NativeArray<Entity> units, 
            float3 destination, float spacing = 2.5f)
        {
            if (!ShouldAIIssueCommands()) return;
            
            int count = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != Entity.Null && em.Exists(units[i]))
                    count++;
            }
            
            if (count == 0) return;
            
            int cols = (int)math.ceil(math.sqrt(count));
            int row = 0, col = 0;
            
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] == Entity.Null || !em.Exists(units[i])) continue;
                
                float3 offset = new float3(
                    (col - cols / 2f) * spacing,
                    0,
                    (row - cols / 2f) * spacing
                );
                
                float3 targetPos = destination + offset;
                CommandRouter.IssueMove(em, units[i], targetPos, CommandRouter.CommandSource.AI);
                
                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }
        }
        
        // ==================== Combat ====================
        
        /// <summary>
        /// Issue an attack command from AI. Use this instead of setting Target directly.
        /// </summary>
        public static void IssueAttack(EntityManager em, Entity unit, Entity target)
        {
            if (!ShouldAIIssueCommands()) return;
            if (unit == Entity.Null || target == Entity.Null) return;
            if (!em.Exists(unit) || !em.Exists(target)) return;
            
            CommandRouter.IssueAttack(em, unit, target, CommandRouter.CommandSource.AI);
        }
        
        /// <summary>
        /// Issue a stop command from AI.
        /// </summary>
        public static void IssueStop(EntityManager em, Entity unit)
        {
            if (!ShouldAIIssueCommands()) return;
            if (unit == Entity.Null || !em.Exists(unit)) return;
            
            CommandRouter.IssueStop(em, unit, CommandRouter.CommandSource.AI);
        }
        
        // ==================== Economy ====================
        
        /// <summary>
        /// Issue a gather command from AI.
        /// </summary>
        public static void IssueGather(EntityManager em, Entity miner, Entity resourceNode, Entity depositLocation)
        {
            if (!ShouldAIIssueCommands()) return;
            if (miner == Entity.Null || resourceNode == Entity.Null) return;
            if (!em.Exists(miner)) return;
            
            CommandRouter.IssueGather(em, miner, resourceNode, depositLocation, CommandRouter.CommandSource.AI);
        }
        
        /// <summary>
        /// Issue a build command from AI.
        /// </summary>
        public static void IssueBuild(EntityManager em, Entity builder, Entity targetBuilding, 
            string buildingId, float3 position)
        {
            if (!ShouldAIIssueCommands()) return;
            if (builder == Entity.Null || !em.Exists(builder)) return;
            
            CommandRouter.IssueBuild(em, builder, targetBuilding, buildingId, position, CommandRouter.CommandSource.AI);
        }
        
        // ==================== Support ====================
        
        /// <summary>
        /// Issue a heal command from AI.
        /// </summary>
        public static void IssueHeal(EntityManager em, Entity healer, Entity target)
        {
            if (!ShouldAIIssueCommands()) return;
            if (healer == Entity.Null || target == Entity.Null) return;
            if (!em.Exists(healer) || !em.Exists(target)) return;
            
            CommandRouter.IssueHeal(em, healer, target, CommandRouter.CommandSource.AI);
        }
        
        /// <summary>
        /// Set rally point for a building from AI.
        /// </summary>
        public static void SetRallyPoint(EntityManager em, Entity building, float3 position)
        {
            if (!ShouldAIIssueCommands()) return;
            if (building == Entity.Null || !em.Exists(building)) return;
            
            CommandRouter.SetRallyPoint(em, building, position, CommandRouter.CommandSource.AI);
        }
        
        // ==================== Batch Operations ====================
        
        /// <summary>
        /// Issue attack commands to all units in an army buffer against prioritized targets.
        /// </summary>
        public static void EngageTargets(EntityManager em, DynamicBuffer<ArmyUnit> armyUnits, 
            Unity.Collections.NativeList<(Entity Entity, float3 Position, int Priority)> targets)
        {
            if (!ShouldAIIssueCommands()) return;
            if (targets.Length == 0) return;
            
            int targetIdx = 0;
            for (int i = 0; i < armyUnits.Length; i++)
            {
                var unit = armyUnits[i].Unit;
                if (unit == Entity.Null || !em.Exists(unit)) continue;
                
                if (targetIdx < targets.Length)
                {
                    var target = targets[targetIdx];
                    if (target.Entity != Entity.Null && em.Exists(target.Entity))
                    {
                        CommandRouter.IssueAttack(em, unit, target.Entity, CommandRouter.CommandSource.AI);
                    }
                    
                    // Cycle through targets
                    targetIdx = (targetIdx + 1) % targets.Length;
                }
            }
        }
        
        /// <summary>
        /// Move all units in an army buffer to positions in formation around a destination.
        /// </summary>
        public static void MoveArmy(EntityManager em, DynamicBuffer<ArmyUnit> armyUnits, 
            float3 destination, float spacing = 2.5f)
        {
            if (!ShouldAIIssueCommands()) return;
            
            int validUnits = 0;
            for (int i = 0; i < armyUnits.Length; i++)
            {
                if (armyUnits[i].Unit != Entity.Null && em.Exists(armyUnits[i].Unit))
                    validUnits++;
            }
            
            if (validUnits == 0) return;
            
            int cols = (int)math.ceil(math.sqrt(validUnits));
            int row = 0, col = 0;
            
            for (int i = 0; i < armyUnits.Length; i++)
            {
                var unit = armyUnits[i].Unit;
                if (unit == Entity.Null || !em.Exists(unit)) continue;
                
                // Skip scouts (ArmyId == -1)
                if (em.HasComponent<ArmyTag>(unit))
                {
                    var tag = em.GetComponentData<ArmyTag>(unit);
                    if (tag.ArmyId == -1)
                        continue;
                }
                
                float3 offset = new float3(
                    (col - cols / 2f) * spacing,
                    0,
                    (row - cols / 2f) * spacing
                );
                
                float3 targetPos = destination + offset;
                CommandRouter.IssueMove(em, unit, targetPos, CommandRouter.CommandSource.AI);
                
                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }
        }
        
        /// <summary>
        /// Hold formation at a position (only move units that are out of position).
        /// </summary>
        public static void HoldFormation(EntityManager em, DynamicBuffer<ArmyUnit> armyUnits, 
            float3 position, float spacing = 2.5f, float threshold = 1.25f)
        {
            if (!ShouldAIIssueCommands()) return;
            
            int validUnits = 0;
            for (int i = 0; i < armyUnits.Length; i++)
            {
                if (armyUnits[i].Unit != Entity.Null && em.Exists(armyUnits[i].Unit))
                    validUnits++;
            }
            
            if (validUnits == 0) return;
            
            int cols = (int)math.ceil(math.sqrt(validUnits));
            int row = 0, col = 0;
            
            for (int i = 0; i < armyUnits.Length; i++)
            {
                var unit = armyUnits[i].Unit;
                if (unit == Entity.Null || !em.Exists(unit)) continue;
                
                // Skip scouts
                if (em.HasComponent<ArmyTag>(unit))
                {
                    var tag = em.GetComponentData<ArmyTag>(unit);
                    if (tag.ArmyId == -1)
                        continue;
                }
                
                float3 offset = new float3(
                    (col - cols / 2f) * spacing,
                    0,
                    (row - cols / 2f) * spacing
                );
                
                float3 holdPos = position + offset;
                
                // Only move if significantly out of position
                if (em.HasComponent<LocalTransform>(unit))
                {
                    var transform = em.GetComponentData<LocalTransform>(unit);
                    float dist = math.distance(transform.Position, holdPos);
                    
                    if (dist > threshold)
                    {
                        CommandRouter.IssueMove(em, unit, holdPos, CommandRouter.CommandSource.AI);
                    }
                }
                
                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }
        }
    }
}