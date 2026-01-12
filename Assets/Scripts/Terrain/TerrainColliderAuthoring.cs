using UnityEngine;
using Unity.Entities;
using UnityEditor;

[RequireComponent(typeof(MeshCollider))]
public class TerrainColliderAuthoring : MonoBehaviour
{
    [Range(2, 512)] public int resolution = 128;
    public Terrain terrain;

    public Mesh ConvertTerrainToMesh()
    {
        TerrainData td = terrain.terrainData;
        Vector3 size = td.size;
        int hmRes = td.heightmapResolution;

        int w = Mathf.Clamp(resolution, 2, hmRes);
        int h = Mathf.Clamp(resolution, 2, hmRes);

        float[,] heights = td.GetHeights(0, 0, hmRes, hmRes);

        Vector3[] verts = new Vector3[w * h];
        Vector2[] uvs = new Vector2[w * h];
        int[] tris = new int[(w - 1) * (h - 1) * 6];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = x + y * w;

                // normalized coords [0,1]
                float nx = x / (float)(w - 1);
                float ny = y / (float)(h - 1);

                // sample using bilinear interpolation so it always stays in bounds
                float fx = nx * (hmRes - 1);
                float fy = ny * (hmRes - 1);

                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                int x1 = Mathf.Min(x0 + 1, hmRes - 1);
                int y1 = Mathf.Min(y0 + 1, hmRes - 1);

                float hx0 = Mathf.Lerp(heights[y0, x0], heights[y0, x1], fx - x0);
                float hx1 = Mathf.Lerp(heights[y1, x0], heights[y1, x1], fx - x0);
                float height = Mathf.Lerp(hx0, hx1, fy - y0);

                verts[i] = new Vector3(nx * size.x, height * size.y, ny * size.z);
                uvs[i] = new Vector2(nx, ny);
            }
        }

        int t = 0;
        for (int y = 0; y < h - 1; y++)
        {
            for (int x = 0; x < w - 1; x++)
            {
                int i = x + y * w;
                tris[t++] = i;
                tris[t++] = i + w;
                tris[t++] = i + w + 1;

                tris[t++] = i;
                tris[t++] = i + w + 1;
                tris[t++] = i + 1;
            }
        }

        Mesh mesh = new Mesh
        {
            vertices = verts,
            triangles = tris,
            uv = uvs
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

class TerrainColliderBaker : Baker<TerrainColliderAuthoring>
{
    public override void Bake(TerrainColliderAuthoring authoring)
    {
        var c = authoring.GetComponent<MeshCollider>();
        c.sharedMesh = authoring.ConvertTerrainToMesh();

        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        // Add more ECS components here if needed
    }
}
