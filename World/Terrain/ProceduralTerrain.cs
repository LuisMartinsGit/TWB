// ProceduralTerrain.cs
// AoE-Style Archipelago Map Generator
// - Player-centric islands (one per player, equal size)
// - Neutral expansion islands between players
// - Flat playable areas with coastal slopes
// Location: Assets/Scripts/World/Terrain/ProceduralTerrain.cs

using UnityEngine;
using System.Collections.Generic;

namespace TheWaningBorder.World.Terrain
{
    /// <summary>
    /// Generates AoE-style archipelago maps with fair player placement.
    /// Each player gets their own island of equal size.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ProceduralTerrain : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════
        // WORLD SETTINGS
        // ═══════════════════════════════════════════════════════════════════════

        [Header("World (derived from GameSettings)")]
        public Vector2 worldMin = new(-256, -256);
        public Vector2 worldMax = new(256, 256);
        public int heightmapRes = 1025;
        public int controlTexRes = 1024;
        public float maxHeight = 100f;
        public int seed = 12345;

        [Header("Height Levels (in world units)")]
        [Tooltip("Sea floor depth")]
        public float seaFloorHeight = 0f;
        [Tooltip("Water surface height")]
        public float waterHeight = 20f;
        [Tooltip("Beach/shore height")]
        public float beachHeight = 25f;
        [Tooltip("Flat island plateau height")]
        public float plateauHeight = 28f;
        [Tooltip("Mountain peak height (unused in archipelago mode)")]
        public float mountainPeakHeight = 80f;

        // ═══════════════════════════════════════════════════════════════════════
        // ARCHIPELAGO SETTINGS (AoE-Style)
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Player Islands")]
        [Tooltip("Base radius for player islands (auto-adjusted based on player count)")]
        [Range(60f, 120f)]
        public float playerIslandBaseRadius = 80f;
        
        [Tooltip("How far player islands are from map center (0-1)")]
        [Range(0.4f, 0.7f)]
        public float playerIslandDistance = 0.55f;
        
        [Tooltip("Coastline irregularity for player islands (lower = rounder, fairer)")]
        [Range(0.1f, 0.25f)]
        public float playerIslandNoise = 0.18f;

        [Header("Neutral Islands")]
        [Tooltip("Size of neutral islands relative to player islands")]
        [Range(0.3f, 0.6f)]
        public float neutralIslandScale = 0.45f;
        
        [Tooltip("Coastline irregularity for neutral islands")]
        [Range(0.15f, 0.35f)]
        public float neutralIslandNoise = 0.25f;
        
        [Tooltip("Include a center island (contested area)")]
        public bool includeCenterIsland = true;

        [Header("Terrain Layers (auto-generated if null)")]
        public TerrainLayer sand;
        public TerrainLayer grass;
        public TerrainLayer dirt;
        public TerrainLayer rock;
        public TerrainLayer snow;

        [Header("Texture Settings")]
        public int textureResolution = 512;
        public float textureTiling = 15f;

        // ═══════════════════════════════════════════════════════════════════════
        // RUNTIME DATA
        // ═══════════════════════════════════════════════════════════════════════

        private UnityEngine.Terrain _terrain;
        private TerrainData _data;
        private System.Random _rng;
        private float _noiseOffsetX;
        private float _noiseOffsetY;

        /// <summary>
        /// Island data for spawn system integration.
        /// </summary>
        public struct IslandInfo
        {
            public Vector2 Center;
            public float Radius;
            public bool IsMainland;      // Legacy - kept for compatibility
            public bool IsPlayerIsland;  // True if this is a player starting island
            public int PlayerIndex;      // Which player owns this island (-1 for neutral)
        }

        private List<IslandInfo> _islands = new();
        private float islandSpacing;
        private float islandMinRadius;
        private float islandMaxRadius;

        /// <summary>
        /// Get all generated islands (for spawn positioning).
        /// </summary>
        public IReadOnlyList<IslandInfo> Islands => _islands;

        /// <summary>
        /// Singleton instance for easy access.
        /// </summary>
        public static ProceduralTerrain Instance { get; private set; }

        // ═══════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════

        void Awake()
        {
            Instance = this;

            // Derive bounds from GameSettings
            int half = Mathf.Max(16, GameSettings.MapHalfSize);
            worldMin = new Vector2(-half, -half);
            worldMax = new Vector2(half, half);

            // Use seed from GameSettings if available
            if (GameSettings.SpawnSeed != 0)
                seed = GameSettings.SpawnSeed;

            _rng = new System.Random(seed);
            _noiseOffsetX = _rng.Next(0, 100000);
            _noiseOffsetY = _rng.Next(0, 100000);

            // Scale island parameters to map size (base values are for 512x512 map)
            float mapScale = half / 256f;
            islandSpacing *= mapScale;
            islandMinRadius *= mapScale;
            islandMaxRadius *= mapScale;

            // Create terrain
            var size = new Vector3(worldMax.x - worldMin.x, maxHeight, worldMax.y - worldMin.y);
            
            Debug.Log($"[ProceduralTerrain] Creating terrain: size={size}, heightmapRes={heightmapRes}");

            _data = new TerrainData();
            _data.heightmapResolution = heightmapRes;
            _data.alphamapResolution = controlTexRes;
            _data.baseMapResolution = controlTexRes;
            _data.size = size;  // Set size AFTER resolutions

            var go = UnityEngine.Terrain.CreateTerrainGameObject(_data);
            go.name = "ProcTerrain";
            go.transform.position = new Vector3(worldMin.x, 0, worldMin.y);
            _terrain = go.GetComponent<UnityEngine.Terrain>();
            _terrain.drawInstanced = true;
            
            Debug.Log($"[ProceduralTerrain] Terrain created at {go.transform.position}, data.size={_data.size}");

            // Generate textures if not assigned
            GenerateTerrainLayers();

            // Generate
            GenerateIslands();
            GenerateHeightmap();
            PaintSplatmaps();

            // Create water plane
            CreateWaterPlane();

            Debug.Log($"[ProceduralTerrain] Generated coastal map with {_islands.Count} landmasses (seed: {seed})");
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WATER PLANE CREATION
        // ═══════════════════════════════════════════════════════════════════════

        void CreateWaterPlane()
        {
            // Create water GameObject
            var waterGO = new GameObject("WaterPlane");
            waterGO.transform.SetParent(transform);

            var water = waterGO.AddComponent<WaterPlane>();
            water.waterLevel = waterHeight;
            
            // AoE4-style water settings
            // Flow animation (gentle, drifting)
            water.flowSpeed = 0.06f;
            water.flowStrength = 0.25f;
            
            // Surface detail
            water.rippleScale = 0.05f;
            water.rippleSpeed = 0.4f;
            water.bumpiness = 0.35f;
            
            // Foam settings
            water.foamScale = 0.07f;
            water.foamThreshold = 0.55f;
            water.foamIntensity = 1.2f;
            
            // Specular (subtle for RTS readability)
            water.specularPower = 64f;
            water.specularIntensity = 0.35f;

            // AoE4-style colors (depth-based)
            water.shallowColor = new Color(0.30f, 0.60f, 0.70f, 0.6f);
            water.deepColor = new Color(0.08f, 0.22f, 0.35f, 0.95f);
            water.foamColor = new Color(0.95f, 0.98f, 1f, 0.9f);

            // Initialize with world bounds
            water.Initialize(worldMin, worldMax, waterHeight);
            
            Debug.Log($"[ProceduralTerrain] AoE4-style water plane at {waterHeight} units");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PROCEDURAL TEXTURE GENERATION
        // ═══════════════════════════════════════════════════════════════════════

        void GenerateTerrainLayers()
        {
            float mapSize = worldMax.x - worldMin.x;
            float tileSize = mapSize / textureTiling;

            // Use seed-based offset for texture generation (in case main offsets not set)
            var texRng = new System.Random(seed + 999);
            float texOffsetX = texRng.Next(0, 10000);
            float texOffsetY = texRng.Next(0, 10000);

            if (sand == null)
                sand = CreateTerrainLayer("Sand", GenerateSandTexture(texOffsetX, texOffsetY), tileSize);
            
            if (grass == null)
                grass = CreateTerrainLayer("Grass", GenerateGrassTexture(texOffsetX, texOffsetY), tileSize);
            
            if (dirt == null)
                dirt = CreateTerrainLayer("Dirt", GenerateDirtTexture(texOffsetX, texOffsetY), tileSize);
            
            if (rock == null)
                rock = CreateTerrainLayer("Rock", GenerateRockTexture(texOffsetX, texOffsetY), tileSize);
            
            if (snow == null)
                snow = CreateTerrainLayer("Snow", GenerateSnowTexture(texOffsetX, texOffsetY), tileSize);

            Debug.Log("[ProceduralTerrain] Generated terrain layer textures");
        }

        TerrainLayer CreateTerrainLayer(string name, Texture2D diffuse, float tileSize)
        {
            // Generate normal map from diffuse
            var normalMap = GenerateNormalMap(diffuse);

            var layer = new TerrainLayer
            {
                diffuseTexture = diffuse,
                normalMapTexture = normalMap,
                tileSize = new Vector2(tileSize, tileSize),
                tileOffset = Vector2.zero,
                normalScale = 1.0f
            };
            return layer;
        }

        Texture2D GenerateNormalMap(Texture2D source)
        {
            int width = source.width;
            int height = source.height;
            var normalMap = new Texture2D(width, height, TextureFormat.RGB24, true);
            
            float strength = 2.0f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Sample heights using grayscale
                    float left = source.GetPixel((x - 1 + width) % width, y).grayscale;
                    float right = source.GetPixel((x + 1) % width, y).grayscale;
                    float up = source.GetPixel(x, (y + 1) % height).grayscale;
                    float down = source.GetPixel(x, (y - 1 + height) % height).grayscale;

                    // Calculate normal
                    float dx = (left - right) * strength;
                    float dy = (down - up) * strength;
                    
                    Vector3 normal = new Vector3(dx, dy, 1.0f).normalized;
                    
                    // Convert to color (0-1 range)
                    Color c = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f
                    );
                    normalMap.SetPixel(x, y, c);
                }
            }

            normalMap.Apply();
            normalMap.wrapMode = TextureWrapMode.Repeat;
            normalMap.filterMode = FilterMode.Bilinear;
            return normalMap;
        }

        Texture2D GenerateSandTexture(float offsetX, float offsetY)
        {
            var tex = new Texture2D(textureResolution, textureResolution, TextureFormat.RGB24, true);
            
            // Realistic beach sand colors
            var baseSand = new Color(0.82f, 0.75f, 0.58f);
            var wetSand = new Color(0.68f, 0.60f, 0.45f);
            var lightSand = new Color(0.90f, 0.85f, 0.68f);
            var shellBits = new Color(0.95f, 0.92f, 0.85f);
            var darkGrain = new Color(0.65f, 0.58f, 0.42f);

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    float u = x / (float)textureResolution;
                    float v = y / (float)textureResolution;

                    // Multi-octave noise for sand grain variation
                    float noise = Mathf.PerlinNoise(u * 50 + offsetX, v * 50 + offsetY) * 0.4f;
                    noise += Mathf.PerlinNoise(u * 100 + offsetX, v * 100 + offsetY) * 0.35f;
                    noise += Mathf.PerlinNoise(u * 200 + offsetX, v * 200 + offsetY) * 0.25f;

                    Color c = Color.Lerp(wetSand, lightSand, noise);

                    // Subtle ripple patterns (wind-formed)
                    float ripple = Mathf.Sin(u * 30 + Mathf.PerlinNoise(v * 8, offsetX) * 4f);
                    ripple = ripple * 0.5f + 0.5f;
                    ripple = Mathf.Pow(ripple, 3) * 0.08f;
                    c = Color.Lerp(c, lightSand, ripple);

                    // Occasional darker grains
                    float darkGrains = Mathf.PerlinNoise(u * 300 + offsetY, v * 300 + offsetX);
                    if (darkGrains > 0.78f)
                    {
                        c = Color.Lerp(c, darkGrain, (darkGrains - 0.78f) * 2f);
                    }
                    
                    // Small shell/white bits
                    float shells = Mathf.PerlinNoise(u * 250 + offsetX * 2, v * 250 + offsetY * 2);
                    if (shells > 0.88f)
                    {
                        c = Color.Lerp(c, shellBits, (shells - 0.88f) * 4f);
                    }

                    c = Color.Lerp(baseSand, c, 0.7f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        Texture2D GenerateGrassTexture(float offsetX, float offsetY)
        {
            var tex = new Texture2D(textureResolution, textureResolution, TextureFormat.RGB24, true);
            
            // Realistic grass colors - green with yellow/brown variation
            var baseGreen = new Color(0.35f, 0.55f, 0.18f);
            var darkGreen = new Color(0.22f, 0.42f, 0.12f);
            var yellowGreen = new Color(0.55f, 0.62f, 0.22f);
            var dryYellow = new Color(0.65f, 0.58f, 0.28f);
            var brownPatch = new Color(0.45f, 0.38f, 0.22f);

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    float u = x / (float)textureResolution;
                    float v = y / (float)textureResolution;

                    // Base grass variation
                    float noise = Mathf.PerlinNoise(u * 35 + offsetX, v * 35 + offsetY) * 0.4f;
                    noise += Mathf.PerlinNoise(u * 70 + offsetX, v * 70 + offsetY) * 0.35f;
                    noise += Mathf.PerlinNoise(u * 140 + offsetX, v * 140 + offsetY) * 0.25f;

                    // Grass blade direction streaks
                    float streaks = Mathf.PerlinNoise(u * 8 + offsetY, v * 100 + offsetX);
                    streaks = Mathf.Pow(streaks, 2) * 0.3f;

                    // Start with base green
                    Color c = Color.Lerp(darkGreen, baseGreen, noise);
                    
                    // Add yellow-green patches (healthy sun-exposed grass)
                    float yellowNoise = Mathf.PerlinNoise(u * 12 + offsetX + 100, v * 12 + offsetY + 100);
                    if (yellowNoise > 0.5f)
                    {
                        float yellowAmount = (yellowNoise - 0.5f) * 2f;
                        c = Color.Lerp(c, yellowGreen, yellowAmount * 0.5f);
                    }
                    
                    // Add dry patches
                    float dryNoise = Mathf.PerlinNoise(u * 8 + offsetX + 200, v * 8 + offsetY + 200);
                    if (dryNoise > 0.65f)
                    {
                        float dryAmount = (dryNoise - 0.65f) * 2.5f;
                        c = Color.Lerp(c, dryYellow, dryAmount * 0.4f);
                    }
                    
                    // Add occasional brown/bare patches (for woodland floor feel)
                    float brownNoise = Mathf.PerlinNoise(u * 6 + offsetX + 300, v * 6 + offsetY + 300);
                    if (brownNoise > 0.72f)
                    {
                        float brownAmount = (brownNoise - 0.72f) * 3.5f;
                        c = Color.Lerp(c, brownPatch, brownAmount * 0.5f);
                    }

                    // Apply streaks for grass blade texture
                    c = Color.Lerp(c, yellowGreen, streaks * 0.15f);
                    
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        Texture2D GenerateDirtTexture(float offsetX, float offsetY)
        {
            var tex = new Texture2D(textureResolution, textureResolution, TextureFormat.RGB24, true);
            
            // Woodland floor colors - rich browns with organic variation
            var baseBrown = new Color(0.42f, 0.32f, 0.22f);
            var darkBrown = new Color(0.28f, 0.20f, 0.12f);
            var lightBrown = new Color(0.52f, 0.42f, 0.28f);
            var leafLitter = new Color(0.48f, 0.38f, 0.18f);
            var darkLoam = new Color(0.22f, 0.16f, 0.10f);
            var twigColor = new Color(0.38f, 0.28f, 0.16f);

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    float u = x / (float)textureResolution;
                    float v = y / (float)textureResolution;

                    // Base soil variation
                    float noise = Mathf.PerlinNoise(u * 30 + offsetX, v * 30 + offsetY) * 0.5f;
                    noise += Mathf.PerlinNoise(u * 60 + offsetX, v * 60 + offsetY) * 0.3f;
                    noise += Mathf.PerlinNoise(u * 120 + offsetX, v * 120 + offsetY) * 0.2f;

                    // Start with base brown
                    Color c = Color.Lerp(darkBrown, lightBrown, noise);
                    
                    // Add leaf litter patches
                    float leafNoise = Mathf.PerlinNoise(u * 15 + offsetX + 50, v * 15 + offsetY + 50);
                    if (leafNoise > 0.45f)
                    {
                        float leafAmount = (leafNoise - 0.45f) * 1.8f;
                        c = Color.Lerp(c, leafLitter, leafAmount * 0.4f);
                    }
                    
                    // Add dark rich loam patches
                    float loamNoise = Mathf.PerlinNoise(u * 10 + offsetX + 150, v * 10 + offsetY + 150);
                    if (loamNoise > 0.6f)
                    {
                        float loamAmount = (loamNoise - 0.6f) * 2.5f;
                        c = Color.Lerp(c, darkLoam, loamAmount * 0.5f);
                    }
                    
                    // Add small twig/debris details
                    float debrisNoise = Mathf.PerlinNoise(u * 80 + offsetY, v * 80 + offsetX);
                    if (debrisNoise > 0.75f)
                    {
                        c = Color.Lerp(c, twigColor, (debrisNoise - 0.75f) * 2f);
                    }
                    
                    // Small pebble highlights
                    float pebbles = Mathf.PerlinNoise(u * 150 + offsetY * 2, v * 150 + offsetX * 2);
                    if (pebbles > 0.82f)
                    {
                        c = Color.Lerp(c, lightBrown * 1.2f, (pebbles - 0.82f) * 3f);
                    }

                    c = Color.Lerp(baseBrown, c, 0.85f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        Texture2D GenerateRockTexture(float offsetX, float offsetY)
        {
            var tex = new Texture2D(textureResolution, textureResolution, TextureFormat.RGB24, true);
            
            // Realistic rock colors
            var baseGray = new Color(0.45f, 0.43f, 0.40f);
            var darkGray = new Color(0.28f, 0.26f, 0.24f);
            var lightGray = new Color(0.62f, 0.60f, 0.55f);
            var warmGray = new Color(0.50f, 0.45f, 0.38f);
            var coolGray = new Color(0.38f, 0.42f, 0.45f);
            var mossTint = new Color(0.35f, 0.42f, 0.32f);

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    float u = x / (float)textureResolution;
                    float v = y / (float)textureResolution;

                    // Base rock variation - large scale
                    float largeNoise = Mathf.PerlinNoise(u * 8 + offsetX, v * 8 + offsetY);
                    
                    // Medium detail
                    float medNoise = Mathf.PerlinNoise(u * 25 + offsetX, v * 25 + offsetY) * 0.4f;
                    medNoise += Mathf.PerlinNoise(u * 50 + offsetX, v * 50 + offsetY) * 0.35f;
                    medNoise += Mathf.PerlinNoise(u * 100 + offsetX, v * 100 + offsetY) * 0.25f;

                    // Cracks and crevices - multiple directions
                    float crack1 = Mathf.PerlinNoise(u * 60 + offsetY * 2, v * 15 + offsetX);
                    crack1 = 1f - Mathf.Abs(crack1 * 2f - 1f);
                    crack1 = Mathf.Pow(crack1, 8f);
                    
                    float crack2 = Mathf.PerlinNoise(u * 20 + offsetX * 2, v * 55 + offsetY);
                    crack2 = 1f - Mathf.Abs(crack2 * 2f - 1f);
                    crack2 = Mathf.Pow(crack2, 8f);
                    
                    float cracks = Mathf.Max(crack1, crack2);

                    // Start with base color variation
                    Color c = Color.Lerp(darkGray, lightGray, medNoise);
                    
                    // Add warm/cool variation based on large noise
                    c = Color.Lerp(c, warmGray, largeNoise * 0.3f);
                    c = Color.Lerp(c, coolGray, (1f - largeNoise) * 0.2f);
                    
                    // Apply cracks (darken)
                    c = Color.Lerp(c, darkGray * 0.6f, cracks * 0.7f);
                    
                    // Occasional moss in crevices
                    float mossNoise = Mathf.PerlinNoise(u * 12 + offsetX + 200, v * 12 + offsetY + 200);
                    if (mossNoise > 0.6f && medNoise < 0.4f)
                    {
                        float mossAmount = (mossNoise - 0.6f) * 2f * (0.4f - medNoise) * 2f;
                        c = Color.Lerp(c, mossTint, mossAmount * 0.4f);
                    }
                    
                    // Surface highlights
                    float highlight = Mathf.PerlinNoise(u * 150 + offsetY, v * 150 + offsetX);
                    if (highlight > 0.8f)
                    {
                        c = Color.Lerp(c, lightGray * 1.15f, (highlight - 0.8f) * 2f);
                    }

                    c = Color.Lerp(baseGray, c, 0.85f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        Texture2D GenerateSnowTexture(float offsetX, float offsetY)
        {
            var tex = new Texture2D(textureResolution, textureResolution, TextureFormat.RGB24, true);
            var baseColor = new Color(0.92f, 0.95f, 0.98f);
            var shadowColor = new Color(0.78f, 0.85f, 0.95f);
            var brightColor = new Color(0.98f, 0.99f, 1.0f);
            var blueTint = new Color(0.85f, 0.90f, 0.98f);

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    float u = x / (float)textureResolution;
                    float v = y / (float)textureResolution;

                    // Soft snow drifts
                    float noise = Mathf.PerlinNoise(u * 20 + offsetX, v * 20 + offsetY) * 0.4f;
                    noise += Mathf.PerlinNoise(u * 45 + offsetX, v * 45 + offsetY) * 0.35f;
                    noise += Mathf.PerlinNoise(u * 100 + offsetX, v * 100 + offsetY) * 0.25f;

                    // Sparkle effect
                    float sparkle = Mathf.PerlinNoise(u * 300 + offsetY, v * 300 + offsetX);
                    sparkle = sparkle > 0.85f ? (sparkle - 0.85f) * 6f : 0f;

                    // Wind-blown patterns
                    float wind = Mathf.PerlinNoise(u * 8 + offsetX, v * 30 + offsetY);
                    wind = Mathf.Pow(wind, 2) * 0.15f;

                    Color c = Color.Lerp(shadowColor, brightColor, noise);
                    c = Color.Lerp(c, blueTint, wind);
                    c = Color.Lerp(c, brightColor, sparkle);
                    c = Color.Lerp(baseColor, c, 0.6f);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ISLAND GENERATION (AoE-Style Player-Centric)
        // ═══════════════════════════════════════════════════════════════════════

        void GenerateIslands()
        {
            _islands.Clear();
            float mapSize = worldMax.x - worldMin.x;
            float mapHalf = mapSize * 0.5f;

            // Get player count (default to 2 if not set)
            int playerCount = Mathf.Max(2, GameSettings.TotalPlayers);
            
            Debug.Log($"[ProceduralTerrain] Generating AoE-style archipelago for {playerCount} players");

            // === STEP 1: Calculate player island placement ===
            // Players placed in equal angular sectors around the map
            float playerDistance = mapHalf * playerIslandDistance;
            float playerRadius = playerIslandBaseRadius;
            
            // Adjust for player count - more players = smaller islands
            if (playerCount > 4)
            {
                playerRadius *= 0.85f;
                playerDistance = mapHalf * (playerIslandDistance - 0.05f);
            }
            if (playerCount > 6)
            {
                playerRadius *= 0.85f;
                playerDistance = mapHalf * (playerIslandDistance - 0.08f);
            }

            // === STEP 2: Create player islands (equal size, fair spacing) ===
            float angleStep = 360f / playerCount;
            float startAngle = _rng.Next(0, 360); // Random rotation for variety
            
            for (int i = 0; i < playerCount; i++)
            {
                float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                Vector2 center = new Vector2(
                    Mathf.Cos(angle) * playerDistance,
                    Mathf.Sin(angle) * playerDistance
                );
                
                _islands.Add(new IslandInfo
                {
                    Center = center,
                    Radius = playerRadius,
                    IsMainland = false,
                    IsPlayerIsland = true,
                    PlayerIndex = i
                });
                
                Debug.Log($"[ProceduralTerrain] Player {i + 1} island at {center}, radius {playerRadius:F0}");
            }

            // === STEP 3: Create neutral expansion islands ===
            float neutralRadius = playerRadius * neutralIslandScale;
            
            // Center island (contested/late game)
            if (includeCenterIsland && playerCount >= 3)
            {
                _islands.Add(new IslandInfo
                {
                    Center = Vector2.zero,
                    Radius = neutralRadius * 1.3f,
                    IsMainland = false,
                    IsPlayerIsland = false,
                    PlayerIndex = -1
                });
            }
            
            // Islands between players (expansion targets)
            for (int i = 0; i < playerCount; i++)
            {
                float angle1 = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                float angle2 = (startAngle + (i + 1) * angleStep) * Mathf.Deg2Rad;
                float midAngle = (angle1 + angle2) * 0.5f;
                
                // Place neutral island at mid-angle, closer to center
                float neutralDist = playerDistance * 0.65f;
                Vector2 center = new Vector2(
                    Mathf.Cos(midAngle) * neutralDist,
                    Mathf.Sin(midAngle) * neutralDist
                );
                
                _islands.Add(new IslandInfo
                {
                    Center = center,
                    Radius = neutralRadius,
                    IsMainland = false,
                    IsPlayerIsland = false,
                    PlayerIndex = -1
                });
            }
            
            // === STEP 4: Small outer islands (fish, exploration) ===
            int outerIslands = playerCount + 2;
            float outerDistance = mapHalf * 0.85f;
            float outerRadius = neutralRadius * 0.5f;
            
            for (int i = 0; i < outerIslands; i++)
            {
                float angle = (startAngle + 180f / playerCount + i * (360f / outerIslands)) * Mathf.Deg2Rad;
                float distVariation = outerDistance + _rng.Next(-15, 15);
                
                Vector2 center = new Vector2(
                    Mathf.Cos(angle) * distVariation,
                    Mathf.Sin(angle) * distVariation
                );
                
                // Check minimum distance from other islands
                bool tooClose = false;
                foreach (var island in _islands)
                {
                    if (Vector2.Distance(center, island.Center) < island.Radius + outerRadius + 15f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                {
                    _islands.Add(new IslandInfo
                    {
                        Center = center,
                        Radius = outerRadius + _rng.Next(-5, 8),
                        IsMainland = false,
                        IsPlayerIsland = false,
                        PlayerIndex = -1
                    });
                }
            }

            Debug.Log($"[ProceduralTerrain] Created {_islands.Count} islands ({playerCount} player + {_islands.Count - playerCount} neutral)");
        }

        /// <summary>
        /// Get the island assigned to a specific player.
        /// </summary>
        public IslandInfo? GetPlayerIsland(int playerIndex)
        {
            foreach (var island in _islands)
            {
                if (island.IsPlayerIsland && island.PlayerIndex == playerIndex)
                    return island;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HEIGHTMAP GENERATION (AoE-Style: Flat islands with coastal slopes)
        // ═══════════════════════════════════════════════════════════════════════

        void GenerateHeightmap()
        {
            int res = _data.heightmapResolution;
            float[,] heights = new float[res, res];

            // Convert unit heights to normalized (0-1) for Unity terrain
            float seaFloorNorm = seaFloorHeight / maxHeight;
            float beachNorm = beachHeight / maxHeight;
            float plateauNorm = plateauHeight / maxHeight;

            Debug.Log($"[ProceduralTerrain] Generating AoE-style heightmap: flat islands with coastal slopes");

            float mapSizeX = worldMax.x - worldMin.x;
            float mapSizeZ = worldMax.y - worldMin.y;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)y / (res - 1);

                    // Convert to world position
                    float worldX = worldMin.x + u * mapSizeX;
                    float worldZ = worldMin.y + v * mapSizeZ;
                    Vector2 worldPos = new Vector2(worldX, worldZ);

                    // Find closest island and calculate land influence
                    float landMask = 0f;
                    
                    foreach (var island in _islands)
                    {
                        float dist = Vector2.Distance(worldPos, island.Center);
                        float baseRadius = island.Radius;
                        
                        // Add organic coastline noise
                        float angle = Mathf.Atan2(worldPos.y - island.Center.y, worldPos.x - island.Center.x);
                        
                        // Multi-octave noise for natural coastline
                        float coastNoise = 0f;
                        coastNoise += Mathf.PerlinNoise(angle * 2f + _noiseOffsetX + island.Center.x * 0.02f, _noiseOffsetY) * 0.4f;
                        coastNoise += Mathf.PerlinNoise(angle * 5f + _noiseOffsetX * 2f, _noiseOffsetY * 2f) * 0.35f;
                        coastNoise += Mathf.PerlinNoise(angle * 10f + _noiseOffsetX * 3f, _noiseOffsetY * 3f) * 0.25f;
                        coastNoise = (coastNoise - 0.5f) * 2f; // -1 to 1
                        
                        // Player islands have less noise (more consistent/fair shape)
                        float noiseAmount = island.IsPlayerIsland ? playerIslandNoise : neutralIslandNoise;
                        float adjustedRadius = baseRadius * (1f + coastNoise * noiseAmount);
                        
                        // Wide coastal slope zone (40% of radius)
                        float slopeWidth = adjustedRadius * 0.4f;
                        float innerRadius = adjustedRadius - slopeWidth;
                        
                        float mask;
                        if (dist < innerRadius)
                        {
                            // Fully on land - flat interior
                            mask = 1f;
                        }
                        else if (dist < adjustedRadius)
                        {
                            // Coastal slope - gradual transition
                            float t = (adjustedRadius - dist) / slopeWidth;
                            t = t * t * (3f - 2f * t); // Smoothstep
                            mask = t;
                        }
                        else
                        {
                            // Underwater slope continues slightly
                            float underwaterDist = dist - adjustedRadius;
                            float underwaterSlope = slopeWidth * 0.3f;
                            if (underwaterDist < underwaterSlope)
                            {
                                mask = (1f - underwaterDist / underwaterSlope) * 0.2f;
                                mask = mask * mask * (3f - 2f * mask);
                            }
                            else
                            {
                                mask = 0f;
                            }
                        }
                        
                        landMask = Mathf.Max(landMask, mask);
                    }

                    // Calculate height - SIMPLE zones, FLAT playable areas
                    float height;

                    if (landMask < 0.01f)
                    {
                        // Deep ocean - flat sea floor
                        height = seaFloorNorm;
                    }
                    else if (landMask < 0.3f)
                    {
                        // Underwater slope to beach
                        float t = Mathf.InverseLerp(0.01f, 0.3f, landMask);
                        t = t * t * (3f - 2f * t); // Smoothstep
                        height = Mathf.Lerp(seaFloorNorm, beachNorm, t);
                    }
                    else if (landMask < 0.5f)
                    {
                        // Beach to plateau - gentle rise
                        float t = Mathf.InverseLerp(0.3f, 0.5f, landMask);
                        t = t * t * (3f - 2f * t); // Smoothstep
                        height = Mathf.Lerp(beachNorm, plateauNorm, t);
                    }
                    else
                    {
                        // Island interior - FLAT with very subtle variation
                        float subtleVariation = Mathf.PerlinNoise(u * 8f + _noiseOffsetX, v * 8f + _noiseOffsetY);
                        subtleVariation = (subtleVariation - 0.5f) * 0.008f; // Very subtle: ±0.4%
                        height = plateauNorm + subtleVariation;
                    }

                    heights[y, x] = Mathf.Clamp01(height);
                }
            }

            _data.SetHeights(0, 0, heights);
            
            Debug.Log($"[ProceduralTerrain] Heightmap complete - flat islands with coastal slopes");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SPLATMAP PAINTING
        // ═══════════════════════════════════════════════════════════════════════

        void PaintSplatmaps()
        {
            var layers = BuildLayerArray();
            _data.terrainLayers = layers;

            int res = _data.alphamapResolution;
            int layerCount = layers.Length;

            if (layerCount == 0) return;

            float[,,] splat = new float[res, res, layerCount];

            int iSand = IndexOf(layers, sand);
            int iGrass = IndexOf(layers, grass);
            int iDirt = IndexOf(layers, dirt);
            int iRock = IndexOf(layers, rock);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)y / (res - 1);

                    // Get height in world units
                    float heightUnits = _data.GetInterpolatedHeight(u, v);
                    
                    // Get slope
                    Vector3 normal = _data.GetInterpolatedNormal(u, v);
                    float slope = 1f - normal.y;

                    // Noise for natural variation
                    float patchNoise = Mathf.PerlinNoise(u * 20f + _noiseOffsetX, v * 20f + _noiseOffsetY);
                    float woodlandNoise = Mathf.PerlinNoise(u * 8f + _noiseOffsetX * 2f, v * 8f + _noiseOffsetY * 2f);

                    float wSand = 0f, wGrass = 0f, wDirt = 0f, wRock = 0f;

                    // === AoE-STYLE TEXTURE ZONES ===
                    
                    if (heightUnits < waterHeight - 1f)
                    {
                        // Underwater = sand
                        wSand = 1f;
                    }
                    else if (heightUnits < waterHeight + 4f)
                    {
                        // Beach zone
                        float beachT = Mathf.InverseLerp(waterHeight - 1f, waterHeight + 4f, heightUnits);
                        wSand = 1f - beachT * 0.7f;
                        wGrass = beachT * 0.5f;
                        wDirt = beachT * 0.2f;
                    }
                    else
                    {
                        // Island interior - grass with woodland patches
                        if (slope < 0.15f)
                        {
                            // Flat - mostly grass with dirt patches
                            wGrass = 0.75f;
                            
                            // Woodland floor patches
                            if (woodlandNoise > 0.5f)
                            {
                                float woodlandAmount = (woodlandNoise - 0.5f) * 2f;
                                wDirt = woodlandAmount * 0.45f;
                                wGrass -= woodlandAmount * 0.35f;
                            }
                            
                            // Random dirt patches
                            if (patchNoise > 0.7f)
                            {
                                float patchAmount = (patchNoise - 0.7f) * 2f;
                                wDirt += patchAmount * 0.25f;
                                wGrass -= patchAmount * 0.15f;
                            }
                        }
                        else if (slope < 0.35f)
                        {
                            // Mild slope
                            float t = Mathf.InverseLerp(0.15f, 0.35f, slope);
                            wGrass = 0.6f * (1f - t);
                            wDirt = 0.3f + t * 0.3f;
                            wRock = t * 0.3f;
                        }
                        else
                        {
                            // Steep coastal cliffs
                            float t = Mathf.InverseLerp(0.35f, 0.6f, slope);
                            wDirt = 0.3f * (1f - t);
                            wRock = 0.7f + t * 0.3f;
                        }
                    }

                    // Normalize
                    float sum = wSand + wGrass + wDirt + wRock + 0.0001f;
                    
                    if (iSand >= 0) splat[y, x, iSand] = wSand / sum;
                    if (iGrass >= 0) splat[y, x, iGrass] = wGrass / sum;
                    if (iDirt >= 0) splat[y, x, iDirt] = wDirt / sum;
                    if (iRock >= 0) splat[y, x, iRock] = wRock / sum;
                }
            }

            _data.SetAlphamaps(0, 0, splat);
            Debug.Log($"[ProceduralTerrain] Splatmap painted - AoE style");
        }

        TerrainLayer[] BuildLayerArray()
        {
            var list = new List<TerrainLayer>();
            if (sand != null) list.Add(sand);
            if (grass != null) list.Add(grass);
            if (dirt != null) list.Add(dirt);
            if (rock != null) list.Add(rock);
            if (snow != null) list.Add(snow);
            return list.ToArray();
        }

        static int IndexOf(TerrainLayer[] arr, TerrainLayer layer)
        {
            if (layer == null || arr == null) return -1;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == layer) return i;
            return -1;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PUBLIC UTILITY METHODS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if a world position is on land (above sea level).
        /// </summary>
        public bool IsOnLand(Vector3 worldPos)
        {
            if (_terrain == null || _data == null) return true;
            float height = _terrain.SampleHeight(worldPos);
            return height > waterHeight;
        }

        /// <summary>
        /// Check if a world position is in water.
        /// </summary>
        public bool IsInWater(Vector3 worldPos)
        {
            return !IsOnLand(worldPos);
        }

        /// <summary>
        /// Get the nearest island to a world position.
        /// </summary>
        public IslandInfo? GetNearestIsland(Vector3 worldPos)
        {
            if (_islands.Count == 0) return null;

            Vector2 pos2D = new Vector2(worldPos.x, worldPos.z);
            IslandInfo nearest = _islands[0];
            float minDist = float.MaxValue;

            foreach (var island in _islands)
            {
                float dist = Vector2.Distance(pos2D, island.Center);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = island;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get a valid spawn position on a specific island.
        /// </summary>
        public Vector3 GetSpawnPositionOnIsland(int islandIndex)
        {
            if (islandIndex < 0 || islandIndex >= _islands.Count)
                return Vector3.zero;

            var island = _islands[islandIndex];
            
            // Find a position toward the center of the island
            float spawnRadius = island.Radius * 0.5f;
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            
            Vector2 offset = new Vector2(
                Mathf.Cos(angle) * spawnRadius * 0.3f,
                Mathf.Sin(angle) * spawnRadius * 0.3f
            );

            Vector2 spawnPos2D = island.Center + offset;
            float height = TerrainUtility.GetHeight(spawnPos2D.x, spawnPos2D.y);

            return new Vector3(spawnPos2D.x, height, spawnPos2D.y);
        }

        /// <summary>
        /// Get spawn positions - each player spawns on their assigned island.
        /// AoE-style: one player per island, guaranteed fair spacing.
        /// </summary>
        public Vector3[] GetMultiplayerSpawnPositions(int playerCount)
        {
            var positions = new Vector3[playerCount];

            // Each player spawns on their designated island
            for (int i = 0; i < playerCount; i++)
            {
                var playerIsland = GetPlayerIsland(i);
                
                if (playerIsland.HasValue)
                {
                    var island = playerIsland.Value;
                    // Spawn at island center (flat, safe location)
                    float y = TerrainUtility.GetHeight(island.Center.x, island.Center.y);
                    positions[i] = new Vector3(island.Center.x, y, island.Center.y);
                    
                    Debug.Log($"[ProceduralTerrain] Player {i + 1} spawns on island at {island.Center}");
                }
                else
                {
                    // Fallback: distribute around map center
                    float radius = (worldMax.x - worldMin.x) * 0.4f;
                    float angle = i * Mathf.PI * 2f / playerCount;
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    float y = TerrainUtility.GetHeight(x, z);
                    positions[i] = new Vector3(x, y, z);
                    
                    Debug.LogWarning($"[ProceduralTerrain] No island for player {i + 1}, using fallback position");
                }
            }

            return positions;
        }
    }
}