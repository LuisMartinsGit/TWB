// File: Assets/Scripts/ECS/Commands/AttackCommand.cs
using Unity.Entities;

public struct AttackCommand : IComponentData
{
    public Entity Target;
}

// (Optional) helper extensions
public static class AttackCommandExtensions
{
    /// Issues/overwrites an attack order and clears any MoveCommand.
    public static void IssueAttack(this EntityManager em, Entity e, Entity target)
    {
        if (!em.HasComponent<AttackCommand>(e)) em.AddComponent<AttackCommand>(e);
        em.SetComponentData(e, new AttackCommand { Target = target });
        if (em.HasComponent<MoveCommand>(e)) em.RemoveComponent<MoveCommand>(e);
    }

    /// Clears an attack order if present.
    public static void ClearAttack(this EntityManager em, Entity e)
    {
        if (em.HasComponent<AttackCommand>(e)) em.RemoveComponent<AttackCommand>(e);
    }
}
