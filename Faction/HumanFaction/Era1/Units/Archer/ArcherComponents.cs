using Unity.Entities;
using Unity.Mathematics;


public struct ArrowProjectile : IComponentData
{
    public float3 Velocity;          // Current velocity vector
    public float Gravity;            // Gravity constant (-9.81 or adjusted)
    public Entity Shooter;           // Who shot it (for friendly fire checking)
    public bool IsParabolic;         // 0 = horizontal, 1 = parabolic arc
}

public struct ArcherTag : IComponentData { }


public struct ArcherState : IComponentData
{
    public Entity CurrentTarget;
    public float AimTimer;           // Time spent aiming at current target
    public float AimTimeRequired;    // How long to aim before firing
    public float CooldownTimer;      // Time until can fire again
    public float MinRange;           // Don't shoot closer than this
    public float MaxRange;           // Base maximum range
    public float HeightRangeMod;     // Range bonus/penalty per unit height difference
    public byte IsRetreating;        // 1 if backing away from too-close enemy
    public byte IsFiring;            // 1 when actively firing
}