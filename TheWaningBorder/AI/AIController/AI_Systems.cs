using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.GameManager;
using Random = Unity.Mathematics.Random;

namespace TheWaningBorder.AI.AIController
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AIControllerSystem : SystemBase
    {
        private Random _random;

        protected override void OnCreate()
        {
            _random = new Random(123);
        }

        protected override void OnUpdate()
        {
            // Cache values so the lambda doesn't capture 'this'
            var currentTime = (float)SystemAPI.Time.ElapsedTime;
            var rand = _random;

            // Use a lookup instead of a lambda parameter for PlayerComponent to avoid DC0005
            var playerLookup = GetComponentLookup<Core.GameManager.PlayerComponent>(isReadOnly: true);

            Entities
                .WithName("AIController_Decisions")
                .WithBurst()
                .WithReadOnly(playerLookup)
                .ForEach((Entity entity, ref AIControllerComponent ai) =>
                {
                    // If the entity doesn't have PlayerComponent, skip
                    if (!playerLookup.HasComponent(entity))
                        return;

                    var player = playerLookup[entity];
                    if (player.IsHuman || !player.IsAlive)
                        return;

                    if (currentTime - ai.LastDecisionTime >= ai.DecisionInterval)
                    {
                        ai.LastDecisionTime = currentTime;
                        MakeDecisionStatic(entity, ref ai, player.PlayerId, ref rand, currentTime);
                    }
                })
                .Run();

            // Persist RNG state
            _random = rand;
        }

        private static void MakeDecisionStatic(
            Entity aiEntity,
            ref AIControllerComponent ai,
            int playerId,
            ref Random rand,
            float currentTime)
        {
            switch (ai.CurrentState)
            {
                case AIState.Idle:
                {
                    // Decide what to do next
                    int decision = rand.NextInt(0, 3);
                    ai.CurrentState = decision switch
                    {
                        0 => AIState.Gathering,
                        1 => AIState.Building,
                        _ => AIState.Exploring
                    };
                    ai.StateChangeTime = currentTime;
                    break;
                }

                case AIState.Gathering:
                    // TODO: Send miners to gather resources
                    break;

                case AIState.Building:
                    // TODO: Build structures
                    break;

                case AIState.Attacking:
                    // TODO: Attack enemies
                    break;

                case AIState.Defending:
                    // TODO: Defend base
                    break;
            }
        }
    }
}
