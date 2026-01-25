using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class StructureManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public GameObject[] manifest;
}

class StructureManifestBaker : Baker<StructureManifestAuthoring>
{
    public override void Bake(StructureManifestAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        // AddBuffer creates and returns the buffer - no need for AddComponent
        var buffer = AddBuffer<StructureManifest>(entity);

        foreach (var g in authoring.manifest)
        {
            if (g != null)
            {
                var prefabEntity = GetEntity(g, TransformUsageFlags.Dynamic);
                buffer.Add(new StructureManifest { Value = prefabEntity });
            }
        }
    }
}

// Buffer element should be simple - just hold one entity
[InternalBufferCapacity(8)] // Number of elements before it allocates to heap
public struct StructureManifest : IBufferElementData
{
    public Entity Value;
}