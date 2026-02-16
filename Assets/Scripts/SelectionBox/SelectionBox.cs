using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;


[RequireComponent(typeof(MeshFilter), typeof(UnityEngine.MeshCollider))]
public class SelectionBox : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Mesh selectionMesh;

    // We expect RuntimeColliderConverter to be attached to this same GameObject
    public RuntimeColliderConverter conv;
    private UnityEngine.MeshCollider collider;

    // The 'thickness' is no longer needed as the height is defined by the camera's Y position.

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        collider = GetComponent<UnityEngine.MeshCollider>();

        selectionMesh = new Mesh { name = "SelectionBox3D" };
        collider.sharedMesh = selectionMesh;
        meshFilter.mesh = selectionMesh;

        // Ensure the converter is set up
        if (conv == null)
        {
            conv = GetComponent<RuntimeColliderConverter>();
        }
    }

    /// <summary>
    /// Calculates the world-space intersection point of a ray with the Y=0 plane.
    /// </summary>
    /// <param name="ray">The ray to cast.</param>
    /// <param name="intersectionPoint">The resulting intersection point.</param>
    /// <returns>True if the ray intersects the plane, false otherwise.</returns>
    private bool RaycastAgainstYPlane(UnityEngine.Ray ray, out Vector3 intersectionPoint)
    {
        // Define the plane: origin (0, 0, 0), normal (0, 1, 0)
        UnityEngine.Plane yPlane = new UnityEngine.Plane(Vector3.up, Vector3.zero);

        float distance;

        // Raycast the plane
        if (yPlane.Raycast(ray, out distance))
        {
            intersectionPoint = ray.GetPoint(distance);
            return true;
        }

        intersectionPoint = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Updates the visual mesh and its underlying MeshCollider to match the 3D drag volume.
    /// Bottom vertices are snapped to the y=0 plane, and top vertices are set to the camera's y position.
    /// </summary>
    /// <param name="cam">The camera to cast rays from.</param>
    /// <param name="screenStart">The screen start point of the drag.</param>
    /// <param name="screenEnd">The screen end point of the drag.</param>
    public FixedSelectionData UpdatePerspectiveSelection(Camera cam, Vector2 screenStart, Vector2 screenEnd)
    {
        // 1. Get the 4 screen corners of the drag box
        Vector2 min = Vector2.Min(screenStart, screenEnd);
        Vector2 max = Vector2.Max(screenStart, screenEnd);

        Vector2[] screenCorners =
        {
            new Vector2(min.x, min.y), // Bottom-left
            new Vector2(max.x, min.y), // Bottom-right
            new Vector2(max.x, max.y), // Top-right
            new Vector2(min.x, max.y)  // Top-left
        };

        // 2. Calculate the intersection point on the Y=0 plane for each screen corner
        Vector3[] projectedXZPoints = new Vector3[4];
        bool allHitsValid = true;

        for (int i = 0; i < 4; i++)
        {
            UnityEngine.Ray ray = cam.ScreenPointToRay(screenCorners[i]);
            Vector3 intersection;

            if (RaycastAgainstYPlane(ray, out intersection))
            {
                projectedXZPoints[i] = intersection;
            }
            else
            {
                Debug.LogWarning("Selection ray failed to intersect Y=0 plane for corner " + i);
                projectedXZPoints[i] = Vector3.zero;
                allHitsValid = false;
            }
        }

        if (!allHitsValid) return new FixedSelectionData();

        // Define the fixed Y-coordinates
        float bottomY = 0f;
        float topY = cam.transform.position.y; // FIXED: Store this as a variable

        // 3. Generate the 3D Vertices (8 total) - FIXED: Create proper box
        Vector3[] vertices = new Vector3[8];
        for (int i = 0; i < 4; i++)
        {
            Vector3 xzAnchor = projectedXZPoints[i];

            // Bottom vertex: At Y=0
            vertices[i] = new Vector3(xzAnchor.x, bottomY, xzAnchor.z);

            // Top vertex: Same XZ but at camera height - FIXED
            vertices[i + 4] = cam.transform.position;
        }

        // 4. Assign to mesh
        Mesh mesh = selectionMesh;
        mesh.Clear();
        mesh.vertices = vertices;

        mesh.triangles = new int[]
        {
        // Bottom face (at Y=0)
        0, 1, 2, 2, 3, 0,
        // Top face (at Camera Y)
        4, 7, 6, 6, 5, 4,
        // Sides
        0, 4, 5, 5, 1, 0,
        1, 5, 6, 6, 2, 1,
        2, 6, 7, 7, 3, 2,
        3, 7, 4, 4, 0, 3
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Convert to FixedSelectionData
        var fsd = new FixedList128Bytes<float3>();
        foreach (var v in vertices)
        {
            fsd.Add(v);
        }

        return new FixedSelectionData { Value = fsd };
    }
    //one for replay
    public void UpdatePerspectiveSelection(FixedSelectionData verticies)
    {
        if (verticies.Value.Length == 0) return;
        // 4. Assign to mesh
        Mesh mesh = selectionMesh;
        mesh.Clear();

        //convert it
        Vector3[] verts = new Vector3[verticies.Value.Length];
        for (int i = 0; i < verticies.Value.Length; i++)
        {
            verts[i] = verticies.Value[i];
        }
        mesh.vertices = verts;

        // Define the box triangles (6 faces * 2 triangles/face * 3 vertices/triangle = 36 indices)
        mesh.triangles = new int[]
        {
            // Bottom face (at Y=0)
            0, 1, 2, 2, 3, 0,
            // Top face (at Camera Y)
            4, 7, 6, 6, 5, 4,
            // Sides
            0, 4, 5, 5, 1, 0,
            1, 5, 6, 6, 2, 1,
            2, 6, 7, 7, 3, 2,
            3, 7, 4, 4, 0, 3
        };

        // Finalize mesh
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
    /// <summary>
    /// Creates and returns a new ECS Entity with a PhysicsCollider based on the current mesh.
    /// </summary>
    /*public Entity GetColliderEntity()
    {
        // 2. Ensure the converter has the up-to-date mesh data to read from.
        if (collider.sharedMesh == null || collider.sharedMesh.vertexCount == 0)
        {
            Debug.Log("Cannot bake collider: Selection mesh is empty.");
            return Entity.Null;
        }

        // 3. Use the converter component to perform the actual baking and entity creation.
        // Assuming RuntimeColliderConverter has a method to bake the assigned mesh.
        return conv.ConvertToEntityWithCollider();
    }*/

    public void ClearBox()
    {
        selectionMesh.Clear();
    }
}
public class SelectionData
{
    public Vector3[] value = new Vector3[8];
    public SelectionData(Vector3[] vertices)
    {
        value = vertices;
    }
}
