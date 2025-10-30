using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Archer-specific combat state including aim time and firing mode
/// </summary>
public struct ArcherCombatState : IComponentData
{
    public float AimTimer;           // Countdown to when arrow is released
    public float AimDuration;        // How long we need to aim (set based on distance)
    public byte IsAiming;            // 0 = not aiming, 1 = currently aiming
    public byte FiringMode;          // 0 = horizontal, 1 = parabolic
    public float3 AimPoint;          // Where we're aiming (predicted target position)
}

/// <summary>
/// Archer range configuration with height-based modifiers
/// </summary>
public struct ArcherRange : IComponentData
{
    public float MinRange;           // Minimum engagement distance (6 units)
    public float BaseMaxRange;       // Base max range (18 units)
    public float HeightRangeMod;     // ±4 units per height difference
    public float ParabolicThreshold; // Distance above which we use parabolic (10 units)
}

/// <summary>
/// Configuration for aim time calculation
/// </summary>
public struct ArcherAimTime : IComponentData
{
    public float BaseAimTime;        // Base aim time at min range (e.g., 0.3s)
    public float MaxAimTime;         // Max aim time at max range (e.g., 1.2s)
}

/// <summary>
/// Arrow projectile in flight - replaces generic Projectile for archers
/// </summary>
public struct ArrowProjectile : IComponentData
{
    public float3 StartPos;          // Launch position
    public float3 TargetPos;         // Target position at launch
    public float3 Velocity;          // Current velocity vector
    public double LaunchTime;        // When it was fired
    public float FlightTime;         // How long until impact
    public float Gravity;            // Gravity constant (-9.81 or adjusted)
    public int Damage;               // Damage on hit
    public Entity Target;            // Target entity (may be destroyed during flight)
    public Entity Shooter;           // Who shot it (for friendly fire checking)
    public Faction ShooterFaction;   // Faction of shooter
    public bool IsParabolic;         // 0 = horizontal, 1 = parabolic arc
}

/// <summary>
/// Tag to mark an entity as an archer (for system queries)
/// </summary>
public struct ArcherTag : IComponentData { }

/// <summary>
/// Retreat behavior when enemy gets too close
/// </summary>
public struct ArcherRetreat : IComponentData
{
    public byte IsRetreating;        // 0 = normal, 1 = backing away
    public float3 RetreatDirection;  // Direction to move when retreating
    public float RetreatSpeed;       // Speed multiplier when retreating (e.g., 1.2x normal)
}

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