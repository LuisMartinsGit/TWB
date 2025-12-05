// LobbyTypes.cs
// Shared lobby configuration types used by both Core and Multiplayer
// Location: Assets/Scripts/Core/Config/LobbyTypes.cs

using UnityEngine;

namespace TheWaningBorder.Core.Config
{
    /// <summary>
    /// AI difficulty levels for lobby configuration.
    /// </summary>
    public enum LobbyAIDifficulty
    {
        Easy,
        Normal,
        Hard,
        Expert
    }

    /// <summary>
    /// Type of player in a lobby slot.
    /// </summary>
    public enum SlotType
    {
        Empty = 0,
        Human = 1,
        AI = 2
    }

    /// <summary>
    /// Alias for SlotType - used by some Multiplayer code.
    /// Values match SlotType for easy conversion.
    /// </summary>
    public enum LobbySlotType
    {
        Empty = 0,
        Human = 1,
        AI = 2
    }

    /// <summary>
    /// A player slot in the lobby.
    /// </summary>
    public class PlayerSlot
    {
        public int SlotIndex;
        public SlotType Type;
        public Faction Faction;
        public LobbyAIDifficulty AIDifficulty;
        public string PlayerName;

        public PlayerSlot(int index, Faction faction)
        {
            SlotIndex = index;
            Faction = faction;
            Type = SlotType.Empty;
            AIDifficulty = LobbyAIDifficulty.Normal;
            PlayerName = "";
        }

        public Color GetFactionColor()
        {
            return Faction switch
            {
                Faction.Blue => new Color(0.20f, 0.55f, 1.00f),
                Faction.Red => new Color(1.00f, 0.20f, 0.25f),
                Faction.Green => new Color(0.20f, 0.90f, 0.35f),
                Faction.Yellow => new Color(1.00f, 0.85f, 0.20f),
                Faction.Purple => new Color(0.80f, 0.40f, 1.00f),
                Faction.Orange => new Color(1.00f, 0.55f, 0.15f),
                Faction.Teal => new Color(0.20f, 1.00f, 0.95f),
                Faction.White => Color.white,
                _ => Color.gray
            };
        }
    }

    /// <summary>
    /// Network-specific lobby slot with client tracking.
    /// Used by Multiplayer lobby for networked games.
    /// </summary>
    public class LobbySlot
    {
        public LobbySlotType Type = LobbySlotType.Empty;
        public string PlayerName = "";
        public LobbyAIDifficulty AIDifficulty = LobbyAIDifficulty.Normal;
        public string ClientKey = "";
    }

    /// <summary>
    /// Static holder for lobby configuration.
    /// Shared between Core and Multiplayer assemblies.
    /// </summary>
    public static class LobbyConfig
    {
        public static PlayerSlot[] Slots = new PlayerSlot[8];
        public static int ActiveSlotCount = 2;

        static LobbyConfig()
        {
            InitializeSlots();
        }

        public static void InitializeSlots()
        {
            Faction[] factions = {
                Faction.Blue, Faction.Red, Faction.Green, Faction.Yellow,
                Faction.Purple, Faction.Orange, Faction.Teal, Faction.White
            };

            for (int i = 0; i < 8; i++)
            {
                Slots[i] = new PlayerSlot(i, factions[i]);
            }
        }

        public static void SetupSinglePlayer(int playerCount)
        {
            ActiveSlotCount = Mathf.Clamp(playerCount, 2, 8);
            
            for (int i = 0; i < 8; i++)
            {
                if (i == 0)
                {
                    Slots[i].Type = SlotType.Human;
                    Slots[i].PlayerName = "Player";
                }
                else if (i < ActiveSlotCount)
                {
                    Slots[i].Type = SlotType.AI;
                    Slots[i].AIDifficulty = LobbyAIDifficulty.Normal;
                }
                else
                {
                    Slots[i].Type = SlotType.Empty;
                }
            }
        }

        public static void SetupMultiplayer(int playerCount)
        {
            ActiveSlotCount = Mathf.Clamp(playerCount, 2, 8);
            
            for (int i = 0; i < 8; i++)
            {
                if (i < ActiveSlotCount)
                {
                    Slots[i].Type = SlotType.AI;
                    Slots[i].AIDifficulty = LobbyAIDifficulty.Normal;
                }
                else
                {
                    Slots[i].Type = SlotType.Empty;
                }
            }
        }
    }
}