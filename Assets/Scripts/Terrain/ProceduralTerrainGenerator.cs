using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Terrain))]
public class ProceduralTerrainGenerator : MonoBehaviour
{
    private Terrain terrain;
    private TerrainData terrainData;

    [Header("General Settings")]
    [Tooltip("Must be (2^N) + 1, e.g., 257, 513, 1025.")]
    public int resolution = 513;
    public float terrainSize = 1000f;
    public float heightMultiplier = 150f;

    [Header("Noise Settings (FBM)")]
    public int seed = 0;
    [Range(0f, 1f)] public float noiseScale = 0.01f;
    public int octaves = 4;
    [Range(0f, 1f)] public float persistence = 0.5f;
    public float lacunarity = 2.0f;
    public Vector2 offset = Vector2.zero;

    [Header("Height Curve")]
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Terrain Layers (Flexible Count)")]
    public TerrainLayer[] terrainLayers;

    [Header("Layer Thresholds (Normalized 0–1 height values)")]
    [Tooltip("Each entry defines where a new texture starts blending in. Must have terrainLayers.Length entries.")]
    public float[] heightThresholds;

    [Header("Slope Influence")]
    [Range(0f, 90f)] public float rockSlopeAngle = 35f;
    [Range(0f, 1f)] public float slopeBlendStrength = 0.5f;

    void OnEnable()
    {
        InitializeTerrain();
        GenerateTerrain();
    }

    void OnValidate()
    {
        noiseScale = Mathf.Max(0.0001f, noiseScale);
        lacunarity = Mathf.Max(1.0f, lacunarity);
        octaves = Mathf.Clamp(octaves, 1, 8);
        if (!Application.isPlaying && terrain != null)
        {
            InitializeTerrain();
            GenerateTerrain();
        }
    }

    private void InitializeTerrain()
    {
        terrain = GetComponent<Terrain>();
        terrainData = terrain.terrainData ?? new TerrainData();
        terrain.terrainData = terrainData;

        terrainData.heightmapResolution = resolution;
        terrainData.size = new Vector3(terrainSize, heightMultiplier, terrainSize);

        if (terrainLayers != null && terrainLayers.Length > 0)
            terrainData.terrainLayers = terrainLayers;
        else
            Debug.LogWarning("Assign at least one TerrainLayer!");
    }

    public void GenerateTerrain()
    {
        if (terrainData == null) return;

        float[,] heights = GenerateHeights();
        terrainData.SetHeights(0, 0, heights);

        ApplyTextures(heights);
    }

    // --------------------------------------------------------------
    // HEIGHTMAP GENERATION
    // --------------------------------------------------------------
    private float[,] GenerateHeights()
    {
        float[,] heights = new float[resolution, resolution];
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = ((float)x / (resolution - 1)) / noiseScale * frequency + octaveOffsets[i].x;
                    float sampleY = ((float)y / (resolution - 1)) / noiseScale * frequency + octaveOffsets[i].y;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                float normalizedHeight = Mathf.InverseLerp(-octaves, octaves, noiseHeight);
                heights[y, x] = heightCurve.Evaluate(normalizedHeight);
            }
        }

        return heights;
    }

    // --------------------------------------------------------------
    // TEXTURE BLENDING (Flexible)
    // --------------------------------------------------------------
    private void ApplyTextures(float[,] heights)
    {
        if (terrainLayers == null || terrainLayers.Length == 0) return;

        int layerCount = terrainLayers.Length;
        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        float[,,] splatmapData = new float[height, width, layerCount];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float h = heights[
                    Mathf.RoundToInt((float)y / height * (heights.GetLength(0) - 1)),
                    Mathf.RoundToInt((float)x / width * (heights.GetLength(1) - 1))
                ];

                float slope = terrainData.GetSteepness(
                    (float)x / (width - 1),
                    (float)y / (height - 1)
                ) / 90f;

                float[] weights = new float[layerCount];

                // ✅ Always start with the base layer at 1
                weights[0] = 1f;

                // Blend in higher layers based on thresholds
                for (int i = 1; i < layerCount; i++)
                {
                    float start = (heightThresholds != null && i - 1 < heightThresholds.Length)
                        ? heightThresholds[i - 1]
                        : (float)(i - 1) / layerCount;

                    float end = (heightThresholds != null && i < heightThresholds.Length)
                        ? heightThresholds[i]
                        : (float)i / layerCount;

                    weights[i] = Mathf.InverseLerp(start, end, h);
                }

                // Add slope influence to mid layers
                if (layerCount > 1)
                {
                    int midIndex = Mathf.Clamp(layerCount / 2, 0, layerCount - 1);
                    weights[midIndex] += slope * slopeBlendStrength;
                }

                // Normalize weights
                float total = 0.0001f;
                for (int i = 0; i < layerCount; i++) total += weights[i];
                for (int i = 0; i < layerCount; i++) weights[i] /= total;

                // Write to splatmap
                for (int i = 0; i < layerCount; i++)
                    splatmapData[y, x, i] = weights[i];
            }
        }

        terrainData.SetAlphamaps(0, 0, splatmapData);
    }


    // --------------------------------------------------------------
    // CLEAR TERRAIN
    // --------------------------------------------------------------
    public void ClearTerrain()
    {
        if (terrainData == null) return;

        float[,] zeroHeights = new float[resolution, resolution];
        terrainData.SetHeights(0, 0, zeroHeights);

        float[,,] clearSplatmap = new float[terrainData.alphamapHeight, terrainData.alphamapWidth, terrainLayers.Length];
        terrainData.SetAlphamaps(0, 0, clearSplatmap);
    }
}
