using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class ProductionStructureAuthoring : MonoBehaviour
{
    public GameObject prefab0;
    public GameObject prefab1;
    public GameObject prefab2;
    public GameObject prefab3;
    public GameObject prefab4;
    public GameObject prefab5;
    public GameObject prefab6;
    public GameObject prefab7;
}
class ProductionStructureBaker : Baker<ProductionStructureAuthoring>
{
    public override void Bake(ProductionStructureAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ProductionStructure
        {
            Prefabs = new FixedList512Bytes<Entity> { 
                GetEntity(authoring.prefab0, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab1, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab2, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab3, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab4, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab5, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab6, TransformUsageFlags.Dynamic),
                GetEntity(authoring.prefab7, TransformUsageFlags.Dynamic)
            },
            Queue = new FixedList512Bytes<Entity> {}
        });
    }
}
public struct ProductionStructure : IComponentData
{
    //64 bytes for each unit
    public FixedList512Bytes<Entity> Prefabs;
    // can hold 8
    public FixedList512Bytes<Entity> Queue;
}