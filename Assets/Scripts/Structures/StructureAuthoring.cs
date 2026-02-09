using Unity.Entities;
using UnityEngine;

public class StructureAuthoring : MonoBehaviour
{
    public EntityData data;
    public int teamID = 0;
    public int selectionKey = 1;
    public float visionRadius = 5f;
    public int hp = 100;
}
class StructureBaker : Baker<StructureAuthoring>
{
    public override void Bake(StructureAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new Vision { Radius = Mathf.Round(authoring.visionRadius) });
        AddComponent(entity, new StructureTag { });
        AddComponent(entity, new SelectionKey { Value = authoring.data.keyGUI });
        AddComponent(entity, new LocalVisibility { IsVisible = true, DisableChildren = true });
        AddComponent(entity, new Team { TeamID = authoring.teamID, UnitID = -1 });
        AddComponent(entity, new UnitHP {HP = authoring.hp});
    }
}
public struct StructureTag : IComponentData { }