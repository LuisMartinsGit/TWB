// ICommand.cs
// Base interface and shared types for the command system
// Location: Assets/Scripts/Core/Commands/ICommand.cs

using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.Core.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // COMMAND SOURCE IDENTIFICATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Identifies where a command originated from.
    /// Used for determining lockstep routing behavior.
    /// </summary>
    public enum CommandSource
    {
        /// <summary>Local human player (RTSInput, UI clicks)</summary>
        LocalPlayer,
        
        /// <summary>Remote human player (received via network)</summary>
        RemotePlayer,
        
        /// <summary>AI system (AITacticalManager, etc.)</summary>
        AI,
        
        /// <summary>Internal system (auto-targeting, spawning, etc.)</summary>
        System
    }

    // ═══════════════════════════════════════════════════════════════
    // COMMAND INTERFACE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Interface for command pattern implementation.
    /// Commands encapsulate all information needed to execute an action.
    /// </summary>
    public interface IGameCommand
    {
        /// <summary>The entity this command targets</summary>
        Entity TargetEntity { get; }
        
        /// <summary>Where the command originated from</summary>
        CommandSource Source { get; }
        
        /// <summary>Execute the command immediately</summary>
        void Execute(EntityManager em);
        
        /// <summary>Check if the command can be executed</summary>
        bool CanExecute(EntityManager em);
    }

    /// <summary>
    /// Interface for commands that can be undone (future feature).
    /// </summary>
    public interface IUndoableCommand : IGameCommand
    {
        /// <summary>Undo the command's effects</summary>
        void Undo(EntityManager em);
    }

    // ═══════════════════════════════════════════════════════════════
    // COMMAND RESULT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Result of a command execution attempt
    /// </summary>
    public enum CommandResult
    {
        Success,
        EntityNotFound,
        TargetNotFound,
        InvalidState,
        InsufficientResources,
        NoPermission,
        QueuedForLockstep
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER MARKER COMPONENTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks that a unit has been explicitly ordered by the user to move.
    /// Used to distinguish user commands from AI auto-move behaviors.
    /// </summary>
    public struct UserMoveOrder : IComponentData { }

    /// <summary>
    /// Marks a position as a guard point - where unit returns after combat.
    /// </summary>
    public struct GuardPoint : IComponentData
    {
        public float3 Position;
        public byte Has; // 0/1 - whether guard point is set
    }

    /// <summary>
    /// Rally point for buildings - where trained units go after spawning.
    /// </summary>
    public struct RallyPoint : IComponentData
    {
        public float3 Position;
        public byte Has; // 0/1 - whether rally point is set
    }

    /// <summary>
    /// Marks a unit that can build structures.
    /// </summary>
    public struct CanBuild : IComponentData { }

    /// <summary>
    /// Marks a unit that can heal other units.
    /// </summary>
    public struct CanHeal : IComponentData 
    {
        public float HealRate;     // HP per second
        public float HealRange;    // Max distance to target
    }

    // ═══════════════════════════════════════════════════════════════
    // MOVEMENT COMPONENTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Desired destination for pathfinding/movement systems.
    /// </summary>
    public struct DesiredDestination : IComponentData
    {
        public float3 Position;
        public byte Has; // 0/1 - whether destination is set
    }
}