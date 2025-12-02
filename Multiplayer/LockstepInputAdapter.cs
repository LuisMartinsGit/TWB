// Assets/Scripts/Multiplayer/LockstepInputAdapter.cs
// DEPRECATED: This class is kept for backward compatibility.
// NEW CODE SHOULD USE: CommandRouter (for player commands) or AICommandAdapter (for AI)
//
// This adapter now simply delegates to CommandRouter.
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// DEPRECATED: Use CommandRouter directly instead.
    /// 
    /// This adapter is kept for backward compatibility with existing code.
    /// All methods now delegate to CommandRouter with CommandSource.LocalPlayer.
    /// 
    /// For new code:
    /// - Player commands: Use CommandRouter.IssueMove(), CommandRouter.IssueAttack(), etc.
    /// - AI commands: Use AICommandAdapter.IssueMove(), AICommandAdapter.IssueAttack(), etc.
    /// </summary>
    public static class LockstepInputAdapter
    {
        /// <summary>
        /// Issue a move command (delegates to CommandRouter)
        /// </summary>
        public static void IssueMove(EntityManager em, Entity unit, float3 destination)
        {
            CommandRouter.IssueMove(em, unit, destination, CommandRouter.CommandSource.LocalPlayer);
        }

        /// <summary>
        /// Issue an attack command (delegates to CommandRouter)
        /// </summary>
        public static void IssueAttack(EntityManager em, Entity unit, Entity target)
        {
            CommandRouter.IssueAttack(em, unit, target, CommandRouter.CommandSource.LocalPlayer);
        }

        /// <summary>
        /// Issue a stop command (delegates to CommandRouter)
        /// </summary>
        public static void IssueStop(EntityManager em, Entity unit)
        {
            CommandRouter.IssueStop(em, unit, CommandRouter.CommandSource.LocalPlayer);
        }

        /// <summary>
        /// Issue a gather command (delegates to CommandRouter)
        /// </summary>
        public static void IssueGather(EntityManager em, Entity miner, Entity resourceNode, Entity depositLocation)
        {
            CommandRouter.IssueGather(em, miner, resourceNode, depositLocation, CommandRouter.CommandSource.LocalPlayer);
        }

        /// <summary>
        /// Issue a build command (delegates to CommandRouter)
        /// </summary>
        public static void IssueBuild(EntityManager em, Entity builder, Entity targetBuilding, string buildingId, float3 position)
        {
            CommandRouter.IssueBuild(em, builder, targetBuilding, buildingId, position, CommandRouter.CommandSource.LocalPlayer);
        }

        /// <summary>
        /// Issue a heal command (delegates to CommandRouter)
        /// </summary>
        public static void IssueHeal(EntityManager em, Entity healer, Entity target)
        {
            CommandRouter.IssueHeal(em, healer, target, CommandRouter.CommandSource.LocalPlayer);
        }

        /// <summary>
        /// Set rally point for a building (delegates to CommandRouter)
        /// </summary>
        public static void SetRallyPoint(EntityManager em, Entity building, float3 position)
        {
            CommandRouter.SetRallyPoint(em, building, position, CommandRouter.CommandSource.LocalPlayer);
        }
    }
}