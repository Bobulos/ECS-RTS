using Unity.Entities;
using UnityEngine;

public class StructureAuthoring : MonoBehaviour
{
    public int selectionKey = 1;
}
class StructureBaker : Baker<StructureAuthoring>
{
    public override void Bake(StructureAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new Vision { Level = 3f });
        AddComponent(entity, new StructureTag { });
        AddComponent(entity, new SelectionKey { Value = authoring.selectionKey });
        AddComponent(entity, new LocalVisibility { IsVisible = true, DisableChildren = true });
        AddComponent(entity, new UnitTeam { TeamID = 0, UnitID = -1 });
    }
}
public struct StructureTag : IComponentData { }