// File: Assets/Scripts/ECS/Commands/MoveCommand.cs
using Unity.Entities;
using Unity.Mathematics;

public struct MoveCommand : IComponentData
{
    public float3 Destination;
    // (Optional) add extras later, e.g. public float StopDistance;
}

// (Optional) helper extensions
public static class MoveCommandExtensions
{
    /// Issues/overwrites a move order and clears any AttackCommand.
    public static void IssueMove(this EntityManager em, Entity e, float3 destination)
    {
        if (!em.HasComponent<MoveCommand>(e)) em.AddComponent<MoveCommand>(e);
        em.SetComponentData(e, new MoveCommand { Destination = destination });
        if (em.HasComponent<AttackCommand>(e)) em.RemoveComponent<AttackCommand>(e);
    }

    /// Clears a move order if present.
    public static void ClearMove(this EntityManager em, Entity e)
    {
        if (em.HasComponent<MoveCommand>(e)) em.RemoveComponent<MoveCommand>(e);
    }
}
