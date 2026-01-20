using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public class StructureManifestAuthoring : MonoBehaviour
{
    [SerializeField]
    public GameObject[] manifest;
    //public float3 dir;
}
class StructureManifestBaker : Baker<StructureManifestAuthoring>
{
    public override void Bake(StructureManifestAuthoring authoring)
    {
        // GetEntity returns an entity that ECS creates from the GameObject using
        // pre-built ECS baker methods. TransformUsageFlags.Dynamic instructs the
        // Bake method to add the Transforms.LocalTransform component to the entity.
        var entity = GetEntity(authoring, TransformUsageFlags.None);

        FixedList4096Bytes<Entity> m = new FixedList4096Bytes<Entity>();
        foreach(var g in authoring.manifest)
        {
            m.Add(GetEntity(g, TransformUsageFlags.None));
        }
        AddComponent(entity, new StructureManifest { Manifest = m });
        //var buffer = AddBuffer<StructureDatabaseElement>(entity);

        //Entity[] e = new Entity[authoring.data.Length]
        //AddComponent<>
    }
}

public struct StructureManifest : IComponentData
{
    public FixedList4096Bytes<Entity> Manifest;
}