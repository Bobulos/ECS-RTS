using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class UnitManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public GameObject[] manifest;
}

class UnitManifestBaker : Baker<UnitManifestAuthoring>
{
    public override void Bake(UnitManifestAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        // AddBuffer creates and returns the buffer - no need for AddComponent
        var buffer = AddBuffer<UnitManifest>(entity);

        foreach (var g in authoring.manifest)
        {
            if (g != null)
            {
                var prefabEntity = GetEntity(g, TransformUsageFlags.Dynamic);
                buffer.Add(new UnitManifest { Value = prefabEntity });
            }
        }
    }
}

// Buffer element should be simple - just hold one entity
[InternalBufferCapacity(8)] // Number of elements before it allocates to heap
public struct UnitManifest : IBufferElementData
{
    public Entity Value;
}