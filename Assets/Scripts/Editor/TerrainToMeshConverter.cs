using UnityEngine;
using UnityEditor;
using System.IO;

public class TerrainToMeshConverter : EditorWindow
{
    private Terrain terrain;
    private int resolution = 128;
    private string meshName = "TerrainMesh";

    [MenuItem("Tools/Terrain → Mesh Converter")]
    public static void ShowWindow()
    {
        GetWindow<TerrainToMeshConverter>("Terrain → Mesh");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert Unity Terrain to Mesh", EditorStyles.boldLabel);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        resolution = EditorGUILayout.IntSlider("Resolution", resolution, 16, 512);
        meshName = EditorGUILayout.TextField("Mesh Name", meshName);

        if (GUILayout.Button("Convert and Save"))
        {
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Terrain first!", "OK");
                return;
            }
            ConvertTerrainToMesh();
        }
    }

    private void ConvertTerrainToMesh()
    {
        TerrainData td = terrain.terrainData;

        int w = resolution;
        int h = resolution;

        Vector3 size = td.size;
        float[,] heights = td.GetHeights(0, 0, w, h);

        Vector3[] verts = new Vector3[w * h];
        Vector2[] uvs = new Vector2[w * h];
        int[] tris = new int[(w - 1) * (h - 1) * 6];

        // Build vertices and UVs
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = x + y * w;
                float height = heights[y, x];
                verts[i] = new Vector3(
                    (x / (float)(w - 1)) * size.x,
                    height * size.y,
                    (y / (float)(h - 1)) * size.z
                );
                uvs[i] = new Vector2(x / (float)(w - 1), y / (float)(h - 1));
            }
        }

        // Build triangles
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

        // Create Mesh
        Mesh mesh = new Mesh
        {
            vertices = verts,
            triangles = tris,
            uv = uvs
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Save Mesh as Asset
        string folderPath = "Assets/GeneratedMeshes";
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string path = $"{folderPath}/{meshName}.asset";
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", $"Mesh saved to:\n{path}", "OK");
    }
}
