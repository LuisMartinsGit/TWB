using UnityEngine;

// Attach once in the Game scene (runs before players spawn)
[DefaultExecutionOrder(-100)]
public class ProceduralTerrain : MonoBehaviour
{
    [Header("World (must match FogOfWar bounds)")]
    public Vector2 worldMin = new(-125, -125);
    public Vector2 worldMax = new( 125,  125);
    public int heightmapRes = 513;          // power-of-two + 1
    public int controlTexRes = 512;         // splatmap resolution
    public float maxHeight = 40f;           // vertical scale
    public float seaLevel = 0.32f;          // 0..1 of heightmap

    [Header("Noise (continents → details)")]
    public float contFreq = 0.003f;         // big shapes
    public float hillsFreq = 0.015f;        // rolling
    public float mountainsFreq = 0.05f;     // jagged
    public float mountainsAmp = 0.45f;      // how strong the ridges are
    public int seed = 12345;

    [Header("Biomes (temperature/moisture)")]
    public float tempFreq = 0.0025f;
    public float moistFreq = 0.0030f;

    [Header("Terrain Layers (drag 4–6 layers in Inspector)")]
    public TerrainLayer grass;
    public TerrainLayer rock;
    public TerrainLayer snow;
    public TerrainLayer sand;
    public TerrainLayer dirt;

    Terrain _terrain;
    TerrainData _data;

    void Awake()
    {
        // NEW: derive bounds from GameSettings
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

        GenerateHeights();
        PaintSplatmaps();
        //PlaceWaterPlane();
    }

    void GenerateHeights()
    {
        int res = _data.heightmapResolution;
        float[,] h = new float[res, res];

        var prng = new System.Random(seed);
        float ox = prng.Next(0, 100000);
        float oy = prng.Next(0, 100000);

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float u = x / (float)(res - 1);
            float v = y / (float)(res - 1);

            // --- Continents (soft) ---
            float c = Perlin(u * contFreq * 1000 + ox, v * contFreq * 1000 + oy);
            c = Mathf.Pow(c, 1.2f); // concentrate land

            // --- Hills (rolling) ---
            float h1 = Perlin(u * hillsFreq * 1000 + ox*1.3f, v * hillsFreq * 1000 + oy*1.3f) * 0.20f;

            // --- Mountains (ridged: 1-|noise|) ---
            float n = Perlin(u * mountainsFreq * 1000 + ox*2f, v * mountainsFreq * 1000 + oy*2f);
            float ridged = (1f - Mathf.Abs(n));         // sharp peaks
            ridged = Mathf.Pow(ridged, 2.4f) * mountainsAmp;

            // Edge falloff (more ocean near borders)
            float edge = Mathf.Min(Mathf.Min(u, 1f-u), Mathf.Min(v, 1f-v));
            float coast = Mathf.SmoothStep(0, 0.22f, edge); // 0 at edge, 1 inward

            float height01 = Mathf.Clamp01(c * coast + h1 + ridged * coast * 1.1f);
            h[y, x] = height01;
        }

        _data.SetHeights(0, 0, h);
    }
    static int IndexOf(TerrainLayer[] arr, TerrainLayer layer)
    {
        if (layer == null || arr == null) return -1;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == layer) return i;
        return -1;
    }
    void PaintSplatmaps()
    {
        // Build the layer array with only the non-null layers you dragged in
        var layersArr = BuildLayerArray();
        _data.terrainLayers = layersArr;

        int res = _data.alphamapResolution;
        int layers = layersArr.Length;

        // If no layers, skip painting to avoid exceptions
        if (layers == 0)
            return;

        float[,,] splat = new float[res, res, layers];

        // Precompute the index of each named layer within the array (-1 if not present)
        int iSand  = IndexOf(layersArr, sand);
        int iGrass = IndexOf(layersArr, grass);
        int iDirt  = IndexOf(layersArr, dirt);
        int iRock  = IndexOf(layersArr, rock);
        int iSnow  = IndexOf(layersArr, snow);

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float u = x / (float)(res - 1);
            float v = y / (float)(res - 1);

            float h = _data.GetInterpolatedHeight(u, v) / _data.size.y;
            Vector3 nrm = _data.GetInterpolatedNormal(u, v);
            float slope = 1f - nrm.y; // 0 flat → 1 vertical

            // Simple biome axes
            float temp  = Perlin(u * tempFreq * 1000, v * tempFreq * 1000);
            float moist = Perlin(u * moistFreq * 1000 + 200, v * moistFreq * 1000 + 200);

            // Weights (same logic as before)
            float wSand  = Mathf.SmoothStep(0.0f, 0.04f, seaLevel + 0.02f - h);
            float wGrass = Mathf.SmoothStep(0.0f, 0.15f, h - seaLevel) * Mathf.SmoothStep(1f, 0.6f, slope);
            float wRock  = Mathf.SmoothStep(0.35f, 0.8f, slope) * Mathf.SmoothStep(seaLevel + 0.05f, 1f, h);
            float wSnow  = Mathf.SmoothStep(0.72f, 0.9f, h) * Mathf.SmoothStep(0.2f, 0.6f, 1f - slope);
            float wDirt  = Mathf.SmoothStep(0.05f, 0.35f, slope) * Mathf.SmoothStep(0.0f, 0.2f, h - seaLevel) * (1f - moist);

            // Normalize just among the layers that exist
            float sum = 1e-6f;
            if (iSand  >= 0) sum += wSand;
            if (iGrass >= 0) sum += wGrass;
            if (iDirt  >= 0) sum += wDirt;
            if (iRock  >= 0) sum += wRock;
            if (iSnow  >= 0) sum += wSnow;

            if (iSand  >= 0)  splat[y, x, iSand]  = wSand  / sum;
            if (iGrass >= 0)  splat[y, x, iGrass] = wGrass / sum;
            if (iDirt  >= 0)  splat[y, x, iDirt]  = wDirt  / sum;
            if (iRock  >= 0)  splat[y, x, iRock]  = wRock  / sum;
            if (iSnow  >= 0)  splat[y, x, iSnow]  = wSnow  / sum;
        }

        _data.SetAlphamaps(0, 0, splat);
    }
    void EnsureTerrainMaterial()
    {
        // URP Terrain Lit; fall back to built-in if needed
        var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (sh == null) sh = Shader.Find("Nature/Terrain/Standard");
        if (sh == null) return;

        if (_terrain.materialTemplate == null || _terrain.materialTemplate.shader != sh)
            _terrain.materialTemplate = new Material(sh);

        // Instancing is good for perf if supported
        _terrain.drawInstanced = true;

        // Force Unity to rebind layers & control maps now that we changed data/material
        _terrain.Flush();
    }

    void PlaceWaterPlane()
    {
        // Ocean/sea → a simple plane at sea level (use proper water later)
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        var sizeX = _data.size.x;
        var sizeZ = _data.size.z;
        water.transform.localScale = new Vector3(sizeX/10f, 1, sizeZ/10f);
        float y = seaLevel * _data.size.y;
        water.transform.position = new Vector3(worldMin.x + sizeX*0.5f, y, worldMin.y + sizeZ*0.5f);

        var mr = water.GetComponent<MeshRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mr.sharedMaterial = new Material(sh) { color = new Color(0.2f,0.4f,0.65f,0.9f) };
        Destroy(water.GetComponent<Collider>());
    }

    TerrainLayer[] BuildLayerArray()
    {
        var list = new System.Collections.Generic.List<TerrainLayer>();
        if (Has(sand))  list.Add(sand);
        if (Has(grass)) list.Add(grass);
        if (Has(dirt))  list.Add(dirt);
        if (Has(rock))  list.Add(rock);
        if (Has(snow))  list.Add(snow);
        return list.ToArray();
    }

    static void Normalize(ref float a, ref float b, ref float c, ref float d, ref float e)
    {
        float s = a + b + c + d + e + 1e-6f;
        a/=s; b/=s; c/=s; d/=s; e/=s;
    }

    static bool Has(Object o) => o != null;

    // Unity's Mathf.PerlinNoise is 2D Perlin [0..1]; we wrap it
    static float Perlin(float x, float y) => Mathf.PerlinNoise(x, y);
}
