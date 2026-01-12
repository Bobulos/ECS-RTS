using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class MapDataAuthoring : MonoBehaviour
{

    public int2 size = new int2(512, 512);
    //public float3 dir;
}
class MapDataBaker : Baker<MapDataAuthoring>
{
    public override void Bake(MapDataAuthoring authoring)
    {
        // GetEntity returns an entity that ECS creates from the GameObject using
        // pre-built ECS baker methods. TransformUsageFlags.Dynamic instructs the
        // Bake method to add the Transforms.LocalTransform component to the entity.
        var entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new MapData
        {
            Size = authoring.size
        });
    }
}
public struct MapData : IComponentData
{
    public int2 Size;
}