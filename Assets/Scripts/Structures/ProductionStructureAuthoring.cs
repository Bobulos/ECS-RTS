using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ProductionStructureAuthoring : MonoBehaviour
{
    public Vector3 spawnOffset;
    [Header("Possible Units")]
    public UnitData prefab0;
    public UnitData prefab1;
    public UnitData prefab2;
    public UnitData prefab3;
    public UnitData prefab4;
    public UnitData prefab5;
    public UnitData prefab6;
    public UnitData prefab7;
}
class ProductionStructureBaker : Baker<ProductionStructureAuthoring>
{
    public override void Bake(ProductionStructureAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ProductionStructure
        {
            SpawnOffset = authoring.spawnOffset,
            QueueCount = 0,
            Prefabs = new FixedList512Bytes<int> {
                authoring.prefab0.Key, 
                authoring.prefab1.Key, 
                authoring.prefab2.Key, 
                authoring.prefab3.Key, 
                authoring.prefab4.Key, 
                authoring.prefab5.Key, 
                authoring.prefab6.Key, 
                authoring.prefab7.Key,
            },
            Queue = new FixedList512Bytes<int> {}
        });
    }
}
public struct ProductionStructure : IComponentData
{
    public float3 SpawnOffset;
    public int QueueCount;
    //64 bytes for each unit
    public FixedList512Bytes<int> Prefabs;
    // can hold 512
    public FixedList512Bytes<int> Queue;
}