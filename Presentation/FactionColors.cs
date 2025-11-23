using UnityEngine;

/// <summary>
/// Central palette for faction colors. Use everywhere that needs consistent per-player colors
/// (minimap blips, selection/indicator decals, UI, etc.).
/// </summary>
public static class FactionColors
{
    // Tweak these to your art direction.
    private static readonly Color Blue    = new Color(0.20f, 0.55f, 1.00f, 1f);
    private static readonly Color Red     = new Color(1.00f, 0.20f, 0.25f, 1f);
    private static readonly Color Green   = new Color(0.20f, 0.90f, 0.35f, 1f);
    private static readonly Color Yellow  = new Color(1.00f, 0.85f, 0.20f, 1f);
    private static readonly Color Purple  = new Color(0.80f, 0.40f, 1.00f, 1f);
    private static readonly Color Orange  = new Color(1.00f, 0.55f, 0.15f, 1f);
    private static readonly Color Teal    = new Color(0.20f, 1.00f, 0.95f, 1f);
    private static readonly Color White   = new Color(1.00f, 1.00f, 1.00f, 1f);

    public static Color Get(Faction f)
    {
        switch (f)
        {
            case Faction.Blue:   return Blue;
            case Faction.Red:    return Red;
            case Faction.Green:  return Green;
            case Faction.Yellow: return Yellow;
            case Faction.Purple: return Purple;
            case Faction.Orange: return Orange;
            case Faction.Teal:   return Teal;
            default:             return White;
        }
    }

    /// <summary>Alpha-tinted version for “revealed but not visible” (ghost) cases.</summary>
    public static Color Ghost(Color baseColor, float alpha = 0.55f)
    {
        baseColor.a = Mathf.Clamp01(alpha);
        return baseColor;
    }
}
