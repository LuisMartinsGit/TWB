// File: Assets/Scripts/ECS/Commands/AttackCommand.cs
using Unity.Entities;

/// <summary>
/// Component representing an attack command for a unit.
/// </summary>
public struct AttackCommand : IComponentData
{
    public Entity Target;
}
