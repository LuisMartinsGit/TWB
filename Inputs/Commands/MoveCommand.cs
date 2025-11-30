// File: Assets/Scripts/ECS/Commands/MoveCommand.cs
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Component representing a movement command for a unit.
/// </summary>
public struct MoveCommand : IComponentData
{
    public float3 Destination;
}
