using Unity.Entities;
using UnityEngine;

public class StructureAuthoring : MonoBehaviour
{
    public EntityData data;
    public int selectionKey = 1;
    public float visionRadius = 5f;
}
class StructureBaker : Baker<StructureAuthoring>
{
    public override void Bake(StructureAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new Vision { Radius = Mathf.Round(authoring.visionRadius) });
        AddComponent(entity, new StructureTag { });
        AddComponent(entity, new SelectionKey { Value = authoring.selectionKey });
        AddComponent(entity, new LocalVisibility { IsVisible = true, DisableChildren = true });
        AddComponent(entity, new Team { TeamID = 0, UnitID = -1 });
    }
}
public struct StructureTag : IComponentData { }