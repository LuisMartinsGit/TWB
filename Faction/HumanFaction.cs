using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Humans;
using System.Collections.Generic;
// HumanFaction.cs
// Spawns human players: Hall (Era 1) + two melee units each, positioned in a ring.
namespace TheWaningBorder.Gameplay
{

/// ===== Era 1 — generic buildings =====
    public struct GathererHutTag : IComponentData { }   // E1_GatherersHut
    public struct BarracksTag    : IComponentData { }   // E1_Barracks
    public struct WorkshopTag    : IComponentData { }   // E1_Workshop
    public struct DepotTag       : IComponentData { }   // E1_Depot
    public struct TempleTag      : IComponentData { }   // E1_Temple
    public struct WallTag        : IComponentData { }   // E1_Wall

    /// ===== Era 2 — Runai =====
    public struct OutpostTag     : IComponentData { }   // E2_Runai_Outpost
    public struct TradeHubTag    : IComponentData { }   // E2_Runai_TradeHub
    public struct VaultTag       : IComponentData { }   // E2_Runai_Vault

    /// ===== Era 2 — Alanthor =====
    public struct SmelterTag     : IComponentData { }   // E2_Alanthor_Smelter
    public struct CrucibleTag    : IComponentData { }   // E2_Alanthor_Crucible

    /// ===== Era 2 — Feraldis =====
    public struct HuntingLodgeTag    : IComponentData { }   // E2_Feraldis_Hunting
    public struct LoggingStationTag  : IComponentData { }   // E2_Feraldis_Logging
    public struct WarbrandFoundryTag : IComponentData { }   // E2_Feraldis_Foundry

    /// ===== Sects (buildings & units) =====
    public struct ChapelSmallTag         : IComponentData { }
    public struct ChapelLargeTag         : IComponentData { }
    public struct SectUniqueBuildingTag  : IComponentData { }
    public struct SectUniqueUnitTag      : IComponentData { }
    public static class HumanFaction
    {
        
        /// <summary>
        /// Main entry: spawn <paramref name="playerCount"/> human players (Hall + 2 melee each).
        /// Uses the default world EntityManager and FoW bounds if available.
        /// </summary>
        public static void GeneratePlayers(int playerCount)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[HumanFaction] Default world is not available.");
                return;
            }
            GeneratePlayers(world.EntityManager, playerCount);
        }

        /// <summary>
        /// Same as above, but with an explicit EntityManager.
        /// </summary>
      public static void GeneratePlayers(EntityManager em, int playerCount)
        {
            if (playerCount <= 0) return;

            // Map bounds still useful as fallback
            float minX = -125f, maxX = 125f, minZ = -125f, maxZ = 125f;
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            if (fow != null)
            {
                minX = fow.WorldMin.x; maxX = fow.WorldMax.x;
                minZ = fow.WorldMin.y; maxZ = fow.WorldMax.y;
            }

            // NEW: ask the generator for positions based on menu settings (Circle / TwoSides / TwoEachSide8)
            var starts = SpawnPositioning.Generate(playerCount);
            if (starts == null || starts.Count == 0)
            {
                starts = FallbackCircle(playerCount, minX, maxX, minZ, maxZ);
            }

            Faction[] palette = new[]
            {
                Faction.Blue, Faction.Red, Faction.Green, Faction.Yellow,
                Faction.Purple, Faction.Orange, Faction.Teal, Faction.White
            };

            for (int i = 0; i < playerCount; i++)
            {
                var pos = (float3)starts[Mathf.Min(i, starts.Count - 1)];
                var fac = palette[i % palette.Length];

                SpawnPlayerStart(em, pos, fac);
            }

            Debug.Log($"[HumanFaction] Spawned {playerCount} human players with layout {GameSettings.SpawnLayout}.");
        }
        static List<Vector3> FallbackCircle(int n, float minX, float maxX, float minZ, float maxZ)
        {
            float cx = (minX + maxX) * 0.5f;
            float cz = (minZ + maxZ) * 0.5f;
            float halfW = (maxX - minX) * 0.5f;
            float halfH = (maxZ - minZ) * 0.5f;
            float r = Mathf.Min(halfW, halfH) * 0.65f;
            var list = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i * Mathf.PI * 2f / n;
                list.Add(new Vector3(cx + Mathf.Cos(t) * r, 0, cz + Mathf.Sin(t) * r));
            }
            return list;
        }


        // ---------------------- Spawning primitives ----------------------

        static void SpawnPlayerStart(EntityManager em, float3 spawnPos, Faction fac)
        {
            // 1) Hall (Era 1 capital)
            Hall.Create(em, spawnPos, fac);

            // 2) Two melee units near the Hall
            var right = new float3(1, 0, 0);
            var fwd = new float3(0, 0, 1);

            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Swordsman.Create(em, spawnPos + right * 3f + fwd * 2f, fac);
            Builder.Create(em, spawnPos - right * 3f + fwd * 2f, fac);

        }
    }
}
