using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Collections;
using UnityEngine;
using System.Collections;
public class RuntimeColliderConverter : MonoBehaviour
{
    private UnityEngine.MeshCollider meshCollider;

    void Awake()
    {
        meshCollider = GetComponent<UnityEngine.MeshCollider>();
    }

    /// <summary>
    /// Converts this GameObject into an ECS Entity with a baked PhysicsCollider.
    /// NOTE: The caller is responsible for ensuring the generated entity is cleaned up.
    /// </summary>
    public Entity ConvertToEntityWithCollider()
    {
        if (meshCollider == null || meshCollider.sharedMesh == null)
        {
            Debug.LogError("GameObject requires a MeshCollider with a shared mesh assigned.");
            return Entity.Null;
        }

        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            Debug.LogError("Default ECS World is not initialized.");
            return Entity.Null;
        }
        EntityManager entityManager = defaultWorld.EntityManager;

        // --- 1. Get Source Mesh Data ---
        Mesh mesh = meshCollider.sharedMesh;
        Vector3[] unityVertices = mesh.vertices;

        // 2. Copy Vertices to a DOTS Native Array (Local Space)
        var vertices = new NativeList<float3>(unityVertices.Length, Allocator.Temp);
        for (int i = 0; i < unityVertices.Length; i++)
        {
            // IMPORTANT: The vertices must be transformed into World Space or handled 
            // relative to the entity's position. Since your selection box is a complex, 
            // custom mesh, we assume the vertices are already defined relative to the 
            // local transform (0, 0, 0) of the GameObject. 
            // If the GO has a non-identity transform, you MUST transform them here.
            Vector3 worldVertex = transform.TransformPoint(unityVertices[i]);
            vertices.Add(new float3(worldVertex.x, worldVertex.y, worldVertex.z));
        }

        // --- 3. BAKE the Collider at Runtime (The Cooking Process) ---
        // ConvexHullCollider is much faster to generate at runtime than MeshCollider.
        BlobAssetReference<Unity.Physics.Collider> colliderBlob = ConvexCollider.Create(
            vertices.AsArray(),
            ConvexHullGenerationParameters.Default, CollisionFilter.Default
            //new CollisionFilter { CollidesWith = 0}
// Or a custom filter
        );

        // 4. Create the Entity Archetype (needs Translation, Rotation, PhysicsCollider)
        EntityArchetype archetype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(PhysicsCollider), // Holds the baked collider asset
            typeof(PhysicsWorldIndex)
        );

        Entity newEntity = entityManager.CreateEntity(archetype);

        // 5. Assign Component Data

        // 🌟 Assign the baked collider blob to the entity
        //entityManager.SetComponentData(newEntity, new CollisionFilter { CollidesWith = 0 });

        entityManager.SetComponentData(newEntity, new PhysicsCollider { 

            Value = colliderBlob
        });
        entityManager.SetComponentData(newEntity, new LocalTransform
        {

            Position = transform.position,
        });

        //Debug.Log($"Baked collider and created ECS Entity: {newEntity}");

        // 6. Cleanup
        //colliderBlob.Dispose();
        vertices.Dispose();
        //Destroy(gameObject); // Remove the original GameObject
        StartCoroutine(DisposeOfBlob(colliderBlob));
        return newEntity;
    }

    //discard blob asset
    IEnumerator DisposeOfBlob(BlobAssetReference<Unity.Physics.Collider> blob)
    {
        // Wait for one full frame cycle
        yield return null;
        blob.Dispose();
    }
}