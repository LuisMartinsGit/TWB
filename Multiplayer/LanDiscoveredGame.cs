using System;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Represents a game discovered via LAN broadcast.
    /// </summary>
    [Serializable]
    public class LanDiscoveredGame
    {
        public string IPAddress { get; set; }
        public LanGameInfo GameInfo { get; set; }
        public DateTime LastSeen { get; set; }

        public LanDiscoveredGame(string ipAddress, LanGameInfo gameInfo)
        {
            IPAddress = ipAddress;
            GameInfo = gameInfo;
            LastSeen = DateTime.Now;
        }
    }

    /// <summary>
    /// Information about a hosted game, sent in broadcast packets.
    /// </summary>
    [Serializable]
    public class LanGameInfo
    {
        public string GameName { get; set; }
        public string HostName { get; set; }
        public ushort GamePort { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }

        public byte[] Serialize()
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(GameName ?? "");
                writer.Write(HostName ?? "");
                writer.Write(GamePort);
                writer.Write(CurrentPlayers);
                writer.Write(MaxPlayers);
                return stream.ToArray();
            }
        }

        public static LanGameInfo Deserialize(byte[] data)
        {
            using (var stream = new System.IO.MemoryStream(data))
            using (var reader = new System.IO.BinaryReader(stream))
            {
                return new LanGameInfo
                {
                    GameName = reader.ReadString(),
                    HostName = reader.ReadString(),
                    GamePort = reader.ReadUInt16(),
                    CurrentPlayers = reader.ReadInt32(),
                    MaxPlayers = reader.ReadInt32()
                };
            }
        }
    }
}