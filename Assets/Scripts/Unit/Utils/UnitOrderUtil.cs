using Unity.Entities;
using Unity.Physics;
using Unity.Burst;
using Unity.Mathematics;
[BurstCompile]
public static class UnitOrderUtil
{
    const float DEPTH_TEST = 10f;
    public static void UnitMoveOrder(ref EntityCommandBuffer ecb, PhysicsWorld physicsWorld, Entity entity, float3 pos)
    {
        var mask = new CollisionFilter
        {
            CollidesWith = 1 << 7,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
        float3 vo = new float3(0,DEPTH_TEST,0);
        var ray = new RaycastInput
        {
            Start = pos + vo,
            End = pos - vo,
            Filter = mask,
        };
        if (physicsWorld.CastRay(ray, out var hit))
        {
            ecb.AddComponent(entity, new UnitMoveOrder { Dest = hit.Position });
        }
    }
}