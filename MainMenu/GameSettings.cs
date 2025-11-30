// Assets/Scripts/GameSettings.cs
using UnityEngine;
using System.Collections.Generic;

public enum GameMode { FreeForAll, SoloVsCurse }

public enum SpawnLayout
{
    Circle,          // evenly spaced around a ring
    TwoSides,        // 4 players split across two sides (e.g., LR, UD, LU, LD, RU, RD)
    TwoEachSide8     // 2 players per side (up to 8 total)
}
public enum TwoSidesPreset
{
    LeftRight,   // LR
    UpDown,      // UD
    LeftUp,      // LU (adjacent)
    LeftDown,    // LD
    RightUp,     // RU
    RightDown    // RD
}

public enum NetworkRole
{
    None,       // Single-player mode
    Server,     // Hosting multiplayer game
    Client      // Joined multiplayer game
}

public static class GameSettings
{
    public static int TotalPlayers = 2;
    public static int SpawnEdgeBufferMin = 30;
    public static int SpawnEdgeBufferMax = 60;
    public static int SpawnMinSeparation = 100; 
    public static GameMode Mode = GameMode.FreeForAll;

    public static bool FogOfWarEnabled = true;

    public static int MapHalfSize = 125;

    // Spawn layout settings
    public static SpawnLayout SpawnLayout = SpawnLayout.Circle;
    public static TwoSidesPreset TwoSides = TwoSidesPreset.LeftRight;

    // Optional: make randomness reproducible per match
    public static int SpawnSeed = 1234567;

    // ==================== Multiplayer Settings ====================
    
    /// <summary>
    /// Whether the current game is a multiplayer session
    /// </summary>
    public static bool IsMultiplayer = false;

    /// <summary>
    /// The network role of this instance (None for single-player)
    /// </summary>
    public static NetworkRole NetworkRole = NetworkRole.None;

    /// <summary>
    /// Faction controlled by the local player in multiplayer
    /// </summary>
    public static Faction LocalPlayerFaction = Faction.Blue;

    /// <summary>
    /// Mapping of factions to player client IDs in multiplayer
    /// Key: Faction, Value: NetworkManager client ID (ulong)
    /// Factions not in this dictionary are AI-controlled
    /// </summary>
    public static Dictionary<Faction, ulong> FactionToPlayerMapping = new Dictionary<Faction, ulong>();

    /// <summary>
    /// Reset all settings to single-player defaults
    /// </summary>
    public static void ResetToSinglePlayer()
    {
        IsMultiplayer = false;
        NetworkRole = NetworkRole.None;
        LocalPlayerFaction = Faction.Blue;
        FactionToPlayerMapping.Clear();
    }

    /// <summary>
    /// Check if a faction is controlled by a human player (vs AI)
    /// </summary>
    public static bool IsFactionHumanControlled(Faction faction)
    {
        if (!IsMultiplayer) return faction == Faction.Blue; // Single-player: only Blue is human
        return FactionToPlayerMapping.ContainsKey(faction);
    }

    /// <summary>
    /// Check if a faction is controlled by the local player
    /// </summary>
    public static bool IsFactionLocallyControlled(Faction faction)
    {
        if (!IsMultiplayer) return faction == Faction.Blue;
        return faction == LocalPlayerFaction;
    }
}
