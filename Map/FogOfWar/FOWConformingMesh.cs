using UnityEngine;

public static class FogOfWarConformingMesh
{
    // Build a grid mesh snapped to the active Terrain’s height
    public static GameObject Create(Vector2 worldMin, Vector2 worldMax, int grid = 128, Material mat = null)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return CreateFlatQuad(worldMin, worldMax, mat);

        var td = terrain.terrainData;
        var tpos = terrain.transform.position;
        var tsize = td.size;

        int vertsX = Mathf.Max(2, grid + 1);
        int vertsZ = Mathf.Max(2, grid + 1);

        var verts = new Vector3[vertsX * vertsZ];
        var uvs   = new Vector2[verts.Length];
        var tris  = new int[(vertsX - 1) * (vertsZ - 1) * 6];

        for (int z = 0; z < vertsZ; z++)
        {
            float vz = Mathf.Lerp(worldMin.y, worldMax.y, z / (float)(vertsZ - 1));
            float vT = Mathf.InverseLerp(tpos.z, tpos.z + tsize.z, vz);
            for (int x = 0; x < vertsX; x++)
            {
                float vx = Mathf.Lerp(worldMin.x, worldMax.x, x / (float)(vertsX - 1));
                float uT = Mathf.InverseLerp(tpos.x, tpos.x + tsize.x, vx);

                float y = td.GetInterpolatedHeight(uT, vT) + 0.03f; // tiny lift to avoid z-fight
                int i = z * vertsX + x;
                verts[i] = new Vector3(vx, y, vz);

                // UV in FoW texture space (0..1 over world bounds)
                float u = Mathf.InverseLerp(worldMin.x, worldMax.x, vx);
                float v = Mathf.InverseLerp(worldMin.y, worldMax.y, vz);
                uvs[i] = new Vector2(u, v);
            }
        }

        int ti = 0;
        for (int z = 0; z < vertsZ - 1; z++)
        {
            for (int x = 0; x < vertsX - 1; x++)
            {
                int i0 = z * vertsX + x;
                int i1 = i0 + 1;
                int i2 = i0 + vertsX;
                int i3 = i2 + 1;

                tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
            }
        }

        var mesh = new Mesh { name = "FogConformMesh" };
        mesh.indexFormat = (verts.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("FogConforming");
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;

        if (mat == null) mat = new Material(Shader.Find("Unlit/FogOfWar"));
        // Depth test/write so it sits on terrain without parallax; render after terrain
        mat.renderQueue = 3000;
        mr.sharedMaterial = mat;
        return go;
    }

    static GameObject CreateFlatQuad(Vector2 min, Vector2 max, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FogOfWar";
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        go.transform.position = new Vector3(0, 0.2f, 0);
        go.transform.localScale = new Vector3(max.x - min.x, max.y - min.y, 1);
        var mr = go.GetComponent<MeshRenderer>();
        if (mat == null) mat = new Material(Shader.Find("Unlit/FogOfWar"));
        mr.sharedMaterial = mat;
        var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
        return go;
    }
}
