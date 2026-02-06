using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ProductionStructureAuthoring : MonoBehaviour
{
    public EntityData data;
    public int queueSize = 10;
    public Vector3 spawnOffset;
    [Header("Possible Units")]
    public EntityData prefab0;
    public EntityData prefab1;
    public EntityData prefab2;
    public EntityData prefab3;
    public EntityData prefab4;
    public EntityData prefab5;
    public EntityData prefab6;
    public EntityData prefab7;
}
class ProductionStructureBaker : Baker<ProductionStructureAuthoring>
{
    public override void Bake(ProductionStructureAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ProductionStructure
        {
            SpawnOffset = authoring.spawnOffset,
            RallyPoint = float3.zero,
            QueueCount = 0,
            Prefabs = new FixedList512Bytes<int> {
                authoring.prefab0.key, 
                authoring.prefab1.key, 
                authoring.prefab2.key, 
                authoring.prefab3.key, 
                authoring.prefab4.key, 
                authoring.prefab5.key, 
                authoring.prefab6.key, 
                authoring.prefab7.key,
            },
            QueueSize = authoring.queueSize,
            Queue = new FixedList512Bytes<int> {}
        });
    }
}
public struct ProductionStructure : IComponentData
{
    public float3 SpawnOffset;
    public float3 RallyPoint;
    public int QueueCount;
    public int QueueSize;
    public float StartTime;
    
    //64 bytes for each unit
    public FixedList512Bytes<int> Prefabs;
    // can hold 512
    public FixedList512Bytes<int> Queue;
}