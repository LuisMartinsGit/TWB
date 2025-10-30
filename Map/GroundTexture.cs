using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class GroundTexture : MonoBehaviour
{
    [Header("Provide either a Material OR a Texture")]
    public Material Material;          // optional: if you have a working mat in your project
    public Texture2D Texture;          // optional: if you just want to assign a texture
    public string ResourcesTexturePath; // optional: e.g. "Ground/grass" to load from Resources

    [Header("Tiling & Tint")]
    public Vector2 Tiling = new Vector2(100, 100);
    public Color Tint = Color.white;

    MeshRenderer _mr;

    void Awake()
    {
        _mr = GetComponent<MeshRenderer>();
    }

    void Start()
    {
        if (_mr == null) return;

        // If a texture path is given, try to load it now
        if (Texture == null && !string.IsNullOrEmpty(ResourcesTexturePath))
        {
            Texture = Resources.Load<Texture2D>(ResourcesTexturePath);
        }

        // Choose / create the material to assign
        Material matToUse = Material;

        if (matToUse == null)
        {
            // Try to make a reasonable default that matches the pipeline
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null)
            {
                Debug.LogWarning("GroundTexture: No suitable shader found. Skipping.");
                return;
            }
            matToUse = new Material(sh);
        }
        else
        {
            // Use an instance so we don't mutate your project asset at runtime
            matToUse = new Material(matToUse);
        }

        // Apply texture/tint/tiling robustly
        ApplyToMaterial(matToUse, Texture, Tiling, Tint);

        // Assign to the renderer
        _mr.material = matToUse; // instance per-scene; avoids editing shared asset
    }

    static void ApplyToMaterial(Material mat, Texture2D tex, Vector2 tiling, Color tint)
    {
        if (mat == null) return;

        // Prefer URP properties if present, else fall back to Built-in
        bool hasBaseMap  = mat.HasProperty("_BaseMap");
        bool hasMainTex  = mat.HasProperty("_MainTex");
        bool hasBaseCol  = mat.HasProperty("_BaseColor");
        bool hasColor    = mat.HasProperty("_Color");

        if (tex != null)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            if (hasBaseMap)
            {
                mat.SetTexture("_BaseMap", tex);
                // SetTextureScale only works if the shader supports it
                mat.SetTextureScale("_BaseMap", tiling);
            }
            if (hasMainTex)
            {
                mat.SetTexture("_MainTex", tex);
                mat.mainTextureScale = tiling; // also sets _MainTex_ST
            }
        }

        if (hasBaseCol) mat.SetColor("_BaseColor", tint);
        if (hasColor)   mat.SetColor("_Color", tint);
    }
}
