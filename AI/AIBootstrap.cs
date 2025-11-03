// AIBootstrap.cs
// Initializes AI players and creates AI brain entities
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace TheWaningBorder.AI
{
    /// <summary>
    /// Initializes AI systems for all non-human players.
    /// Call this after faction banks are created.
    /// </summary>
    public static class AIBootstrap
    {
        /// <summary>
        /// Creates AI brain entities for all AI-controlled factions.
        /// </summary>
        /// <param name="totalPlayers">Total number of players (including human)</param>
        /// <param name="humanPlayerFaction">Faction controlled by human (typically Blue/0)</param>
        public static void InitializeAIPlayers(int totalPlayers, Faction humanPlayerFaction = Faction.Blue)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            Debug.Log($"[AI Bootstrap] Initializing AI for {totalPlayers - 1} AI players");

            for (int i = 0; i < totalPlayers; i++)
            {
                Faction faction = (Faction)i;
                
                // Skip human player
                if (faction == humanPlayerFaction)
                    continue;

                CreateAIBrain(em, faction, GetDefaultPersonality(faction), AIDifficulty.Normal);
            }

            Debug.Log("[AI Bootstrap] AI initialization complete");
        }

        private static void CreateAIBrain(EntityManager em, Faction faction, 
            AIPersonality personality, AIDifficulty difficulty)
        {
            // Create the main AI brain entity
            var brainEntity = em.CreateEntity();

            // Add AI brain component
            em.AddComponentData(brainEntity, new AIBrain
            {
                Owner = faction,
                UpdateInterval = 0.5f, // Think twice per second
                NextUpdateTime = 0,
                IsActive = 1,
                Personality = personality,
                Difficulty = difficulty
            });

            em.AddComponentData(brainEntity, new FactionTag { Value = faction });

            // Add Economy Manager state
            em.AddComponentData(brainEntity, new AIEconomyState
            {
                AssignedMiners = 0,
                DesiredMiners = 0,
                ActiveGatherersHuts = 0,
                DesiredGatherersHuts = 0,
                LastMineAssignmentCheck = 0,
                MineCheckInterval = 5.0f,
                NeedsMoreSupplyIncome = 0,
                NeedsMoreIronIncome = 0
            });

            em.AddBuffer<MineAssignment>(brainEntity);

            // Add Building Manager state
            em.AddComponentData(brainEntity, new AIBuildingState
            {
                ActiveBuilders = 0,
                DesiredBuilders = 0,
                QueuedConstructions = 0,
                LastBuildCheck = 0,
                BuildCheckInterval = 3.0f
            });

            em.AddBuffer<BuildRequest>(brainEntity);

            // Add Military Manager state
            em.AddComponentData(brainEntity, new AIMilitaryState
            {
                TotalSoldiers = 0,
                TotalArchers = 0,
                TotalSiegeUnits = 0,
                ActiveBarracks = 0,
                DesiredBarracks = 0,
                ArmiesCount = 0,
                ScoutsCount = 0,
                LastRecruitmentCheck = 0,
                RecruitmentCheckInterval = 5.0f
            });

            em.AddBuffer<RecruitmentRequest>(brainEntity);

            // Add Scouting Manager state
            em.AddComponentData(brainEntity, new AIScoutingState
            {
                ActiveScouts = 0,
                DesiredScouts = 0,
                LastScoutUpdate = 0,
                ScoutUpdateInterval = 2.0f,
                MapExplorationPercent = 0
            });

            em.AddBuffer<ScoutAssignment>(brainEntity);
            em.AddBuffer<EnemySighting>(brainEntity);

            // Add Mission Manager state
            em.AddComponentData(brainEntity, new AIMissionState
            {
                ActiveMissions = 0,
                PendingMissions = 0,
                LastMissionUpdate = 0,
                MissionUpdateInterval = 4.0f
            });

            // Add Tactical Manager state
            em.AddComponentData(brainEntity, new AITacticalState
            {
                ManagedArmies = 0,
                LastTacticalUpdate = 0,
                TacticalUpdateInterval = 1.0f
            });

            // Add shared knowledge
            em.AddComponentData(brainEntity, new AISharedKnowledge
            {
                EnemyLastKnownPosition = float3.zero,
                EnemyLastSeenTime = 0,
                EnemyEstimatedStrength = 0,
                KnownEnemyBases = 0,
                OwnMilitaryStrength = 0,
                OwnEconomicStrength = 0
            });

            // Add resource requests buffer
            em.AddBuffer<ResourceRequest>(brainEntity);

            Debug.Log($"[AI Bootstrap] Created AI brain for {faction} with {personality} personality");
        }

        private static AIPersonality GetDefaultPersonality(Faction faction)
        {
            // Assign different personalities to different AI players for variety
            return faction switch
            {
                Faction.Red => AIPersonality.Aggressive,
                Faction.Green => AIPersonality.Defensive,
                Faction.Yellow => AIPersonality.Economic,
                Faction.Purple => AIPersonality.Balanced,
                Faction.Orange => AIPersonality.Rush,
                Faction.Teal => AIPersonality.Balanced,
                Faction.White => AIPersonality.Aggressive,
                _ => AIPersonality.Balanced
            };
        }

        /// <summary>
        /// Changes AI difficulty for a specific faction at runtime
        /// </summary>
        public static void SetAIDifficulty(Faction faction, AIDifficulty difficulty)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            var query = em.CreateEntityQuery(typeof(AIBrain), typeof(FactionTag));
            var entities = query.ToEntityArray(Allocator.Temp);
            var brains = query.ToComponentDataArray<AIBrain>(Allocator.Temp);
            var factions = query.ToComponentDataArray<FactionTag>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (factions[i].Value == faction)
                {
                    var brain = brains[i];
                    brain.Difficulty = difficulty;
                    em.SetComponentData(entities[i], brain);
                    Debug.Log($"[AI Bootstrap] Set {faction} difficulty to {difficulty}");
                    break;
                }
            }

            entities.Dispose();
            brains.Dispose();
            factions.Dispose();
        }

        /// <summary>
        /// Changes AI personality for a specific faction at runtime
        /// </summary>
        public static void SetAIPersonality(Faction faction, AIPersonality personality)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            var query = em.CreateEntityQuery(typeof(AIBrain), typeof(FactionTag));
            var entities = query.ToEntityArray(Allocator.Temp);
            var brains = query.ToComponentDataArray<AIBrain>(Allocator.Temp);
            var factions = query.ToComponentDataArray<FactionTag>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (factions[i].Value == faction)
                {
                    var brain = brains[i];
                    brain.Personality = personality;
                    em.SetComponentData(entities[i], brain);
                    Debug.Log($"[AI Bootstrap] Set {faction} personality to {personality}");
                    break;
                }
            }

            entities.Dispose();
            brains.Dispose();
            factions.Dispose();
        }

        /// <summary>
        /// Disables/enables AI for a specific faction
        /// </summary>
        public static void SetAIActive(Faction faction, bool active)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            var query = em.CreateEntityQuery(typeof(AIBrain), typeof(FactionTag));
            var entities = query.ToEntityArray(Allocator.Temp);
            var brains = query.ToComponentDataArray<AIBrain>(Allocator.Temp);
            var factions = query.ToComponentDataArray<FactionTag>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (factions[i].Value == faction)
                {
                    var brain = brains[i];
                    brain.IsActive = active ? (byte)1 : (byte)0;
                    em.SetComponentData(entities[i], brain);
                    Debug.Log($"[AI Bootstrap] {faction} AI is now {(active ? "active" : "inactive")}");
                    break;
                }
            }

            entities.Dispose();
            brains.Dispose();
            factions.Dispose();
        }
    }
}