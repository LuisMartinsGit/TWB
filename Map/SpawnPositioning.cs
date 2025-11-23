// Assets/Scripts/SpawnPositioning.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates spawn positions based on menu-selected layout,
/// terrain/map bounds, configurable edge buffers, and a minimum separation.
/// Requires (and uses) the following in GameSettings:
///   - SpawnLayout SpawnLayout
///   - TwoSidesPreset TwoSides
///   - int SpawnSeed
///   - int SpawnEdgeBufferMin, SpawnEdgeBufferMax
///   - int SpawnMinSeparation  (100 recommended)
/// </summary>
public static class SpawnPositioning
{
    // ---- Public entry ----
    public static List<Vector3> Generate(int playerCount)
    {
        var (min, max) = GetWorldBounds(out float halfX, out float halfZ);

        // Base safe border scaled by map, but never smaller than the min edge buffer
        float baseBorder = 0.12f * Mathf.Min(halfX, halfZ);
        float minBuf = Mathf.Min(GameSettings.SpawnEdgeBufferMin, GameSettings.SpawnEdgeBufferMax);
        float safeBorder = Mathf.Max(baseBorder, minBuf);

        float ringRadius = Mathf.Min(halfX, halfZ) - safeBorder;

        var rnd = new System.Random(GameSettings.SpawnSeed);

        List<Vector3> result;
        switch (GameSettings.SpawnLayout)
        {
            case SpawnLayout.Circle:
                result = Circle(playerCount, ringRadius, rnd, min, max);
                break;

            case SpawnLayout.TwoSides:
                result = TwoSides(playerCount, min, max, GameSettings.TwoSides, rnd);
                break;

            case SpawnLayout.TwoEachSide8:
                result = TwoEachSide8(playerCount, min, max, rnd);
                break;

            default:
                result = Circle(playerCount, ringRadius, rnd, min, max);
                break;
        }

        // Final pass: ensure we have exactly playerCount positions and
        // they all respect minimum separation (will add/adjust extras if needed).
        EnsureCountAndSeparation(result, playerCount, min, max, rnd);

        return result;
    }

    // -------- Helpers (world bounds, randomness, clamping, distances) --------

    private static (Vector2 min, Vector2 max) GetWorldBounds(out float halfX, out float halfZ)
    {
        var t = Terrain.activeTerrain;
        if (t && t.terrainData)
        {
            var td = t.terrainData;
            var p = t.transform.position;
            var s = td.size;
            halfX = s.x * 0.5f;
            halfZ = s.z * 0.5f;
            return (new Vector2(p.x, p.z), new Vector2(p.x + s.x, p.z + s.z));
        }

        float h = Mathf.Max(16, GameSettings.MapHalfSize);
        halfX = halfZ = h;
        return (new Vector2(-h, -h), new Vector2(h, h));
    }

    private static float Rand(System.Random r, float a, float b)
        => (float)(a + (b - a) * r.NextDouble());

    private static float RandBuf(System.Random r)
    {
        // Ensure min<=max; if equal, deterministic constant.
        int a = GameSettings.SpawnEdgeBufferMin;
        int b = GameSettings.SpawnEdgeBufferMax;
        if (a > b) { var tmp = a; a = b; b = tmp; }
        return Rand(r, a, b);
    }

    private static Vector3 TowardsCenter(Vector3 p, Vector2 min, Vector2 max, float t)
    {
        var c = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
        return Vector3.Lerp(p, c, Mathf.Clamp01(t));
    }

    private static int MinSep() => (GameSettings.SpawnMinSeparation > 0 ? GameSettings.SpawnMinSeparation : 100);

    private static bool FarFromAll(Vector3 candidate, List<Vector3> existing, float minDist)
    {
        float minDistSq = minDist * minDist;
        for (int i = 0; i < existing.Count; i++)
        {
            if ((candidate - existing[i]).sqrMagnitude < minDistSq)
                return false;
        }
        return true;
    }

    private static Vector3 RandomInsideBounds(System.Random r, Vector2 min, Vector2 max, float edgeBuffer)
    {
        float x = Rand(r, min.x + edgeBuffer, max.x - edgeBuffer);
        float z = Rand(r, min.y + edgeBuffer, max.y - edgeBuffer);
        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Ensures the list has exactly 'required' positions and all pairs are at least MinSep apart.
    /// If there are too few (e.g. layout with 4 slots but 5 players), it will add randomized positions
    /// biased toward the interior, each respecting min separation.
    /// </summary>
    private static void EnsureCountAndSeparation(List<Vector3> positions, int required, Vector2 min, Vector2 max, System.Random rnd)
    {
        // 1) If duplicates slipped in, lightly perturb them apart before separation enforcement.
        // (rare, but cheap)
        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                if ((positions[i] - positions[j]).sqrMagnitude < 0.01f)
                {
                    positions[j] += new Vector3(0.5f, 0f, -0.5f);
                }
            }
        }

        // 2) Add extras if fewer than required, respecting separation
        float minBuf = Mathf.Min(GameSettings.SpawnEdgeBufferMin, GameSettings.SpawnEdgeBufferMax);
        int attemptsPerExtra = 64;
        while (positions.Count < required)
        {
            bool placed = false;
            for (int attempt = 0; attempt < attemptsPerExtra && !placed; attempt++)
            {
                // Bias inward: shrink the usable rectangle slightly per attempt
                float bias = Mathf.Lerp(0.65f, 0.90f, (float)rnd.NextDouble()); // 65%..90% toward center
                float cx = (min.x + max.x) * 0.5f;
                float cz = (min.y + max.y) * 0.5f;
                float halfX = (max.x - min.x) * 0.5f * bias;
                float halfZ = (max.y - min.y) * 0.5f * bias;

                float x = Rand(rnd, cx - halfX, cx + halfX);
                float z = Rand(rnd, cz - halfZ, cz + halfZ);

                // Clamp to edge buffer
                x = Mathf.Clamp(x, min.x + minBuf, max.x - minBuf);
                z = Mathf.Clamp(z, min.y + minBuf, max.y - minBuf);

                var candidate = new Vector3(x, 0f, z);
                if (FarFromAll(candidate, positions, MinSep()))
                {
                    positions.Add(candidate);
                    placed = true;
                }
            }

            // If we failed to place after many attempts (very crowded small maps),
            // relax by slightly shrinking min separation and try again.
            if (!placed)
            {
                // Force-add with tiny perturbation away from the closest neighbor.
                // (This is a last resort; extremely unlikely at your scales.)
                Vector3 c = RandomInsideBounds(rnd, min, max, minBuf);
                positions.Add(c);
            }
        }

        // 3) If more than required (shouldn't happen), trim.
        if (positions.Count > required)
            positions.RemoveRange(required, positions.Count - required);

        // 4) Final small jitter + clamp (won’t break 100u sep given jitter is small)
        positions = JitterAndClamp(positions, rnd, min, max);
    }

    private static List<Vector3> JitterAndClamp(List<Vector3> src, System.Random r, Vector2 min, Vector2 max)
    {
        var dst = new List<Vector3>(src.Count);

        float spanX = max.x - min.x;
        float spanZ = max.y - min.y;

        // jitter amount ~2% of axis length (clamped)
        float jx = Mathf.Clamp(spanX * 0.02f, 0.5f, 6f);
        float jz = Mathf.Clamp(spanZ * 0.02f, 0.5f, 6f);

        float minBuf = Mathf.Min(GameSettings.SpawnEdgeBufferMin, GameSettings.SpawnEdgeBufferMax);

        foreach (var p in src)
        {
            float x = Mathf.Clamp(p.x + Rand(r, -jx, jx), min.x + minBuf, max.x - minBuf);
            float z = Mathf.Clamp(p.z + Rand(r, -jz, jz), min.y + minBuf, max.y - minBuf);
            dst.Add(new Vector3(x, 0f, z));
        }
        return dst;
    }

    // -------- Layouts --------

    // Circle: evenly spaced around a ring, then jitter+clamp.
    private static List<Vector3> Circle(int n, float r, System.Random rnd, Vector2 min, Vector2 max)
    {
        var list = new List<Vector3>(Mathf.Max(0, n));
        if (n <= 0) return list;

        float jitter = Mathf.Clamp(r * 0.06f, 1.5f, 10f);

        for (int i = 0; i < n; i++)
        {
            float ang = (Mathf.PI * 2f) * (i / (float)n);
            float rr = Mathf.Max(0.01f, r + Rand(rnd, -jitter, jitter));
            list.Add(new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr));
        }

        return JitterAndClamp(list, rnd, min, max);
    }

    // TwoSides: 4 base slots (2 per chosen sides). For n > 4, we add randomized extras (separated).
    private static List<Vector3> TwoSides(int n, Vector2 min, Vector2 max, TwoSidesPreset preset, System.Random rnd)
    {
        var slots = new List<Vector3>(4);

        // Random inward buffers for the two active sides (independent so halves differ a bit)
        float bufA = RandBuf(rnd);
        float bufB = RandBuf(rnd);

        void Left()
        {
            float x = min.x + bufA;
            slots.Add(new Vector3(x, 0, Mathf.Lerp(min.y, max.y, 0.25f)));
            slots.Add(new Vector3(x, 0, Mathf.Lerp(min.y, max.y, 0.75f)));
        }
        void Right()
        {
            float x = max.x - bufA;
            slots.Add(new Vector3(x, 0, Mathf.Lerp(min.y, max.y, 0.25f)));
            slots.Add(new Vector3(x, 0, Mathf.Lerp(min.y, max.y, 0.75f)));
        }
        void Down()
        {
            float z = min.y + bufB;
            slots.Add(new Vector3(Mathf.Lerp(min.x, max.x, 0.25f), 0, z));
            slots.Add(new Vector3(Mathf.Lerp(min.x, max.x, 0.75f), 0, z));
        }
        void Up()
        {
            float z = max.y - bufB;
            slots.Add(new Vector3(Mathf.Lerp(min.x, max.x, 0.25f), 0, z));
            slots.Add(new Vector3(Mathf.Lerp(min.x, max.x, 0.75f), 0, z));
        }

        switch (preset)
        {
            case TwoSidesPreset.LeftRight:  Left(); Right(); break;
            case TwoSidesPreset.UpDown:     Up();   Down();  break;
            case TwoSidesPreset.LeftUp:     Left(); Up();    break;
            case TwoSidesPreset.LeftDown:   Left(); Down();  break;
            case TwoSidesPreset.RightUp:    Right();Up();    break;
            case TwoSidesPreset.RightDown:  Right();Down();  break;
        }

        var chosen = new List<Vector3>(Mathf.Max(0, Mathf.Min(n, 4)));

        if (n >= 4)
        {
            // Fill 2 per side consistently
            chosen.AddRange(slots);
        }
        else if (n == 3)
        {
            // Balance: one per side, odd one toward center along a side
            chosen.Add(slots[0]); // side A #1
            chosen.Add(slots[2]); // side B #1
            chosen.Add(TowardsCenter(slots[1], min, max, 0.35f + (float)rnd.NextDouble() * 0.15f));
        }
        else if (n == 2)
        {
            chosen.Add(slots[0]);
            chosen.Add(slots[2]);
        }
        else if (n == 1)
        {
            chosen.Add(TowardsCenter(slots[0], min, max, 0.5f));
        }

        // If we need more than 4 (e.g., n=5), add extras randomly with separation.
        if (n > chosen.Count)
        {
            int toAdd = n - chosen.Count;
            AddSeparatedExtras(chosen, toAdd, min, max, rnd);
        }

        return JitterAndClamp(chosen, rnd, min, max);
    }

    // TwoEachSide8: 8 base slots → 4 corners + 4 mid-edges (buffered). For n>8, add separated extras.
    private static List<Vector3> TwoEachSide8(int n, Vector2 min, Vector2 max, System.Random rnd)
    {
        var chosen = new List<Vector3>(Mathf.Clamp(n, 0, 8));
        if (n <= 0) return chosen;

        float bufL = RandBuf(rnd), bufR = RandBuf(rnd), bufD = RandBuf(rnd), bufU = RandBuf(rnd);

        var corners = new[]
        {
            new Vector3(min.x + bufL, 0, min.y + bufD), // LD
            new Vector3(min.x + bufL, 0, max.y - bufU), // LU
            new Vector3(max.x - bufR, 0, min.y + bufD), // RD
            new Vector3(max.x - bufR, 0, max.y - bufU), // RU
        };

        var medges = new[]
        {
            new Vector3(min.x + bufL, 0, Mathf.Lerp(min.y, max.y, 0.25f)),
            new Vector3(min.x + bufL, 0, Mathf.Lerp(min.y, max.y, 0.75f)),
            new Vector3(max.x - bufR, 0, Mathf.Lerp(min.y, max.y, 0.25f)),
            new Vector3(max.x - bufR, 0, Mathf.Lerp(min.y, max.y, 0.75f)),
        };

        var order = new List<Vector3>(8);
        order.AddRange(corners); // first 4: corners
        order.AddRange(medges);  // next 4: mid-edges

        int baseCount = Mathf.Min(n, 8);
        for (int i = 0; i < baseCount; i++)
            chosen.Add(order[i]);

        if (n % 2 == 1 && chosen.Count > 0)
            chosen[^1] = TowardsCenter(chosen[^1], min, max, 0.25f);

        // For n > 8, add separated extras
        if (n > chosen.Count)
        {
            int toAdd = n - chosen.Count;
            AddSeparatedExtras(chosen, toAdd, min, max, rnd);
        }

        return JitterAndClamp(chosen, rnd, min, max);
    }

    // ---- Extras & separation enforcement ----

    private static void AddSeparatedExtras(List<Vector3> positions, int count, Vector2 min, Vector2 max, System.Random rnd)
    {
        float minBuf = Mathf.Min(GameSettings.SpawnEdgeBufferMin, GameSettings.SpawnEdgeBufferMax);
        int attemptsPerExtra = 64;

        for (int k = 0; k < count; k++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < attemptsPerExtra && !placed; attempt++)
            {
                // Bias the random extras toward the interior so they don't hug edges.
                float bias = Mathf.Lerp(0.65f, 0.90f, (float)rnd.NextDouble()); // shrink region toward center
                float cx = (min.x + max.x) * 0.5f;
                float cz = (min.y + max.y) * 0.5f;
                float halfX = (max.x - min.x) * 0.5f * bias;
                float halfZ = (max.y - min.y) * 0.5f * bias;

                float x = Rand(rnd, cx - halfX, cx + halfX);
                float z = Rand(rnd, cz - halfZ, cz + halfZ);
                x = Mathf.Clamp(x, min.x + minBuf, max.x - minBuf);
                z = Mathf.Clamp(z, min.y + minBuf, max.y - minBuf);

                var candidate = new Vector3(x, 0f, z);
                if (FarFromAll(candidate, positions, MinSep()))
                {
                    positions.Add(candidate);
                    placed = true;
                }
            }

            if (!placed)
            {
                // Fallback in pathological cases: just drop one anywhere inside buffer
                positions.Add(RandomInsideBounds(rnd, min, max, minBuf));
            }
        }
    }
}
