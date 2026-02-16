using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;
using UnityEngine;
public static class ColliderDebugUtil
{
    /// <summary>
    /// Creates a convex hull collider from FixedSelectionData (8 vertices)
    /// </summary>
/*    public static BlobAssetReference<Unity.Physics.Collider> CreateSelectionCollider(FixedSelectionData selectionData)
    {
        if (selectionData.Value.Length != 8)
        {
            UnityEngine.Debug.LogError($"Expected 8 vertices, got {selectionData.Value.Length}");
            return default;
        }

        var vertices = new NativeArray<float3>(8, Allocator.Temp);

        for (int i = 0; i < 8; i++)
            vertices[i] = selectionData.Value[i];

        DrawSelectionPrism(vertices);

        var collider = ConvexCollider.Create(
            vertices,
            ConvexHullGenerationParameters.Default,
            CollisionFilter.Default
        );

        vertices.Dispose();
        return collider;
    }*/
    public static void DrawSelectionPrism(NativeArray<float3> v, float3 pos)
    {
        // Bottom quad (0-3)
        DrawQuad(v[0]+pos, v[1] + pos, v[2] + pos, v[3] + pos, Color.green);

        // Top quad (4-7)
        DrawQuad(v[4] + pos, v[5] + pos, v[6] + pos, v[7] + pos, Color.yellow);

        // Vertical edges
        DebugLine(v[0] + pos, v[4] + pos, Color.cyan);
        DebugLine(v[1] + pos, v[5] + pos, Color.cyan);
        DebugLine(v[2] + pos, v[6] + pos, Color.cyan);
        DebugLine(v[3] + pos, v[7] + pos, Color.cyan);
    }

    static void DrawQuad(float3 a, float3 b, float3 c, float3 d, Color color)
    {
        DebugLine(a, b, color);
        DebugLine(b, c, color);
        DebugLine(c, d, color);
        DebugLine(d, a, color);
    }

    static void DebugLine(float3 a, float3 b, Color color)
    {
        UnityEngine.Debug.DrawLine((Vector3)a, (Vector3)b, color, 10f, false);
    }

}