using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.AI.AIController
{
    public enum AIState
    {
        Idle,
        Gathering,
        Building,
        Attacking,
        Defending,
        Exploring,
        Retreating
    }
    
    public struct AIControllerComponent : IComponentData
    {
        public AIState CurrentState;
        public float StateChangeTime;
        public float DecisionInterval;
        public float LastDecisionTime;
        public int Difficulty;
    }
}