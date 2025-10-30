// Assets/Scripts/GameSettings.cs
using UnityEngine;

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
public static class GameSettings
{
    public static int TotalPlayers = 2;
    public static int SpawnEdgeBufferMin = 30;
    public static int SpawnEdgeBufferMax = 60;
    public static int SpawnMinSeparation = 100; 
    public static GameMode Mode = GameMode.FreeForAll;

    public static bool FogOfWarEnabled = true;

    public static int MapHalfSize = 125;

    // NEW:
    public static SpawnLayout SpawnLayout = SpawnLayout.Circle;
    public static TwoSidesPreset TwoSides = TwoSidesPreset.LeftRight;

    // Optional: make randomness reproducible per match
    public static int SpawnSeed = 1234567;
}
