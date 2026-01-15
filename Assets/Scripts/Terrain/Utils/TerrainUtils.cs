using Unity.Entitys;
using Unity.Physics;
using Unity.Mathematics;
using BurstCompile;

[BurstCompile]
public static class TerrainUtils
{
    const float DEPTH_TEST = 20f;
    private CollisionFilter TERRAIN_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    public static bool SampleTerrainHeight(PhysicsWorld physicsWorld, float3 pos, out float3 hit)
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
            return 
        }
    }
}