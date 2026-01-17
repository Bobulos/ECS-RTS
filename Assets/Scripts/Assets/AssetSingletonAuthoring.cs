using Unity.Entities;
using UnityEngine;

public class AssetSingletonAuthoring : MonoBehaviour
{
    public GameObject selectedVisual;
    public GameObject validPlacement;
    public GameObject invalidPlacement;
}
class AssetSingletonBaker : Baker<AssetSingletonAuthoring>
{
    public override void Bake(AssetSingletonAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.None);

        AddComponent(entity, new AssetSingletonInit
        {
            SelectedVisual = GetEntity(authoring.selectedVisual, TransformUsageFlags.Dynamic),
            ValidMaterialEntity = GetEntity(authoring.validPlacement, TransformUsageFlags.Renderable),
            InvalidMaterialEntity = GetEntity(authoring.invalidPlacement, TransformUsageFlags.Renderable),
        });
    }
}

public struct AssetSingletonInit : IComponentData
{
    public Entity SelectedVisual;
    public Entity ValidMaterialEntity;
    public Entity InvalidMaterialEntity;
}
public struct AssetSingleton : IComponentData
{
    public Entity SelectedVisual;
    public int ValidMaterialID;
    public int InvalidMaterialID;
}
