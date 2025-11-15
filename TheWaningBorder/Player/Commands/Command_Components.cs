using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Player.Commands
{
    public enum CommandType
    {
        Move,
        Attack,
        AttackMove,
        Build,
        Gather,
        Stop,
        HoldPosition,
        Patrol
    }
    
    public struct CommandComponent : IComponentData
    {
        public CommandType Type;
        public float3 TargetPosition;
        public Entity TargetEntity;
        public FixedString64Bytes BuildingId;
        public bool Queued;
        public float IssuedTime;
    }
}