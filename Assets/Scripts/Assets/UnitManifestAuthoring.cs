using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public class UnitManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public GameObject[] manifest;
    //public float3 dir;
}
class UnitManifestBaker : Baker<UnitManifestAuthoring>
{
    public override void Bake(UnitManifestAuthoring authoring)
    {
        // GetEntity returns an entity that ECS creates from the GameObject using
        // pre-built ECS baker methods. TransformUsageFlags.Dynamic instructs the
        // Bake method to add the Transforms.LocalTransform component to the entity.
        var entity = GetEntity(authoring, TransformUsageFlags.None);

        FixedList4096Bytes<Entity> m = new FixedList4096Bytes<Entity>();
        foreach (var g in authoring.manifest)
        {
            m.Add(GetEntity(g, TransformUsageFlags.None));
        }
        AddComponent(entity, new UnitManifest { Manifest = m });
        //var buffer = AddBuffer<StructureDatabaseElement>(entity);

        //Entity[] e = new Entity[authoring.data.Length]
        //AddComponent<>
    }
}

public struct UnitManifest : IComponentData
{
    public FixedList4096Bytes<Entity> Manifest;
}