using System;
using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Grid")]
    public Vector2 WorldMin = new Vector2(-12.5f, -12.5f);
    public Vector2 WorldMax = new Vector2( 12.5f,  12.5f);
    public float   CellSize = 0.1f;

    [Header("Visuals (Human Player)")]
    public Faction HumanFaction = Faction.Blue;
    [Tooltip("Material that uses the Unlit/FogOfWar shader.")]
    public Material FogMaterial;
    [Tooltip("Quad or plane that covers the playable area; its material will be set to FogMaterial.")]
    public MeshRenderer FogRenderer;
    [Range(0,1)] public float ExploredAlpha = 0.65f; // explored-but-not-currently-visible
    [Range(0,1)] public float HiddenAlpha   = 0.98f; // never seen

    // Internal
    int _w, _h;
    byte[] _visible;   // [faction][cell], 0/1 current frame
    byte[] _revealed;  // [faction][cell], 0/1 persistent
    Texture2D _tex;    // human overlay

    const int MaxFactions = 8;

    int Idx(int x, int y) => y * _w + x;

    // Map any enum to a safe slice [0..MaxFactions-1]
    int FOfs(Faction f)
    {
        int fi = (int)f;
        if (fi < 0) fi = -fi;
        fi %= MaxFactions;
        return fi * _w * _h;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _w = Mathf.CeilToInt((WorldMax.x - WorldMin.x) / CellSize);
        _h = Mathf.CeilToInt((WorldMax.y - WorldMin.y) / CellSize);
        _visible = new byte[MaxFactions * _w * _h];
        _revealed = new byte[MaxFactions * _w * _h];

        _tex = new Texture2D(_w, _h, TextureFormat.Alpha8, false, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        // Bind material/texture even if FogMaterial was not assigned yet at this moment
        EnsureMaterialBound();

        ClearAll();
        PushHumanTexture();
    }

    public void ForceRebuildGrid(bool clearRevealed = false)
        {
            // Recompute dimensions
            int newW = Mathf.CeilToInt((WorldMax.x - WorldMin.x) / CellSize);
            int newH = Mathf.CeilToInt((WorldMax.y - WorldMin.y) / CellSize);

            if (newW <= 0 || newH <= 0) { Debug.LogWarning("[FoW] Invalid grid size."); return; }

            bool sizeChanged = (newW != _w) || (newH != _h);

            _w = newW;
            _h = newH;

            // Reallocate visibility
            int slice = _w * _h;
            var newVisible  = new byte[MaxFactions * slice];
            byte[] newRevealed;

            if (clearRevealed || _revealed == null || _revealed.Length != MaxFactions * slice || !sizeChanged)
            {
                newRevealed = new byte[MaxFactions * slice];
            }
            else
            {
                // Simple nearest-neighbor copy of revealed (optional upsample/downsample)
                newRevealed = new byte[MaxFactions * slice];
                int oldSlice = _revealed.Length / MaxFactions;
                int oldW = oldSlice / Mathf.Max(1, (_revealed.Length / MaxFactions) / Mathf.Max(1, _h)); // fallback guard
                // If we can't infer oldW properly, just clear
                if (oldW <= 0 || oldSlice % oldW != 0)
                {
                    newRevealed = new byte[MaxFactions * slice];
                }
                else
                {
                    int oldH = oldSlice / oldW;
                    for (int f = 0; f < MaxFactions; f++)
                    {
                        int fOfsNew = f * slice;
                        int fOfsOld = f * oldSlice;
                        for (int y = 0; y < _h; y++)
                        {
                            int oldY = Mathf.Clamp(Mathf.RoundToInt((y / (float)_h) * (oldH - 1)), 0, oldH - 1);
                            for (int x = 0; x < _w; x++)
                            {
                                int oldX = Mathf.Clamp(Mathf.RoundToInt((x / (float)_w) * (oldW - 1)), 0, oldW - 1);
                                newRevealed[fOfsNew + (y * _w + x)] = _revealed[fOfsOld + (oldY * oldW + oldX)];
                            }
                        }
                    }
                }
            }

            _visible  = newVisible;
            _revealed = newRevealed;

            // Recreate texture
            if (_tex == null) _tex = new Texture2D(_w, _h, TextureFormat.Alpha8, false, true);
            else _tex.Reinitialize(_w, _h);
            _tex.filterMode = _tex.filterMode; // keep whatever you chose (Point/Bilinear)
            _tex.wrapMode   = TextureWrapMode.Clamp;

            EnsureMaterialBound();
            PushHumanTexture();
        }

    /// <summary>Ensures FogMaterial, FogRenderer and shader params are bound to _tex.</summary>
    void EnsureMaterialBound()
    {
        // If FogMaterial is still null but renderer exists, use its current material.
        if (FogMaterial == null && FogRenderer != null)
            FogMaterial = FogRenderer.sharedMaterial;

        // If neither exists yet, nothing to bind now.
        if (FogMaterial == null) return;

        // Make sure our texture is set on the material
        if (FogMaterial.mainTexture != _tex)
            FogMaterial.mainTexture = _tex;

        // Keep world-bounds in sync (shader needs these every time bounds may change)
        FogMaterial.SetVector("_WorldMin", new Vector4(WorldMin.x, 0, WorldMin.y, 0));
        FogMaterial.SetVector("_WorldMax", new Vector4(WorldMax.x, 0, WorldMax.y, 0));

        // Ensure the renderer uses this material
        if (FogRenderer != null && FogRenderer.sharedMaterial != FogMaterial)
            FogRenderer.sharedMaterial = FogMaterial;
    }

    public void ClearAll()
    {
        Array.Clear(_visible,  0, _visible.Length);
        // NOTE: revealed persists across frames; do NOT clear here
    }

    /// <summary>Call once per frame before stamping to zero current visibility only.</summary>
    public void BeginFrame()
    {
        Array.Clear(_visible, 0, _visible.Length);
    }

    /// <summary>Stamp a circular LoS for a faction.</summary>
    public void Stamp(Faction f, Vector3 worldPos, float radius)
    {
        int fx = FOfs(f);

        // Convert to grid space
        float gx = (worldPos.x - WorldMin.x) / CellSize;
        float gy = (worldPos.z - WorldMin.y) / CellSize;
        float r  = Mathf.Max(0.01f, radius / CellSize);
        int minx = Mathf.Clamp(Mathf.FloorToInt(gx - r), 0, _w - 1);
        int maxx = Mathf.Clamp(Mathf.CeilToInt (gx + r), 0, _w - 1);
        int miny = Mathf.Clamp(Mathf.FloorToInt(gy - r), 0, _h - 1);
        int maxy = Mathf.Clamp(Mathf.CeilToInt (gy + r), 0, _h - 1);
        float r2 = r * r;

        for (int y = miny; y <= maxy; y++)
        {
            for (int x = minx; x <= maxx; x++)
            {
                float dx = (x + 0.5f) - gx;
                float dy = (y + 0.5f) - gy;
                if (dx*dx + dy*dy <= r2)
                {
                    int i = fx + Idx(x, y);
                    _visible[i]  = 1;
                    _revealed[i] = 1; // persist
                }
            }
        }
    }

    /// <summary>Update the human overlay texture after stamping.</summary>
    public void EndFrameAndBuild()
    {
        // In case the Bootstrap assigned FogMaterial after our Awake, bind now.
        EnsureMaterialBound();
        PushHumanTexture();
    }

    void PushHumanTexture()
    {
        int ofs = FOfs(HumanFaction);

        // Keep texture size coherent with grid (in case settings changed)
        if (_tex.width != _w || _tex.height != _h)
        {
            _tex.Reinitialize(_w, _h);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode   = TextureWrapMode.Clamp;
            EnsureMaterialBound();
        }

        var data = _tex.GetRawTextureData<byte>();
        int required = _w * _h;
        if (data.Length != required)
        {
            _tex.Reinitialize(_w, _h);
            data = _tex.GetRawTextureData<byte>();
            EnsureMaterialBound();
        }

        // Alpha encoding: 0 = fully visible (clear), ExploredAlpha = explored, HiddenAlpha = hidden
        for (int i = 0; i < required; i++)
        {
            byte vis = _visible [ofs + i];
            byte rev = _revealed[ofs + i];

            byte a = 255;
            if (vis == 1) a = 0;
            else if (rev == 1) a = (byte)Mathf.RoundToInt(ExploredAlpha * 255f);
            else a = (byte)Mathf.RoundToInt(HiddenAlpha * 255f);

            data[i] = a;
        }

        _tex.Apply(false, false);
    }

    public bool IsVisible(Faction f, Vector3 worldPos)
    {
        if (!WorldToCell(worldPos, out int x, out int y)) return false;
        return _visible[FOfs(f) + Idx(x, y)] != 0;
    }

    public bool IsRevealed(Faction f, Vector3 worldPos)
    {
        if (!WorldToCell(worldPos, out int x, out int y)) return false;
        return _revealed[FOfs(f) + Idx(x, y)] != 0;
    }

    bool WorldToCell(Vector3 pos, out int x, out int y)
    {
        x = Mathf.FloorToInt((pos.x - WorldMin.x) / CellSize);
        y = Mathf.FloorToInt((pos.z - WorldMin.y) / CellSize);
        return (x >= 0 && x < _w && y >= 0 && y < _h);
    }

    public static void SetupFogOfWar()
    {
        if (FindObjectOfType<FogOfWarManager>() != null) return;

        int half = Mathf.Max(16, GameSettings.MapHalfSize);

        // Root + manager
        var root = new GameObject("FogOfWar");
        var mgr  = root.AddComponent<FogOfWarManager>();
        mgr.WorldMin     = new Vector2(-half, -half);
        mgr.WorldMax     = new Vector2( half,  half);
        mgr.CellSize     = 1f; // keep 1u cells; grid scales with map size
        mgr.HumanFaction = Faction.Blue;

        // Material for FoW (same shader)
        var mat = new Material(Shader.Find("Unlit/FogOfWar"));
        mat.renderQueue = 3000;
        mat.SetVector("_WorldMin", new Vector4(mgr.WorldMin.x, 0, mgr.WorldMin.y, 0));
        mat.SetVector("_WorldMax", new Vector4(mgr.WorldMax.x, 0, mgr.WorldMax.y, 0));

        // Build the surface: conforming if Terrain exists; else temporary flat
        GameObject fogSurface = FogOfWarConformingMesh.Create(mgr.WorldMin, mgr.WorldMax, 128, mat);
        fogSurface.name = "FogSurface";
        fogSurface.transform.SetParent(root.transform, false);

        var mr = fogSurface.GetComponent<MeshRenderer>();
        mgr.FogMaterial = mr.sharedMaterial;
        mgr.FogRenderer = mr;

        if (Terrain.activeTerrain == null)
        {
            root.AddComponent<OneShotFoWRebuilder>().Init(mgr, 128);
        }
    }
    private class OneShotFoWRebuilder : MonoBehaviour
    {
        FogOfWarManager _mgr;
        int _grid;

        public void Init(FogOfWarManager mgr, int grid) { _mgr = mgr; _grid = grid; }

        void LateUpdate()
        {
            var t = Terrain.activeTerrain;
            if (t == null || t.terrainData == null) return;

            // Destroy previous child (flat quad) if present
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            // Rebuild conforming surface
            var mat = _mgr.FogMaterial; // reuse same material the manager updates
            GameObject fogSurface = FogOfWarConformingMesh.Create(_mgr.WorldMin, _mgr.WorldMax, _grid, mat);
            fogSurface.name = "FogSurface";
            fogSurface.transform.SetParent(transform, false);

            var mr = fogSurface.GetComponent<MeshRenderer>();
            _mgr.FogRenderer = mr; // rebind

            Destroy(this); // done
        }
    }
    public void ApplyBounds(Vector2 newMin, Vector2 newMax, float? newCellSize = null, bool clearRevealed = false, int surfaceGrid = 128)
    {
        WorldMin = newMin;
        WorldMax = newMax;
        if (newCellSize.HasValue) CellSize = Mathf.Max(0.05f, newCellSize.Value);

        // Rebind shader params for the new bounds
        EnsureMaterialBound();

        // Rebuild visibility grid (+ texture)
        ForceRebuildGrid(clearRevealed);

        // Rebuild/replace surface mesh to fit the new bounds
        if (FogRenderer != null)
        {
            var old = FogRenderer.gameObject;
            if (old != null) Destroy(old);
        }
        var mat = FogMaterial != null ? FogMaterial : new Material(Shader.Find("Unlit/FogOfWar"));
        GameObject surface = FogOfWarConformingMesh.Create(WorldMin, WorldMax, surfaceGrid, mat);
        surface.name = "FogSurface";
        surface.transform.SetParent(transform, false);
        FogRenderer = surface.GetComponent<MeshRenderer>();

        // Ensure texture is bound again and pushed
        EnsureMaterialBound();
        PushHumanTexture();
    }
}
