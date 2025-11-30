using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Humans;
using System.Collections.Generic;
using Unity.Transforms;

// HumanFaction.cs - UPDATED WITH TECHTREE STATS
// Spawns human players with Hall + properly statted army for each player.
// Uses TechTreeDB for all unit stats - no hardcoded values!

namespace TheWaningBorder.Gameplay
{
    /// ===== Era 1 — generic buildings =====
    public struct GathererHutTag : IComponentData { }
    public struct BarracksTag    : IComponentData { }
    public struct WorkshopTag    : IComponentData { }
    public struct DepotTag       : IComponentData { }
    public struct TempleTag      : IComponentData { }
    public struct WallTag        : IComponentData { }

    /// ===== Era 2 — Runai =====
    public struct OutpostTag     : IComponentData { }
    public struct TradeHubTag    : IComponentData { }
    public struct VaultTag       : IComponentData { }

    /// ===== Era 2 — Alanthor =====
    public struct SmelterTag     : IComponentData { }
    public struct CrucibleTag    : IComponentData { }

    /// ===== Era 2 — Feraldis =====
    public struct HuntingLodgeTag    : IComponentData { }
    public struct LoggingStationTag  : IComponentData { }
    public struct WarbrandFoundryTag : IComponentData { }

    /// ===== Sects =====
    public struct ChapelSmallTag         : IComponentData { }
    public struct ChapelLargeTag         : IComponentData { }
    public struct SectUniqueBuildingTag  : IComponentData { }
    public struct SectUniqueUnitTag      : IComponentData { }

    public static class HumanFaction
    {
        /// <summary>
        /// Main entry: spawn players with Hall + army for each.
        /// CRITICAL: TechTreeDB.Load() must be called BEFORE this!
        /// </summary>
        public static void GeneratePlayers(int playerCount)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {

                return;
            }
            GeneratePlayers(world.EntityManager, playerCount);
        }

        /// <summary>
        /// Same as above, but with an explicit EntityManager.
        /// Spawns each player with Hall + army using TechTreeDB stats.
        /// </summary>
        public static void GeneratePlayers(EntityManager em, int playerCount)
        {
            if (playerCount <= 0) return;

            // CRITICAL: Verify TechTreeDB is loaded
            if (TechTreeDB.Instance == null)
            {

                return;
            }

            // Get map bounds
            float minX = -125f, maxX = 125f, minZ = -125f, maxZ = 125f;
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            if (fow != null)
            {
                minX = fow.WorldMin.x; maxX = fow.WorldMax.x;
                minZ = fow.WorldMin.y; maxZ = fow.WorldMax.y;
            }

            // Get spawn positions based on menu settings
            var starts = SpawnPositioning.Generate(playerCount);
            if (starts == null || starts.Count == 0)
            {

                starts = FallbackCircle(playerCount, minX, maxX, minZ, maxZ);
            }

            // Faction palette
            Faction[] palette = new[]
            {
                Faction.Blue, Faction.Red, Faction.Green, Faction.Yellow,
                Faction.Purple, Faction.Orange, Faction.Teal, Faction.White
            };

            // Spawn each player
            for (int i = 0; i < playerCount; i++)
            {
                var pos = (float3)starts[Mathf.Min(i, starts.Count - 1)];
                var fac = palette[i % palette.Length];

                SpawnPlayerStart(em, pos, fac);
            }

        }

        // Fallback circle positioning if SpawnPositioning fails
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

        /// <summary>
        /// Spawn one player's starting setup: Hall + army with proper stats.
        /// Army composition: 200 archers, 1 builder.
        /// All units get stats from TechTreeDB.
        /// </summary>
        static void SpawnPlayerStart(EntityManager em, float3 spawnPos, Faction fac)
        {
            Debug.Log("generating player");
            // 1) Hall (Era 1 capital)
            var hall = Hall.Create(em, spawnPos, fac);
            // NEW: Add population provider (20 population)
            if (!em.HasComponent<PopulationProvider>(hall))
            {
                em.AddComponentData(hall, new PopulationProvider { Amount = 20 });
            }
            
            // 2) Spawn army with proper stats and formation
            // Army spawns slightly offset from Hall
            var armySpawnPos = spawnPos + new float3(8, 0, 8);
            
            // FIXED: Spawn 200 archers per player
            SpawnArmyWithStats(em, armySpawnPos, fac, 
                swordsmenCount: 0, 
                archersCount: 200);

            // 3) Spawn builder separately (off to the side)
            var builderPos = spawnPos + new float3(-5, 0, 5);
            CreateBuilderWithStats(em, builderPos, fac);

            // 4) NEW: Spawn iron deposit near Hall (no ownership - neutral resource)
            //    Position it close enough for easy access but not blocking
            var ironDepositPos = spawnPos + new float3(10, 0, -8);
            TheWaningBorder.Resources.IronDeposit.Create(em, ironDepositPos);
            
            UnityEngine.Debug.Log($"Spawned iron deposit at {ironDepositPos} near {fac} Hall at {spawnPos}");
        }

        /// <summary>
        /// Spawn army in formation with stats from TechTreeDB.
        /// Identical logic to InitialArmyBootstrap.
        /// </summary>
        static void SpawnArmyWithStats(
            EntityManager em,
            float3 centerPos,
            Faction faction,
            int swordsmenCount,
            int archersCount)
        {
            // Calculate formation
            int totalUnits = swordsmenCount + archersCount;
            int cols = (int)math.ceil(math.sqrt(totalUnits));
            int rows = (int)math.ceil(totalUnits / (float)cols);
            
            float spacing = 1.5f;
            float3 right = new float3(1, 0, 0);
            float3 forward = new float3(0, 0, 1);
            
            float width = (cols - 1) * spacing;
            float height = (rows - 1) * spacing;
            float3 topLeft = centerPos - right * (width * 0.5f) - forward * (height * 0.5f);
            
            int unitIndex = 0;
            
            // Spawn swordsmen (front ranks)
            for (int i = 0; i < swordsmenCount; i++)
            {
                int row = unitIndex / cols;
                int col = unitIndex % cols;
                float3 pos = topLeft + right * (col * spacing) + forward * (row * spacing);
                
                CreateSwordsmanWithStats(em, pos, faction);
                unitIndex++;
            }
            
            // Spawn archers (back ranks)
            for (int i = 0; i < archersCount; i++)
            {
                int row = unitIndex / cols;
                int col = unitIndex % cols;
                float3 pos = topLeft + right * (col * spacing) + forward * (row * spacing);
                
                CreateArcherWithStats(em, pos, faction);
                unitIndex++;
            }
        }

        /// <summary>
        /// Create Swordsman with ALL components and stats from TechTreeDB.
        /// </summary>
        static void CreateSwordsmanWithStats(EntityManager em, float3 pos, Faction faction)
        {
            // Create entity with ALL components
            var unit = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(LineOfSight),
                typeof(Target),
                typeof(Radius),
                typeof(AttackCooldown)
            );

            em.SetComponentData(unit, new PresentationId { Id = 201 });
            em.SetComponentData(unit, Unity.Transforms.LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(unit, new FactionTag { Value = faction });
            em.SetComponentData(unit, new UnitTag { Class = UnitClass.Melee });
            em.SetComponentData(unit, new Target { Value = Entity.Null });
            em.SetComponentData(unit, new Radius { Value = 0.5f });
            em.SetComponentData(unit, new AttackCooldown { Cooldown = 1.5f, Timer = 0f });

            // Apply stats from TechTreeDB
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Swordsman", out var udef))
            {
                em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
                em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });

            }
            else
            {

            }
        }

        /// <summary>
        /// Create Archer with ALL components and stats from TechTreeDB.
        /// </summary>
        static void CreateArcherWithStats(EntityManager em, float3 pos, Faction faction)
        {
            // Create entity with ALL components
            var unit = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(ArcherTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(LineOfSight),
                typeof(ArcherState),
                typeof(Target),
                typeof(Radius),
                typeof(AttackCooldown)
            );

            em.SetComponentData(unit, new PresentationId { Id = 202 });
            em.SetComponentData(unit, Unity.Transforms.LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(unit, new FactionTag { Value = faction });
            em.SetComponentData(unit, new UnitTag { Class = UnitClass.Ranged });
            em.SetComponentData(unit, new Target { Value = Entity.Null });
            em.SetComponentData(unit, new Radius { Value = 0.5f });
            em.SetComponentData(unit, new AttackCooldown { Cooldown = 1.5f, Timer = 0f });

            // Apply stats from TechTreeDB
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Archer", out var udef))
            {
                em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(unit, new Damage { Value = (int)udef.damage });
                em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
                
                // Archer-specific state
                em.SetComponentData(unit, new ArcherState
                {
                    CurrentTarget = Entity.Null,
                    AimTimer = 0,
                    AimTimeRequired = 0.5f,
                    CooldownTimer = 0,
                    MinRange = udef.minAttackRange,
                    MaxRange = udef.attackRange,
                    HeightRangeMod = 4f,
                    IsRetreating = 0,
                    IsFiring = 0
                });

            }
            else
            {

            }
        }

        /// <summary>
        /// Create Builder with ALL components and stats from TechTreeDB.
        /// </summary>
        static void CreateBuilderWithStats(EntityManager em, float3 pos, Faction faction)
        {
            var unit = Builder.Create(em, pos, faction);
            
            if (TechTreeDB.Instance != null && 
                TechTreeDB.Instance.TryGetUnit("Builder", out var udef))
            {
                em.SetComponentData(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(unit, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(unit, new LineOfSight { Radius = udef.lineOfSight });
                em.SetComponentData(unit, new Radius { Value = 0.5f });
            }
        }
    }
}