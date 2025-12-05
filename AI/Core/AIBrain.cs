// AIBrain.cs
// Core AI controller component and initialization
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace TheWaningBorder.AI
{
    // ==================== AI Brain Component ====================

    /// <summary>
    /// Main AI controller for a faction. One per AI player.
    /// </summary>
    public struct AIBrain : IComponentData
    {
        public Faction Owner;
        public float UpdateInterval;
        public float NextUpdateTime;
        public byte IsActive;
        public AIPersonality Personality;
        public AIDifficulty Difficulty;
    }

    public enum AIPersonality : byte
    {
        Balanced = 0,
        Aggressive = 1,
        Defensive = 2,
        Economic = 3,
        Rush = 4
    }

    public enum AIDifficulty : byte
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }

    // ==================== Shared Knowledge ====================

    public struct AISharedKnowledge : IComponentData
    {
        public float3 EnemyLastKnownPosition;
        public double EnemyLastSeenTime;
        public int EnemyEstimatedStrength;
        public int KnownEnemyBases;
        public int OwnMilitaryStrength;
        public int OwnEconomicStrength;
        public int EnemyBasesSpotted;
        public int EnemyArmiesSpotted;
    }

    public struct ResourceRequest : IBufferElementData
    {
        public int Supplies;
        public int Iron;
        public int Crystal;
        public int Veilsteel;
        public int Glow;
        public int Priority;
        public Entity Requester;
        public byte Approved;
    }

    // ==================== AI Bootstrap ====================

    /// <summary>
    /// Initializes AI systems for all non-human players.
    /// </summary>
    public static class AIBootstrap
    {
        public static void InitializeAIPlayers(int totalPlayers, Faction humanPlayerFaction = Faction.Blue)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            for (int i = 0; i < totalPlayers; i++)
            {
                Faction faction = (Faction)i;

                if (GameSettings.IsFactionHumanControlled(faction))
                    continue;

                CreateAIBrain(em, faction, GetDefaultPersonality(faction), AIDifficulty.Normal);
            }
        }

        private static void CreateAIBrain(EntityManager em, Faction faction,
            AIPersonality personality, AIDifficulty difficulty)
        {
            var brainEntity = em.CreateEntity();

            em.AddComponentData(brainEntity, new AIBrain
            {
                Owner = faction,
                UpdateInterval = 0.5f,
                NextUpdateTime = 0,
                IsActive = 1,
                Personality = personality,
                Difficulty = difficulty
            });

            em.AddComponentData(brainEntity, new FactionTag { Value = faction });

            // Economy Manager state
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
            em.AddBuffer<ResourceRequest>(brainEntity);

            // Building Manager state
            em.AddComponentData(brainEntity, new AIBuildingState
            {
                ActiveBuilders = 0,
                DesiredBuilders = 0,
                QueuedConstructions = 0,
                LastBuildCheck = 0,
                BuildCheckInterval = 3.0f
            });

            em.AddBuffer<BuildRequest>(brainEntity);

            // Military Manager state
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

            // Scouting Manager state
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
            em.AddBuffer<ExplorationZone>(brainEntity);

            // Mission Manager state
            em.AddComponentData(brainEntity, new AIMissionState
            {
                ActiveMissions = 0,
                PendingMissions = 0,
                LastMissionUpdate = 0,
                MissionUpdateInterval = 4.0f
            });

            // Tactical Manager state
            em.AddComponentData(brainEntity, new AITacticalState
            {
                ManagedArmies = 0,
                LastTacticalUpdate = 0,
                TacticalUpdateInterval = 1.0f
            });

            // Shared knowledge
            em.AddComponentData(brainEntity, new AISharedKnowledge
            {
                EnemyLastKnownPosition = float3.zero,
                EnemyLastSeenTime = 0,
                EnemyEstimatedStrength = 0,
                KnownEnemyBases = 0,
                OwnMilitaryStrength = 0,
                OwnEconomicStrength = 0
            });
        }

        private static AIPersonality GetDefaultPersonality(Faction faction)
        {
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
                    break;
                }
            }

            entities.Dispose();
            brains.Dispose();
            factions.Dispose();
        }
    }
}