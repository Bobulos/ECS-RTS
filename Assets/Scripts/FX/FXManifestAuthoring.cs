using Unity.Entities;
using UnityEngine;

class FXManifestAuthoring : MonoBehaviour
{
    public GameObject explosion;
}

class FXManifestAuthoringBaker : Baker<FXManifestAuthoring>
{
    public override void Bake(FXManifestAuthoring authoring)
    {
        GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(new FXManifest { Explosion = GetEntity(authoring.explosion, TransformUsageFlags.Dynamic) });
    }
}
public struct FXManifest : IComponentData
{
    public Entity Explosion;
}