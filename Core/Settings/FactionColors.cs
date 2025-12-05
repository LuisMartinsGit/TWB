// FactionColors.cs
// Central palette for faction colors
// Place in: Assets/Scripts/Core/Settings/FactionColors.cs

using UnityEngine;

/// <summary>
/// Central palette for faction colors. Use everywhere that needs consistent per-player colors
/// (minimap blips, selection/indicator decals, UI, health bars, etc.).
/// </summary>
public static class FactionColors
{
    // Tweak these to your art direction
    private static readonly Color Blue   = new Color(0.20f, 0.55f, 1.00f, 1f);
    private static readonly Color Red    = new Color(1.00f, 0.20f, 0.25f, 1f);
    private static readonly Color Green  = new Color(0.20f, 0.90f, 0.35f, 1f);
    private static readonly Color Yellow = new Color(1.00f, 0.85f, 0.20f, 1f);
    private static readonly Color Purple = new Color(0.80f, 0.40f, 1.00f, 1f);
    private static readonly Color Orange = new Color(1.00f, 0.55f, 0.15f, 1f);
    private static readonly Color Teal   = new Color(0.20f, 1.00f, 0.95f, 1f);
    private static readonly Color White  = new Color(1.00f, 1.00f, 1.00f, 1f);

    /// <summary>
    /// Get the primary color for a faction.
    /// </summary>
    public static Color Get(Faction f)
    {
        return f switch
        {
            Faction.Blue   => Blue,
            Faction.Red    => Red,
            Faction.Green  => Green,
            Faction.Yellow => Yellow,
            Faction.Purple => Purple,
            Faction.Orange => Orange,
            Faction.Teal   => Teal,
            Faction.White  => White,
            _              => White
        };
    }

    /// <summary>
    /// Alpha-tinted version for "revealed but not visible" (ghost) cases in fog of war.
    /// </summary>
    public static Color Ghost(Color baseColor, float alpha = 0.55f)
    {
        baseColor.a = Mathf.Clamp01(alpha);
        return baseColor;
    }

    /// <summary>
    /// Get a darker/desaturated version for UI backgrounds or shadows.
    /// </summary>
    public static Color GetDark(Faction f, float darkenFactor = 0.3f)
    {
        var c = Get(f);
        return new Color(
            c.r * darkenFactor,
            c.g * darkenFactor,
            c.b * darkenFactor,
            c.a
        );
    }

    /// <summary>
    /// Get faction name as a display string.
    /// </summary>
    public static string GetName(Faction f)
    {
        return f.ToString();
    }
}