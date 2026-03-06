using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Physics;
using Unity.Collections;
public static class ConstructionUtil
{
    #region Helpers
    const float STRUCTURE_CHECK_BEVEL = 0.3f;
    [BurstCompile]
    public static bool CheckValidStructurePlacement(PhysicsWorldSingleton world, float3 position, int3 size, CollisionFilter filter)
    {
        NativeList<int> hits = new NativeList<int>(Allocator.Temp);
        float3 halfExtent = ((float3)size / 2) - new float3(STRUCTURE_CHECK_BEVEL);
        
        var box = new OverlapAabbInput
        {
            Aabb = new Aabb
            {
                Max = position + halfExtent,
                Min = position - halfExtent,
            },
            Filter = filter,
        };

        bool hasOverlap = world.OverlapAabb(box, ref hits);
        hits.Dispose();
        
        return !hasOverlap;
    }
    private const float GRID_SIZE = 3f;
    [BurstCompile]
    public static float3 SnapToGrid(PhysicsWorldSingleton world, float3 position, CollisionFilter filter)
    {
        float3 gridSnapped = math.round(position / GRID_SIZE) * GRID_SIZE;
        
        if (TryGetGroundPoint(world, gridSnapped, out float3 groundPos, filter))
        {
            return groundPos;
        }
        
        return gridSnapped;
    }
    private const float DEPTH_TEST_HEIGHT = 10f;
    [BurstCompile]
    private static bool TryGetGroundPoint(PhysicsWorldSingleton world, float3 pos, out float3 result, CollisionFilter filter)
    {
        float3 upOffset = new float3(0, DEPTH_TEST_HEIGHT, 0);
        RaycastInput ray = new RaycastInput
        {
            Start = pos + upOffset,
            End = pos - upOffset,
            Filter = filter,
        };

        if (world.CastRay(ray, out Unity.Physics.RaycastHit hit))
        {
            result = hit.Position;
            return true;
        }

        result = float3.zero;
        return false;
    }
    #endregion
}