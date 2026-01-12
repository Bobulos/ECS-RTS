using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class LocalPlayerAuthoring : MonoBehaviour
{

    public int teamID;
    //public float3 dir;
}
class LocalPlayerBaker : Baker<LocalPlayerAuthoring>
{
    public override void Bake(LocalPlayerAuthoring authoring)
    {
        // GetEntity returns an entity that ECS creates from the GameObject using
        // pre-built ECS baker methods. TransformUsageFlags.Dynamic instructs the
        // Bake method to add the Transforms.LocalTransform component to the entity.
        var entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new LocalPlayerData
        {
            TeamID = authoring.teamID
        });
    }
}
public struct LocalPlayerData : IComponentData
{
    public int TeamID;
}