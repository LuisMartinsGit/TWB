using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Input component for sending player commands to the server.
    /// This implements IInputComponentData which is automatically sent from client to server.
    /// </summary>
    public struct NetworkCommandInput : IInputComponentData
    {
        public CommandType Type;
        public int TargetEntityNetworkId;
        public int SecondaryTargetNetworkId; // For attack/heal target, resource node, etc.
        public float3 Destination;
        public FixedString64Bytes BuildingId;
    }

    /// <summary>
    /// Command types that can be issued over the network
    /// </summary>
    public enum CommandType : byte
    {
        None = 0,
        Move = 1,
        Attack = 2,
        Stop = 3,
        Build = 4,
        Gather = 5,
        Heal = 6
    }

    /// <summary>
    /// Helper struct for batching multiple commands together (optional optimization).
    /// </summary>
    public struct NetworkCommandBatch : IComponentData
    {
        public int CommandCount;
        // Commands would be in a buffer element
    }

    /// <summary>
    /// Buffer element for command batching (optional optimization).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct NetworkCommandBufferElement : IBufferElementData
    {
        public NetworkCommandInput Command;
    }
}
