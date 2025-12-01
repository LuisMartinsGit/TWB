// Assets/Scripts/Multiplayer/LockstepTypes.cs
// Shared types for lockstep multiplayer system
using System;
using System.Net;
using Unity.Mathematics;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Information about a remote player for lockstep connections
    /// </summary>
    public class RemotePlayerInfo
    {
        public string IP;
        public int Port;
        public Faction Faction;
    }

    /// <summary>
    /// Runtime data for a connected remote player
    /// </summary>
    public class RemotePlayer
    {
        public int PlayerIndex;
        public Faction Faction;
        public IPEndPoint EndPoint;
        public int LastConfirmedTick;
        public int Latency; // ms
    }

    /// <summary>
    /// Client info passed from lobby to lockstep bootstrap
    /// </summary>
    public class ClientInfo
    {
        public string PlayerName;
        public int SlotIndex;
        public string IP;
        public int Port;
        public Faction Faction;
        public DateTime LastSeen;
    }

    /// <summary>
    /// Types of commands that can be sent through lockstep
    /// </summary>
    public enum LockstepCommandType : byte
    {
        None = 0,
        Move = 1,
        Attack = 2,
        Stop = 3,
        Build = 4,
        Train = 5,
        Gather = 6,
        SetRally = 7,
        Heal = 8
    }

    /// <summary>
    /// A command to be executed at a specific tick
    /// </summary>
    [Serializable]
    public class LockstepCommand
    {
        public LockstepCommandType Type;
        public int PlayerIndex;
        public int Tick;
        public int CommandIndex;
        public int EntityNetworkId;
        public float3 TargetPosition;
        public int TargetEntityId;
        public int SecondaryTargetId;
        public string BuildingId;

        public string Serialize()
        {
            // Format: Type,EntityId,PosX,PosY,PosZ,TargetId,SecondaryId,BuildingId
            return $"{(int)Type},{EntityNetworkId},{TargetPosition.x:F2},{TargetPosition.y:F2},{TargetPosition.z:F2},{TargetEntityId},{SecondaryTargetId},{BuildingId ?? ""}";
        }

        public static LockstepCommand Deserialize(string data)
        {
            try
            {
                string[] parts = data.Split(',');
                if (parts.Length < 7) return null;

                return new LockstepCommand
                {
                    Type = (LockstepCommandType)int.Parse(parts[0]),
                    EntityNetworkId = int.Parse(parts[1]),
                    TargetPosition = new float3(
                        float.Parse(parts[2]),
                        float.Parse(parts[3]),
                        float.Parse(parts[4])),
                    TargetEntityId = int.Parse(parts[5]),
                    SecondaryTargetId = int.Parse(parts[6]),
                    BuildingId = parts.Length > 7 ? parts[7] : ""
                };
            }
            catch
            {
                return null;
            }
        }
    }
}