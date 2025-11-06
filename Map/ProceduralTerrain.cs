using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedural terrain that:
/// - carves two irregular (non-circular) spawn clearings on west/east,
///   by mean-preserving smoothing (no height offset vs surroundings),
/// - sculpts hills/mountains with narrow passes/chokepoints,
/// - fixes tiny 1–2 cell pits and limits max slope,
/// - auto-loads TerrainLayers from Resources/Map/Terrains,
/// - blends textures by height/slope with a cliff layer,
/// - adds a wave-animated water surface.
/// Attach once in the Game scene (runs before players spawn).
/// </summary>
[DefaultExecutionOrder(-100)]
public class ProceduralTerrain : MonoBehaviour
{
    [Header("World (must match FogOfWar bounds)")]
    public Vector2 worldMin = new(-125, -125);
    public Vector2 worldMax = new( 125,  125);
    public int heightmapRes = 513;          // power-of-two + 1
    public int controlTexRes = 512;         // splatmap resolution
    public float maxHeight = 40f;           // vertical scale
    [Range(0,1)] public float seaLevel = 0.32f; // 0..1 of heightmap

    [Header("Noise (continents → details)")]
    [Tooltip("Lower = bigger features. Inspector values are multiplied by internal scales.")]
    public float contFreq = 0.003f;         // big shapes
    public float hillsFreq = 0.015f;        // rolling
    public float mountainsFreq = 0.05f;     // jagged
    [Range(0,1)] public float mountainsAmp = 0.45f;  // ridges strength
    public int seed = 12345;

    [Header("Spawn Clearings (irregular, mean-preserving)")]
    [Tooltip("Approximate radius of each clearing, in world units.")]
    public float clearingRadius = 50f;
    [Tooltip("Feather width for the clearing edge, in world units.")]
    public float clearingFeather = 16f;
    [Tooltip("Edge wobble amount as a fraction of radius (0..0.5).")]
    [Range(0,0.5f)] public float clearingNoiseAmp = 0.18f;
    [Tooltip("Noise frequency (world units). Higher = more bumps around the rim.")]
    public float clearingNoiseFreq = 0.035f;
    [Tooltip("How many smoothing iterations within the clearing.")]
    [Range(1,24)] public int clearingSmoothIterations = 10;
    [Tooltip("Kernel half-size in cells for clearing smoothing (1=3x3, 2=5x5).")]
    [Range(1,4)] public int clearingKernelRadius = 2;

    [Header("Passes & Chokepoints")]
    [Tooltip("Depth of carved valleys (0..1 of heightmap magnitude).")]
    [Range(0,0.6f)] public float passDepth = 0.18f;
    [Tooltip("How sharp/narrow the pass lines are.")]
    [Range(1f,6f)] public float passSharp = 3.2f;
    [Tooltip("Density of the pass network (higher = more lines).")]
    [Range(6f,30f)] public float passScale = 16f;

    [Header("Post-process Smoothing")]
    [Tooltip("Target maximum terrain inclination (degrees). Lower = gentler slopes.")]
    [Range(5,70)] public float maxSlopeDeg = 32f;
    [Tooltip("How many blur/relax iterations to soften spikes and reduce pits.")]
    [Range(0,8)] public int smoothIterations = 3;
    [Tooltip("How strongly to lift tiny 1–2 cell pits toward neighbors.")]
    [Range(0,1)] public float pitFixStrength = 0.55f;

    [Header("Cliff Texturing")]
    [Tooltip("Slope at which cliff texture begins to appear (degrees).")]
    [Range(5,70)] public float cliffStartDeg = 26f;
    [Tooltip("Slope at which the cliff texture fully takes over (degrees).")]
    [Range(5,85)] public float cliffFullDeg  = 40f;

    [Header("Height Bands (for texture blending)")]
    [Tooltip("Approximate normalized heights for bands (0..1).")]
    [Range(0,1)] public float hBeachMax = 0.34f;
    [Range(0,1)] public float hLowMax   = 0.50f;
    [Range(0,1)] public float hMidMax   = 0.68f;
    [Range(0,1)] public float hHighMax  = 0.82f; // snow after this (and enough flatness)

    [Header("Biomes (temperature/moisture)")]
    public float tempFreq  = 0.0025f;
    public float moistFreq = 0.0030f;

    [Header("Terrain Layers (auto loads from Resources/Map/Terrains)")]
    [Tooltip("Optional fallback if not found in Resources (by name).")]
    public TerrainLayer grass;
    public TerrainLayer rock;
    public TerrainLayer snow;
    public TerrainLayer sand;
    public TerrainLayer dirt;
    [Tooltip("Used on steep slopes (cliffs).")]
    public TerrainLayer cliff;

    [Header("Water (waves)")]
    [Range(16,256)] public int waterResolution = 128;
    public float waveAmp1 = 0.25f, waveLen1 = 12f, waveSpeed1 = 0.9f, waveDir1Deg = 15f;
    public float waveAmp2 = 0.18f, waveLen2 = 7.5f, waveSpeed2 = 1.4f, waveDir2Deg = -35f;
    public Color waterColor = new Color(0.15f, 0.35f, 0.55f, 0.8f);

    Terrain _terrain;
    TerrainData _data;
    Vector2 _westSpawn, _eastSpawn;

    void Awake()
    {
        // Derive bounds from GameSettings (if present)
        int half = Mathf.Max(16, GameSettings.MapHalfSize);
        worldMin = new Vector2(-half, -half);
        worldMax = new Vector2( half,  half);

        var size = new Vector3(worldMax.x - worldMin.x, maxHeight, worldMax.y - worldMin.y);

        _data = new TerrainData
        {
            heightmapResolution = heightmapRes,
            size = size,
            baseMapResolution = controlTexRes,
            alphamapResolution = controlTexRes
        };

        var go = Terrain.CreateTerrainGameObject(_data);
        go.name = "ProcTerrain";
        go.transform.position = new Vector3(worldMin.x, 0, worldMin.y);
        _terrain = go.GetComponent<Terrain>();
        _terrain.drawInstanced = true;

        // West/East spawn clearing centers (world units)
        float midZ = (worldMin.y + worldMax.y) * 0.5f;
        _westSpawn = new Vector2(worldMin.x + clearingRadius + 20f, midZ);
        _eastSpawn = new Vector2(worldMax.x - clearingRadius - 20f, midZ);

        // Auto-load terrain layers from Resources/Map/Terrains (by keyword)
        LoadTerrainLayersFromResources();

        GenerateHeights();       // noise + passes
        // Irregular, mean-preserving clearing smoothing (NO height offset)
        ApplyIrregularClearing(ref _heights, _westSpawn);
        ApplyIrregularClearing(ref _heights, _eastSpawn);

        // Post-process
        ApplyPitFix(ref _heights);
        for (int i = 0; i < smoothIterations; i++) BoxSmooth(ref _heights, 0.25f);
        LimitSlopes(ref _heights, maxSlopeDeg);

        _data.SetHeights(0, 0, _heights);

        PaintSplatmaps();        // height/slope-driven texture blending
        EnsureTerrainMaterial();
        PlaceWater();            // wave water
    }

    // -------------------- HEIGHT GENERATION --------------------

    float[,] _heights;

    void GenerateHeights()
    {
        int res = _data.heightmapResolution;
        _heights = new float[res, res];

        var prng = new System.Random(seed);
        float ox = prng.Next(0, 100000);
        float oy = prng.Next(0, 100000);

        // Larger landforms
        const float CONT_SCALE = 700f;
        const float HILL_SCALE = 380f;
        const float MTN_SCALE  = 260f;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float u = x / (float)(res - 1);
            float v = y / (float)(res - 1);

            // --- base shapes ---
            float c = Perlin(u * contFreq * CONT_SCALE + ox, v * contFreq * CONT_SCALE + oy);
            c = Mathf.Pow(c, 1.15f);

            float h1 = Perlin(u * hillsFreq * HILL_SCALE + ox*1.3f, v * hillsFreq * HILL_SCALE + oy*1.3f) * 0.20f;

            float n = Perlin(u * mountainsFreq * MTN_SCALE + ox*2f, v * mountainsFreq * MTN_SCALE + oy*2f);
            float ridged = Mathf.Pow(1f - Mathf.Abs(n), 2.2f) * mountainsAmp;

            float edge = Mathf.Min(Mathf.Min(u, 1f-u), Mathf.Min(v, 1f-v));
            float coast = Mathf.SmoothStep(0, 0.24f, edge);

            float h01 = Mathf.Clamp01(c * coast + h1 + ridged * coast * 1.05f);

            // --- passes/chokepoints network (subtract to form valleys) ---
            float lines = PassLines(u, v, passScale, passSharp,  18f, ox*0.7f, oy*0.7f);
            lines = Mathf.Max(lines, PassLines(u, v, passScale*0.9f,  passSharp, -32f, ox*1.1f, oy*0.4f));
            lines = Mathf.Max(lines, PassLines(u, v, passScale*1.15f, passSharp,  58f, ox*0.3f, oy*1.3f));
            h01 = Mathf.Clamp01(h01 - lines * passDepth);

            // Accentuate mountains outside passes
            h01 = Mathf.Clamp01(h01 + ridged * (1f - lines) * 0.10f);

            _heights[y, x] = h01;
        }
    }

    // Narrow line mask from Perlin, rotated by angleDeg
    float PassLines(float u, float v, float scale, float sharp, float angleDeg, float ox, float oy)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cu = Mathf.Cos(rad), su = Mathf.Sin(rad);
        Vector2 uv = new Vector2(u - 0.5f, v - 0.5f);
        float rx = uv.x * cu - uv.y * su;
        float ry = uv.x * su + uv.y * cu;
        Vector2 r = new Vector2(rx + 0.5f, ry + 0.5f);

        float p = Perlin(r.x * scale + ox, r.y * scale + oy); // 0..1
        float ridge = 1f - Mathf.Abs(p * 2f - 1f);            // 0 = trough, 1 = midline
        ridge = Mathf.Pow(ridge, sharp);                      // sharpen into thin “lines”
        return ridge;
    }

    // -------------------- IRREGULAR CLEARING (MEAN-PRESERVING) --------------------

    void ApplyIrregularClearing(ref float[,] H, Vector2 centerWorld)
    {
        int res = H.GetLength(0);
        // Build a noisy, elliptical mask in [0..1] over the heightmap
        float[,] mask = BuildIrregularClearingMask(centerWorld);

        // Record mean height inside mask BEFORE smoothing
        double sumBefore = 0.0;
        double wBefore = 0.0;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float m = mask[y, x];
            if (m > 0f) { sumBefore += H[y, x] * m; wBefore += m; }
        }
        float meanBefore = (wBefore > 0) ? (float)(sumBefore / wBefore) : 0f;

        // Masked smoothing (does not change heights outside the mask)
        MaskedSmooth(ref H, mask, clearingKernelRadius, clearingSmoothIterations, 0.85f);

        // Record mean AFTER and shift back to preserve mean (no plateau/offset)
        double sumAfter = 0.0;
        double wAfter = 0.0;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float m = mask[y, x];
            if (m > 0f) { sumAfter += H[y, x] * m; wAfter += m; }
        }
        if (wAfter > 0.0)
        {
            float meanAfter = (float)(sumAfter / wAfter);
            float delta = meanBefore - meanAfter;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float m = mask[y, x];
                if (m > 0f) H[y, x] = Mathf.Clamp01(H[y, x] + delta * m); // shift proportionally to mask falloff
            }
        }
    }

    float[,] BuildIrregularClearingMask(Vector2 centerWorld)
    {
        int res = _heights.GetLength(0);

        float sx = _data.size.x, sz = _data.size.z;
        float wx0 = worldMin.x,   wz0 = worldMin.y;

        float[,] M = new float[res, res];

        // Deterministic per-clearing randoms from seed + position
        uint h = (uint)(seed) ^ (FloatHash(centerWorld.x) * 374761393u) ^ (FloatHash(centerWorld.y) * 668265263u);
        RandomInit(h, out float rnd0, out float rnd1, out float rnd2);

        // Ellipse params (aspect & rotation)
        float aspect = Mathf.Lerp(0.75f, 1.35f, rnd0);               // 0.75..1.35
        float angle  = Mathf.Lerp(0f, Mathf.PI * 2f, rnd1);          // 0..2π
        float cosA = Mathf.Cos(angle), sinA = Mathf.Sin(angle);
        float ax = aspect, az = 1f / Mathf.Max(0.6f, aspect);        // keep area roughly stable

        // Noise phase offsets
        float nOx = rnd2 * 1000f + 137.2f;
        float nOy = rnd1 * 1000f - 42.7f;

        float rBase = Mathf.Max(6f, clearingRadius);
        float feather = Mathf.Max(2f, clearingFeather);
        float jitterFrac = Mathf.Clamp01(clearingNoiseAmp);

        for (int y = 0; y < res; y++)
        {
            float wz = wz0 + (sz * y) / (res - 1);
            for (int x = 0; x < res; x++)
            {
                float wx = wx0 + (sx * x) / (res - 1);

                // local coords
                float dx = wx - centerWorld.x;
                float dz = wz - centerWorld.y;

                // rotate && anisotropy for ellipse
                float rx =  dx * cosA - dz * sinA;
                float rz =  dx * sinA + dz * cosA;
                rx /= ax; rz /= az;

                float d = Mathf.Sqrt(rx * rx + rz * rz); // ~elliptical distance (world units scaled by axis)

                // rim jitter via world-space Perlin
                float n = Perlin((wx * clearingNoiseFreq) + nOx, (wz * clearingNoiseFreq) + nOy); // 0..1
                float jitter = (n * 2f - 1f) * (jitterFrac * rBase); // +/- fraction of radius
                float r = rBase + jitter;

                // Soft mask by distance to the (jittered) ellipse boundary
                float inner = r - feather;
                float outer = r + feather;
                float m = Mathf.InverseLerp(outer, inner, d);
                M[y, x] = Mathf.SmoothStep(0f, 1f, m);
            }
        }
        return M;
    }

    // Weighted masked smoothing with mean bias controlled by mask
    void MaskedSmooth(ref float[,] H, float[,] M, int radius, int iterations, float strength)
    {
        int res = H.GetLength(0);
        radius = Mathf.Clamp(radius, 1, 4);
        iterations = Mathf.Max(1, iterations);
        strength = Mathf.Clamp01(strength);

        float[,] tmp = (float[,])H.Clone();

        for (int it = 0; it < iterations; it++)
        {
            for (int y = 0; y < res; y++)
            {
                int y0 = Mathf.Max(0, y - radius);
                int y1 = Mathf.Min(res - 1, y + radius);
                for (int x = 0; x < res; x++)
                {
                    int x0 = Mathf.Max(0, x - radius);
                    int x1 = Mathf.Min(res - 1, x + radius);

                    float wSum = 0f;
                    float hSum = 0f;

                    for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        // Gaussian-ish falloff by Manhattan distance (cheap)
                        int dx = Mathf.Abs(xx - x);
                        int dy = Mathf.Abs(yy - y);
                        float w = 1f / (1f + dx + dy);
                        float m = M[yy, xx]; // only neighbors inside mask contribute
                        if (m > 0f)
                        {
                            wSum += w;
                            hSum += H[yy, xx] * w;
                        }
                    }

                    if (wSum > 0f)
                    {
                        float avg = hSum / wSum;
                        float mCenter = M[y, x]; // how much to apply here
                        float t = strength * mCenter;
                        tmp[y, x] = Mathf.Lerp(H[y, x], avg, t);
                    }
                    else
                    {
                        tmp[y, x] = H[y, x];
                    }
                }
            }

            // swap
            var swap = H;
            H = tmp;
            tmp = swap;
        }
    }

    // -------------------- POST FILTERS --------------------

    // Raise tiny pits toward neighbor average
    void ApplyPitFix(ref float[,] H)
    {
        if (pitFixStrength <= 0f) return;

        int res = H.GetLength(0);
        float[,] outH = (float[,])H.Clone();

        for (int y = 1; y < res - 1; y++)
        for (int x = 1; x < res - 1; x++)
        {
            float avg =
                (H[y-1,x-1]+H[y-1,x]+H[y-1,x+1]+
                 H[y,  x-1]+           H[y,  x+1]+
                 H[y+1,x-1]+H[y+1,x]+H[y+1,x+1]) / 8f;

            float h = H[y, x];
            if (h + 0.0025f < avg) // tiny pit threshold
            {
                outH[y, x] = Mathf.Lerp(h, avg, pitFixStrength);
            }
        }

        H = outH;
    }

    // Simple 3x3 box smoothing (lerp-to-average)
    void BoxSmooth(ref float[,] H, float lerpStrength)
    {
        int res = H.GetLength(0);
        float[,] outH = (float[,])H.Clone();

        for (int y = 1; y < res - 1; y++)
        for (int x = 1; x < res - 1; x++)
        {
            float avg =
                (H[y-1,x-1]+H[y-1,x]+H[y-1,x+1]+
                 H[y,  x-1]+H[y,  x]+H[y,  x+1]+
                 H[y+1,x-1]+H[y+1,x]+H[y+1,x+1]) / 9f;

            outH[y, x] = Mathf.Lerp(H[y, x], avg, lerpStrength);
        }

        H = outH;
    }

    // Cap local slope by clamping height deltas to match maxSlopeDeg
    void LimitSlopes(ref float[,] H, float maxSlopeDeg)
    {
        int res = H.GetLength(0);
        float dxWorld = _data.size.x / (res - 1);
        float maxDeltaNorm = Mathf.Tan(maxSlopeDeg * Mathf.Deg2Rad) * (dxWorld / _data.size.y);

        float[,] outH = (float[,])H.Clone();

        for (int y = 1; y < res - 1; y++)
        for (int x = 1; x < res - 1; x++)
        {
            float h = H[y, x];

            float sum = 0f; int cnt = 0;

            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                if (i == 0 && j == 0) continue;
                float hn = H[y + j, x + i];
                float delta = hn - h;

                float scale = (i != 0 && j != 0) ? 1.41421356f : 1f;
                float maxD = maxDeltaNorm * scale;

                if (delta >  maxD) delta =  maxD;
                if (delta < -maxD) delta = -maxD;

                sum += h + delta;
                cnt++;
            }

            float target = sum / cnt;
            outH[y, x] = Mathf.Lerp(h, target, 0.6f);
        }

        H = outH;
    }

    // -------------------- TEXTURE PAINTING --------------------

    void PaintSplatmaps()
    {
        var layersArr = BuildLayerArray();
        _data.terrainLayers = layersArr;

        int res = _data.alphamapResolution;
        int L = layersArr.Length;
        if (L == 0) return;

        float[,,] splat = new float[res, res, L];

        int iSand  = IndexOf(layersArr, sand);
        int iGrass = IndexOf(layersArr, grass);
        int iDirt  = IndexOf(layersArr, dirt);
        int iRock  = IndexOf(layersArr, rock);
        int iSnow  = IndexOf(layersArr, snow);
        int iCliff = IndexOf(layersArr, cliff);

        float tanStart = Mathf.Tan(cliffStartDeg * Mathf.Deg2Rad);
        float tanFull  = Mathf.Tan(cliffFullDeg  * Mathf.Deg2Rad);

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float u = x / (float)(res - 1);
            float v = y / (float)(res - 1);

            float h01 = _data.GetInterpolatedHeight(u, v) / _data.size.y;

            Vector3 nrm = _data.GetInterpolatedNormal(u, v);
            float cosTheta = Mathf.Clamp01(nrm.y);
            float theta = Mathf.Acos(cosTheta);
            float tanSlope = Mathf.Tan(theta);

            float wCliff = Mathf.InverseLerp(tanStart, tanFull, tanSlope);
            wCliff = Mathf.SmoothStep(0f, 1f, wCliff);

            float temp  = Perlin(u * tempFreq * 1000, v * tempFreq * 1000);
            float moist = Perlin(u * moistFreq * 1000 + 200, v * moistFreq * 1000 + 200);

            float wSand  = Mathf.SmoothStep(hBeachMax + 0.02f, seaLevel - 0.06f, h01);
            float wGrass = Band(h01, seaLevel + 0.03f, hLowMax, 0.10f);
            float wDirt  = Band(h01, 0.46f,            hMidMax, 0.12f);
            float wRock  = Band(h01, 0.60f,            hHighMax,0.14f);
            float wSnow  = Mathf.SmoothStep(0.78f, 0.92f, h01) * Mathf.SmoothStep(0.85f, 0.45f, 1f - wCliff);

            wGrass *= Mathf.Lerp(0.75f, 1.15f, moist);
            wDirt  *= Mathf.Lerp(1.15f, 0.85f, moist);
            wRock  *= Mathf.Lerp(0.9f,  1.1f,  1f - moist);

            float nonCliff = 1f - wCliff;
            wSand  *= nonCliff;
            wGrass *= nonCliff;
            wDirt  *= nonCliff;
            wRock  *= nonCliff;
            wSnow  *= nonCliff;

            float sum = 1e-6f;
            if (iSand  >= 0) sum += wSand;
            if (iGrass >= 0) sum += wGrass;
            if (iDirt  >= 0) sum += wDirt;
            if (iRock  >= 0) sum += wRock;
            if (iSnow  >= 0) sum += wSnow;
            if (iCliff >= 0) sum += wCliff;

            if (iSand  >= 0)  splat[y, x, iSand]  = wSand  / sum;
            if (iGrass >= 0)  splat[y, x, iGrass] = wGrass / sum;
            if (iDirt  >= 0)  splat[y, x, iDirt]  = wDirt  / sum;
            if (iRock  >= 0)  splat[y, x, iRock]  = wRock  / sum;
            if (iSnow  >= 0)  splat[y, x, iSnow]  = wSnow  / sum;
            if (iCliff >= 0)  splat[y, x, iCliff] = wCliff / sum;
        }

        _data.SetAlphamaps(0, 0, splat);
    }

    static float Band(float h, float a, float b, float feather)
    {
        float lo = Mathf.Min(a, b);
        float hi = Mathf.Max(a, b);
        float w  = Mathf.SmoothStep(lo - feather, lo + feather, h) *
                   Mathf.SmoothStep(hi + feather, hi - feather, h);
        return Mathf.Clamp01(w);
    }

    // -------------------- TERRAIN MATERIAL --------------------

    void EnsureTerrainMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (sh == null) sh = Shader.Find("Nature/Terrain/Standard");
        if (sh == null) return;

        if (_terrain.materialTemplate == null || _terrain.materialTemplate.shader != sh)
            _terrain.materialTemplate = new Material(sh);

        _terrain.drawInstanced = true;
        _terrain.Flush();
    }

    // -------------------- WATER (waves) --------------------

    void PlaceWater()
    {
        var go = new GameObject("Water");
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", waterColor);
        if (mat.HasProperty("_Color"))     mat.color = waterColor;
        if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface", 1f); // transparent
        mr.sharedMaterial = mat;

        Mesh mesh = BuildWaterGrid();
        mf.sharedMesh = mesh;

        var waves = go.AddComponent<SimpleWaterWaves>();
        waves.Initialize(mesh,
            worldMin, worldMax, _data.size.y * seaLevel,
            waterResolution,
            waveAmp1, waveLen1, waveSpeed1, waveDir1Deg,
            waveAmp2, waveLen2, waveSpeed2, waveDir2Deg);

        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
    }

    Mesh BuildWaterGrid()
    {
        int n = Mathf.Clamp(waterResolution, 16, 256);
        int vertsX = n + 1, vertsZ = n + 1;

        float sx = worldMax.x - worldMin.x;
        float sz = worldMax.y - worldMin.y;

        var verts = new Vector3[vertsX * vertsZ];
        var uvs   = new Vector2[verts.Length];
        var tris  = new int[n * n * 6];

        for (int z = 0, i = 0; z < vertsZ; z++)
        {
            float vz = (float)z / n;
            for (int x = 0; x < vertsX; x++, i++)
            {
                float vx = (float)x / n;
                float wx = worldMin.x + vx * sx;
                float wz = worldMin.y + vz * sz;
                verts[i] = new Vector3(wx, _data.size.y * seaLevel, wz);
                uvs[i]   = new Vector2(vx, vz);
            }
        }

        for (int z = 0, t = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i0 =  z      * vertsX + x;
            int i1 =  z      * vertsX + x + 1;
            int i2 = (z + 1) * vertsX + x;
            int i3 = (z + 1) * vertsX + x + 1;
            tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
            tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
        }

        var mesh = new Mesh { indexFormat = (verts.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16) };
        mesh.name = "WaterGrid";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // -------------------- LAYERS LOADING --------------------

    void LoadTerrainLayersFromResources()
    {
        var loaded = Resources.LoadAll<TerrainLayer>("Map/Terrains");
        if (loaded != null && loaded.Length > 0)
        {
            foreach (var tl in loaded)
            {
                var name = tl != null ? tl.name.ToLowerInvariant() : "";
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Contains("cliff") || name.Contains("steep") || name.Contains("rockwall")) { cliff = tl; continue; }
                if (name.Contains("sand")  || name.Contains("beach"))                               { sand  = tl; continue; }
                if (name.Contains("grass"))                                                        { grass = tl; continue; }
                if (name.Contains("dirt")  || name.Contains("soil") || name.Contains("mud"))      { dirt  = tl; continue; }
                if (name.Contains("rock")  || name.Contains("stone"))                              { rock  = tl; continue; }
                if (name.Contains("snow")  || name.Contains("ice"))                                { snow  = tl; continue; }
            }
        }
    }

    TerrainLayer[] BuildLayerArray()
    {
        var list = new List<TerrainLayer>(6);
        if (Has(sand))  list.Add(sand);
        if (Has(grass)) list.Add(grass);
        if (Has(dirt))  list.Add(dirt);
        if (Has(rock))  list.Add(rock);
        if (Has(snow))  list.Add(snow);
        if (Has(cliff)) list.Add(cliff);
        return list.ToArray();
    }

    static int IndexOf(TerrainLayer[] arr, TerrainLayer layer)
    {
        if (layer == null || arr == null) return -1;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == layer) return i;
        return -1;
    }

    static bool Has(Object o) => o != null;

    // Unity's Mathf.PerlinNoise is 2D Perlin [0..1]; we wrap it
    static float Perlin(float x, float y) => Mathf.PerlinNoise(x, y);

    // Tiny helpers for deterministic per-clearing randomness
    static uint FloatHash(float v)
    {
        uint x = (uint)Mathf.FloatToHalf(v);
        x ^= 0x9E3779B9u; x *= 0x85EBCA6Bu; x ^= x >> 13; x *= 0xC2B2AE35u; x ^= x >> 16;
        return x;
    }
    static void RandomInit(uint seed, out float a, out float b, out float c)
    {
        uint x = seed;
        x ^= 0x9E3779B9u; x *= 0x85EBCA6Bu; x ^= x >> 16; a = (x & 0xFFFFFF) / (float)0x1000000;
        x ^= 0xC2B2AE35u; x *= 0x27D4EB2Du; x ^= x >> 15; b = (x & 0xFFFFFF) / (float)0x1000000;
        x ^= 0x165667B1u; x *= 0x85EBCA6Bu; x ^= x >> 13; c = (x & 0xFFFFFF) / (float)0x1000000;
    }
}

/// <summary>
/// Lightweight CPU wave animator for the water mesh.
/// </summary>
class SimpleWaterWaves : MonoBehaviour
{
    Mesh _mesh;
    Vector3[] _base;
    Vector3[] _work;

    float _yLevel;

    float A1, L1, S1, D1;
    float A2, L2, S2, D2;

    public void Initialize(
        Mesh mesh, Vector2 worldMin, Vector2 worldMax, float yLevel, int gridRes,
        float amp1, float len1, float speed1, float dir1Deg,
        float amp2, float len2, float speed2, float dir2Deg)
    {
        _mesh = mesh;
        _base = mesh.vertices;
        _work = new Vector3[_base.Length];

        _yLevel = yLevel;

        A1 = amp1; L1 = Mathf.Max(0.01f, len1); S1 = speed1; D1 = dir1Deg * Mathf.Deg2Rad;
        A2 = amp2; L2 = Mathf.Max(0.01f, len2); S2 = speed2; D2 = dir2Deg * Mathf.Deg2Rad;
    }

    void LateUpdate()
    {
        if (_mesh == null || _base == null) return;

        float t = Time.time;

        Vector2 k1 = new Vector2(Mathf.Cos(D1), Mathf.Sin(D1)) * (2f * Mathf.PI / L1);
        Vector2 k2 = new Vector2(Mathf.Cos(D2), Mathf.Sin(D2)) * (2f * Mathf.PI / L2);

        for (int i = 0; i < _base.Length; i++)
        {
            var v = _base[i];
            Vector2 xz = new Vector2(v.x, v.z);
            float y =
                A1 * Mathf.Sin(Vector2.Dot(k1, xz) + S1 * t) +
                A2 * Mathf.Sin(Vector2.Dot(k2, xz) + S2 * t);

            v.y = _yLevel + y;
            _work[i] = v;
        }

        _mesh.vertices = _work;
        _mesh.RecalculateNormals();
        _mesh.UploadMeshData(false);
    }
}
