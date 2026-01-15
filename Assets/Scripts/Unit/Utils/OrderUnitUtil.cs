using Unity.Entitys;
using Unity.Physics;
using BurstCompile;
[BurstCompile]
public static class OrderUnitUtil
{
    const float DEPTH_TEST = 10f;
    private CollisionFilter TERRAIN_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    public static void UnitMoveOrder(ref EntityCommandBuffer ecb, PhysicsWorld physicsWorld, float3 pos)
    {
        float3 vo = new float3(0,DEPTH_TEST,0);
        var ray = new RaycastInput
        {
            Start = movPos + vo,
            End = movPos - vo,
            Filter = TERRAIN_MASK,
        };
        if (physicsWorld.CastRay(ray, out var hit))
        {
            ecb.AddComponent(entity, new UnitMoveOrder { Dest = hit.Position });
        }
    }
}