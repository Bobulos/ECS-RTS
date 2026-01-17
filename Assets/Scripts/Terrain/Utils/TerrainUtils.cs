using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics;

[BurstCompile]
public static class TerrainUtils
{
    const float DEPTH_TEST = 20f;
    public static bool SampleTerrainHeight(PhysicsWorld physicsWorld, float3 pos, out float3 hit)
    {
        var mask = new CollisionFilter
        {
            CollidesWith = 1 << 7,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
        hit = float3.zero;
        float3 vo = new float3(0, DEPTH_TEST, 0);
        var ray = new RaycastInput
        {
            Start = pos + vo,
            End = pos - vo,
            Filter = mask,
        };
        if (physicsWorld.CastRay(ray, out var hitTer))
        {

            return true;
        }
        return false;
    }
}