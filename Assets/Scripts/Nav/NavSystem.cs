using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Experimental.AI; // NavMesh types


public struct Pather : IComponentData
{
    public bool NeedsUpdate;
    public float3 Dest;
    public float IndexDistance;
    public int WaypointIndex;
    public bool PathCalculated;

    public bool QuerySet;

    public int QueryIndex;

    public int Bucket;
}

[InternalBufferCapacity(32)]
public struct PatherWayPoint : IBufferElementData
{
    public float3 Position;
}


[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct NavSystem : ISystem
{
    private NavMeshWorld _navWorld;
    private NavMeshQuery _query;

    // hard cap so nav never spikes a frame
    private const int MAX_PATHS_PER_FRAME = 4;

    public void OnCreate(ref SystemState state)
    {
        _navWorld = NavMeshWorld.GetDefaultWorld();
        _query = new NavMeshQuery(_navWorld, Allocator.Persistent, 2048);
    }

    public void OnDestroy(ref SystemState state)
    {
        _query.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        int processed = 0;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (pather, transform, entity) in
                 SystemAPI.Query<RefRW<Pather>, RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            if (processed >= MAX_PATHS_PER_FRAME)
                break;

            if (!pather.ValueRO.NeedsUpdate)
                continue;

            pather.ValueRW.NeedsUpdate = false;

            TryCalculatePath(entity, ref pather.ValueRW, transform.ValueRO.Position, ref ecb);
            processed++;
        }

        if (processed > 0)
            ecb.Playback(state.EntityManager);

        ecb.Dispose();
    }

    // ============================================================
    // PATH CALCULATION
    // ============================================================
    private void TryCalculatePath(
        Entity entity,
        ref Pather pather,
        float3 fromPosition,
        ref EntityCommandBuffer ecb)
    {
        float3 toPosition = pather.Dest;
        float3 extents = new float3(1f, 2f, 1f);

        var fromLoc = _query.MapLocation(fromPosition, extents, 0);
        var toLoc = _query.MapLocation(toPosition, extents, 0);

        if (!_query.IsValid(fromLoc) || !_query.IsValid(toLoc))
        {
            pather.PathCalculated = false;
            return;
        }

        if (_query.BeginFindPath(fromLoc, toLoc) != PathQueryStatus.InProgress)
        {
            pather.PathCalculated = false;
            return;
        }

        var status = _query.UpdateFindPath(512, out _);
        if (status != PathQueryStatus.Success && status != PathQueryStatus.InProgress)
        {
            pather.PathCalculated = false;
            return;
        }

        if (_query.EndFindPath(out int pathSize) != PathQueryStatus.Success || pathSize == 0)
        {
            pather.PathCalculated = false;
            return;
        }

        var polys = new NativeArray<PolygonId>(pathSize + 1, Allocator.Temp);
        _query.GetPathResult(polys);

        var straight = new NativeArray<NavMeshLocation>(pathSize + 1, Allocator.Temp);
        var flags = new NativeArray<StraightPathFlags>(pathSize + 1, Allocator.Temp);
        var sides = new NativeArray<float>(pathSize + 1, Allocator.Temp);

        int straightCount = 0;

        var straightStatus = PathUtils.FindStraightPath(
            _query,
            fromPosition,
            toPosition,
            polys,
            pathSize,
            ref straight,
            ref flags,
            ref sides,
            ref straightCount,
            straight.Length
        );

        polys.Dispose();

        if (straightStatus != PathQueryStatus.Success || straightCount == 0)
        {
            straight.Dispose();
            flags.Dispose();
            sides.Dispose();
            pather.PathCalculated = false;
            return;
        }

        // clear + write waypoints
        ecb.SetBuffer<PatherWayPoint>(entity);

        for (int i = 0; i < straightCount; i++)
        {
            float3 pos = straight[i].position;
            ecb.AppendToBuffer(entity, new PatherWayPoint { Position = pos });
        }

        straight.Dispose();
        flags.Dispose();
        sides.Dispose();

        pather.PathCalculated = true;
        pather.WaypointIndex = 0;
    }
}

/*//[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct NavSystem : ISystem
{
    // Persistent storage for NavMeshQuery objects. Index -> NavMeshQuery.
    // Each pather entity will own one query index.

    //DEPRECATED
    private NativeList<NavMeshQuery> _navQueries;
    private NavMeshWorld _navMeshWorld;

    private int _bucket;
    private int _maxBucket;
    // Keep a list of entities that need pathing this frame (temporary each update)ss
    private EntityQuery _patherQuery;

    //To stop too many queries in one update
    const int MAX_QUERIES = 100;
    public void OnCreate(ref SystemState state)
    {
        _bucket = 0;

        var config = ConfigLoader.LoadSim();
        _maxBucket = config.navBucketCount;
        _navMeshWorld = NavMeshWorld.GetDefaultWorld();
        _navQueries = new NativeList<NavMeshQuery>(Allocator.Persistent);
    }


    public void OnDestroy(ref SystemState state)
    {
        foreach (var q in _navQueries)
        {
            try
            {
                q.Dispose();
            }
            catch
            {

            }

        }
        _navQueries.Dispose();
    }
    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        int count = 0;
        // Ensure nav world handle is valid
        
        foreach (var (pather, transform, entity) in SystemAPI.Query<RefRW<Pather>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            //if (count >= MAX_QUERIES) break;
            continue;
            // Skip if nothing to do
            if (!pather.ValueRO.NeedsUpdate || _bucket != pather.ValueRO.Bucket)
                continue;

            // Ensure Query assigned
            if (!pather.ValueRO.QuerySet)
            {
                int newIndex = _navQueries.Length;
                // Create a NavMeshQuery with a sane max node capacity (adjust if needed)
                var q = new NavMeshQuery(_navMeshWorld, Allocator.Persistent, 1024);
                _navQueries.Add(q);

                pather.ValueRW.QuerySet = true;
                pather.ValueRW.QueryIndex = newIndex;

                // update local copy (we will write back via ECB at end)
            }

            pather.ValueRW.NeedsUpdate = false;
            pather.ValueRW.PathCalculated = true;

            // Do the pathfinding synchronously on main thread using the entity's NavMeshQuery
            TryCalculatePathAndWriteResults(entity, pather, transform.ValueRO.Position, ref ecb);
            count++;
        }
        UnityEngine.Debug.Log($"Calculated {count} paths.");
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        _bucket += 1;
        if (_bucket > _maxBucket) _bucket = 0;

    }

    /// <summary>
    /// Synchronously calculate path for a single entity using its assigned NavMeshQuery,
    /// then write the results into the entity's PatherWayPoint buffer and update the Pather component.
    /// </summary>
    /// [BurstCompile]
    private void TryCalculatePathNew(Entity entity, RefRW<Pather> pather, float3 fromPosition)
    {
        
    }
    //[BurstCompile]
    private void TryCalculatePathAndWriteResults(Entity entity, RefRW<Pather> pather, float3 fromPosition, ref EntityCommandBuffer ecb)
    {
        //UnityEngine.Debug.Log("Calculate Path");
        if (!pather.ValueRO.QuerySet) return; // safety

        int qIndex = pather.ValueRO.QueryIndex;
        if (qIndex < 0 || qIndex >= _navQueries.Length) return;

        NavMeshQuery query = _navQueries[qIndex];

        // Map positions
        float3 toPosition = pather.ValueRO.Dest;
        var extents = new float3(1f, 1f, 1f);

        var fromLocation = query.MapLocation(fromPosition, extents, 0);
        var toLocation = query.MapLocation(toPosition, extents, 0);

        if (!query.IsValid(fromLocation) || !query.IsValid(toLocation))
        {
            // mark path failed - keep PathCalculated false
            return;
        }

        var status = query.BeginFindPath(fromLocation, toLocation);
        if (status != PathQueryStatus.InProgress && status != PathQueryStatus.Success)
            return;

        // We'll let the query run a limited number of iterations (tweak as needed)
        status = query.UpdateFindPath(500, out _);
        if (status != PathQueryStatus.Success && status != PathQueryStatus.InProgress)
            return;

        status = query.EndFindPath(out int pathSize);
        if (status != PathQueryStatus.Success)
            return;

        if (pathSize <= 0)
            return;

        // gather polygon ids
        var polygonIds = new NativeArray<PolygonId>(pathSize + 1, Allocator.Temp);
        query.GetPathResult(polygonIds);

        // Prepare arrays for straight-path extraction
        var straightResult = new NativeArray<NavMeshLocation>(pathSize + 1, Allocator.Temp);
        var straightFlags = new NativeArray<StraightPathFlags>(pathSize + 1, Allocator.Temp);
        var vertexSide = new NativeArray<float>(pathSize + 1, Allocator.Temp);

        int straightCount = 0;
        var pathStatus = PathUtils.FindStraightPath(
            query,
            fromPosition,
            toPosition,
            polygonIds,
            pathSize,
            ref straightResult,
            ref straightFlags,
            ref vertexSide,
            ref straightCount,
            straightResult.Length
        );

        if (pathStatus != PathQueryStatus.Success || straightCount <= 0)
        {
            polygonIds.Dispose();
            straightResult.Dispose();
            straightFlags.Dispose();
            vertexSide.Dispose();

            pather.ValueRW.PathCalculated = false;
            pather.ValueRW.WaypointIndex = 0;
            return;
        }


        ecb.SetBuffer<PatherWayPoint>(entity);

        // Append all straightResult locations
        for (int i = 0; i < straightCount; i++)
        {
            var loc = straightResult[i];
            float3 pos = new float3(loc.position.x, loc.position.y, loc.position.z);

            // simple sanity check
            if (math.any(pos != float3.zero))
            {
                ecb.AppendToBuffer(entity, new PatherWayPoint { Position = pos });
            }
        }

        // Mark pather as calculated and update other fields
        pather.ValueRW.PathCalculated = true;
        pather.ValueRW.WaypointIndex = 0;
        straightResult.Dispose();
        straightFlags.Dispose();
        vertexSide.Dispose();
    }
}
*/