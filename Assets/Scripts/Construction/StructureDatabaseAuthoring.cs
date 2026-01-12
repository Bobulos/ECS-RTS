using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
public class StructureDatabaseAuthoring : MonoBehaviour
{
    [SerializeField]
    public GameObject[] data;
    //public float3 dir;
}
class StructureDatabaseBaker : Baker<StructureDatabaseAuthoring>
{
    public override void Bake(StructureDatabaseAuthoring authoring)
    {
        // GetEntity returns an entity that ECS creates from the GameObject using
        // pre-built ECS baker methods. TransformUsageFlags.Dynamic instructs the
        // Bake method to add the Transforms.LocalTransform component to the entity.
        var entity = GetEntity(authoring, TransformUsageFlags.None);
        var buffer = AddBuffer<StructureDatabaseElement>(entity);

        //Entity[] e = new Entity[authoring.data.Length];
        for (int i = 0; i < authoring.data.Length; i++)
        {
            buffer.Add(new StructureDatabaseElement { Value = GetEntity(authoring.data[i], TransformUsageFlags.Dynamic) });
        }
    }
}
[InternalBufferCapacity(64)]
public struct StructureDatabaseElement : IBufferElementData
{
    public Entity Value;
}