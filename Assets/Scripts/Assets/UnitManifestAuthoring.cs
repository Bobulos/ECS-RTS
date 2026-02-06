using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class UnitManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public UnitManifestAuthoringElement[] manifest;
}
[Serializable]
public class UnitManifestAuthoringElement
{
    public GameObject prefab;
    public ProductionKey productionKey;
}

class UnitManifestBaker : Baker<UnitManifestAuthoring>
{
    public override void Bake(UnitManifestAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        // AddBuffer creates and returns the buffer - no need for AddComponent
        var buffer = AddBuffer<UnitManifest>(entity);

        foreach (var e in authoring.manifest)
        {
            if (e != null)
            {
                var prefabEntity = GetEntity(e.prefab, TransformUsageFlags.Dynamic);
                buffer.Add(new UnitManifest { Unit = prefabEntity, 
                TrainingTime = e.productionKey.TrainingTime});
            }
        }
    }
}

// Buffer element should be simple - just hold one entity
[InternalBufferCapacity(8)] // Number of elements before it allocates to heap
public struct UnitManifest : IBufferElementData
{
    public Entity Unit;
    public float TrainingTime;
}