using Unity.Entities;
using UnityEngine;

public class TestSpawnerAuthoring : MonoBehaviour
{
    public GameObject prefab;
    public float rate;
    public int maxCount;
    public int radius;
    public int per = 10;
}
class TestSpawnerBaker : Baker<TestSpawnerAuthoring>
{
    public override void Bake(TestSpawnerAuthoring authoring)
    {
        // GetEntity returns an entity that ECS creates from the GameObject using
        // pre-built ECS baker methods. TransformUsageFlags.Dynamic instructs the
        // Bake method to add the Transforms.LocalTransform component to the entity.
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

        var testSpawner = new TestSpawner
        {
            // The math class is from the Unity.Mathematics namespace.
            // Unity.Mathematics is optimized for Burst-compiled code.
            Prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic),
            Rate = authoring.rate,
            LastTime = 0,
            Count = 0,
            Per = authoring.per,
            MaxCount = authoring.maxCount,
            Radius = authoring.radius,
        };
        AddComponent(entity, testSpawner);
    }
}
public struct TestSpawner : IComponentData
{
    public Entity Prefab;
    public double LastTime;
    public float Rate;
    public int Count;
    public int MaxCount;
    public float Radius;
    public int Per;
}