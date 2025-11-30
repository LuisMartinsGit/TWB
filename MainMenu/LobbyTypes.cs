// Assets/Scripts/MainMenu/LobbyTypes.cs
using UnityEngine;
using System;

namespace TheWaningBorder.Menu
{
    /// <summary>
    /// Type of player occupying a slot in the lobby.
    /// </summary>
    public enum SlotType
    {
        Empty,      // No player (closed slot)
        Human,      // Human player (only one in single-player)
        AI          // AI-controlled player
    }

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
    /// Represents a single player slot in a game lobby.
    /// </summary>
    [Serializable]
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
                Faction.White => new Color(1.00f, 1.00f, 1.00f),
                _ => Color.gray
            };
        }

        public string GetFactionName()
        {
            return Faction switch
            {
                Faction.Blue => "Blue",
                Faction.Red => "Red",
                Faction.Green => "Green",
                Faction.Yellow => "Yellow",
                Faction.Purple => "Purple",
                Faction.Orange => "Orange",
                Faction.Teal => "Teal",
                Faction.White => "White",
                _ => "Unknown"
            };
        }

        public string GetDisplayName()
        {
            return Type switch
            {
                SlotType.Empty => "[Empty]",
                SlotType.Human => string.IsNullOrEmpty(PlayerName) ? "Player" : PlayerName,
                SlotType.AI => $"AI ({AIDifficulty})",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Static holder for lobby configuration that persists across menu screens.
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

        /// <summary>
        /// Setup for single-player: slot 0 is human, rest are AI based on count.
        /// </summary>
        public static void SetupSinglePlayer(int playerCount)
        {
            ActiveSlotCount = Mathf.Clamp(playerCount, 2, 8);
            
            for (int i = 0; i < 8; i++)
            {
                if (i == 0)
                {
                    // Slot 0 is always the human player
                    Slots[i].Type = SlotType.Human;
                    Slots[i].PlayerName = "Player";
                }
                else if (i < ActiveSlotCount)
                {
                    // Fill remaining active slots with AI
                    Slots[i].Type = SlotType.AI;
                    Slots[i].AIDifficulty = LobbyAIDifficulty.Normal;
                }
                else
                {
                    // Remaining slots are empty
                    Slots[i].Type = SlotType.Empty;
                }
            }
        }

        /// <summary>
        /// Setup for multiplayer: all slots start as AI, humans join later.
        /// </summary>
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

        /// <summary>
        /// Apply lobby configuration to GameSettings before starting the game.
        /// </summary>
        public static void ApplyToGameSettings()
        {
            GameSettings.TotalPlayers = ActiveSlotCount;
            GameSettings.FactionToPlayerMapping.Clear();

            for (int i = 0; i < ActiveSlotCount; i++)
            {
                var slot = Slots[i];
                if (slot.Type == SlotType.Human)
                {
                    // Map human-controlled factions
                    GameSettings.FactionToPlayerMapping[slot.Faction] = (ulong)i;
                    
                    // In single-player, set local player faction
                    if (!GameSettings.IsMultiplayer)
                    {
                        GameSettings.LocalPlayerFaction = slot.Faction;
                    }
                }
            }
        }

        /// <summary>
        /// Count how many human players are configured.
        /// </summary>
        public static int CountHumanPlayers()
        {
            int count = 0;
            for (int i = 0; i < ActiveSlotCount; i++)
            {
                if (Slots[i].Type == SlotType.Human) count++;
            }
            return count;
        }

        /// <summary>
        /// Count how many AI players are configured.
        /// </summary>
        public static int CountAIPlayers()
        {
            int count = 0;
            for (int i = 0; i < ActiveSlotCount; i++)
            {
                if (Slots[i].Type == SlotType.AI) count++;
            }
            return count;
        }
    }
}
