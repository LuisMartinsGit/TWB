using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Unique network identifier for each networked entity.
    /// This is used to reference entities across the network.
    /// </summary>
    public struct NetworkId : IComponentData
    {
        [GhostField] public int Value;
    }

    /// <summary>
    /// Player connection information.
    /// Exists on the connection entity created when a player joins.
    /// </summary>
    public struct PlayerConnection : IComponentData
    {
        public int PlayerId;
        public Faction AssignedFaction;
        public FixedString64Bytes PlayerName;
    }

    /// <summary>
    /// Game phase synchronization across all clients.
    /// Server sets this, clients receive it.
    /// </summary>
    public struct NetworkGamePhase : IComponentData
    {
        [GhostField] public GamePhase Value;
    }

    /// <summary>
    /// Game phase states
    /// </summary>
    public enum GamePhase : byte
    {
        Lobby = 0,
        Loading = 1,
        Playing = 2,
        Paused = 3,
        Ended = 4
    }

    /// <summary>
    /// Marks an entity as a ghost that should be replicated over the network.
    /// Add this to units and buildings that need to be synchronized.
    /// </summary>
    public struct NetworkedEntity : IComponentData
    {
        [GhostField] public int NetworkId;
    }

    /// <summary>
    /// Player ready state in lobby.
    /// </summary>
    public struct PlayerReady : IComponentData
    {
        [GhostField] public byte IsReady; // 1 = ready, 0 = not ready
    }

    /// <summary>
    /// Tag component to mark the local player's connection entity.
    /// </summary>
    public struct LocalPlayerTag : IComponentData
    {
    }

    /// <summary>
    /// Component to track the next available network ID (server only).
    /// Singleton component.
    /// </summary>
    public struct NetworkIdAllocator : IComponentData
    {
        public int NextId;
    }

    /// <summary>
    /// Singleton component to track network game state (server only).
    /// </summary>
    public struct NetworkGameState : IComponentData
    {
        public GamePhase CurrentPhase;
        public int TotalPlayers;
        public int ReadyPlayers;
    }
}
