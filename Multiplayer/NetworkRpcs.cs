using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.Mathematics;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// RPC to request joining a game.
    /// Sent from client to server.
    /// </summary>
    public struct JoinGameRpc : IRpcCommand
    {
        public FixedString64Bytes PlayerName;
    }

    /// <summary>
    /// RPC to assign a faction to a player.
    /// Sent from server to client.
    /// </summary>
    public struct AssignFactionRpc : IRpcCommand
    {
        public int PlayerId;
        public Faction Faction;
    }

    /// <summary>
    /// RPC to signal player ready state change.
    /// Sent from client to server.
    /// </summary>
    public struct PlayerReadyRpc : IRpcCommand
    {
        public byte IsReady; // 1 = ready, 0 = not ready
    }

    /// <summary>
    /// RPC to start the game.
    /// Sent from server to all clients.
    /// </summary>
    public struct StartGameRpc : IRpcCommand
    {
        public int Seed; // Random seed for deterministic spawning
    }

    /// <summary>
    /// RPC to notify game end.
    /// Sent from server to all clients.
    /// </summary>
    public struct GameEndRpc : IRpcCommand
    {
        public Faction WinningFaction;
    }
}
