using UnityEngine;

namespace TheWaningBorder.Core.Settings
{
    public enum SpawnLayout
    {
        Random,
        Circle,
        Grid,
        TwoSides
    }

    public enum TwoSidesPreset
    {
        NorthVsSouth,
        EastVsWest,
        DiagonalNWSE,
        DiagonalNESW
    }

    public static class GameSettings
    {
        // Game Mode
        public static GameManager.GameMode Mode = GameManager.GameMode.FreeForAll;
        public static int TotalPlayers = 4;
        
        // Map Settings
        public static int MapHalfSize = 128;
        public static bool FogOfWarEnabled = true;
        
        // Spawn Settings
        public static SpawnLayout SpawnLayout = SpawnLayout.Circle;
        public static TwoSidesPreset TwoSides = TwoSidesPreset.NorthVsSouth;
        public static int SpawnSeed = 12345;
        
        // Resource Settings
        public static int StartingSupplies = 400;
        public static int StartingIron = 100;
        public static int StartingCrystal = 50;
        
        // Iron Patch Settings
        public static int GuaranteedPatchesPerPlayer = 1;
        public static int AdditionalRandomPatches = 8;
        public static int DepositsPerPatch = 4;
        public static int OrePerDeposit = 500;
        public static float PatchRadius = 8f;
        public static float MinPatchDistance = 20f;
        
        // Fog of War Settings
        public static int FogGridSize = 256;
        public static float FogCellSize = 2f;
        
        // Unit Settings
        public static float DefaultUnitSpeed = 5f;
        public static float DefaultAttackRange = 10f;
        public static float DefaultLineOfSight = 15f;
    }
}
