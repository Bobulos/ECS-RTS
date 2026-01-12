using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class WallAuthoring : MonoBehaviour
{
    public int id = 1;
}
class WallBaker : Baker<WallAuthoring>
{
    public override void Bake(WallAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

        AddComponent(entity, new WallNode
        {
            ID = authoring.id
        });
        AddComponent(entity, new LocalVisibility { IsVisible = true, DisableChildren = true });
        AddComponent(entity, new UnitTeam { TeamID = 0, UnitID = -1});
    }
}
public struct WallNode : IComponentData
{
    public int ID;
}