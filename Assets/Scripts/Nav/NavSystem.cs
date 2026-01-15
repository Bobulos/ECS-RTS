using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Experimental.AI; // NavMesh types
using UnityEngine; // for Debug
using NUnit;
using Unity.Entities.UniversalDelegates;

public struct Pather : IComponentData
{
    public bool NeedsUpdate;
    public float3 Dest;
    public float IndexDistance;
    public int WaypointIndex;
    public bool PathCalculated;

    public bool QuerySet;

    public int QueryIndex;

    public uint Bucket;
}

[InternalBufferCapacity(32)]
public struct PatherWayPoint : IBufferElementData
{
    public float3 Position;
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct NavSystem : ISystem
{
    // Persistent storage for NavMeshQuery objects. Index -> NavMeshQuery.
    // Each pather entity will own one query index.

    //DEPRECATED
    //private NativeList<NavMeshQuery> _navQueries;
    //private NavMeshWorld _navMeshWorld;

    private int _bucket;
    private int _maxBucket;
    // Keep a list of entities that need pathing this frame (temporary each update)ss
    private EntityQuery _patherQuery;

    //To stop too many queries in one update
    const int MAX_QUERIES = 10000;
    
    public void OnCreate(ref SystemState state)
    {
        _bucket = 0;

        var config = ConfigLoader.Load<SimulationConfig>("SimulationConfig");
        _maxBucket = config.TargetBucketCount;
    }

    public void OnDestroy(ref SystemState state)
    {
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        //test declaration of pathing job
        var pathJob = new NavJob
        {
            Bucket = _bucket,
            Ecb = ecb,
            NavWorld = NavMeshWorld.GetDefaultWorld();
        }
        var handle = pathJob.Schedule(state.Dependency);
        handle.Complete();
        // Playback changes
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
    private void TryCalculatePathAndWriteResults(Entity entity, RefRW<Pather>pather, float3 fromPosition, EntityCommandBuffer ecb)
    {
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
            // path failed or no straight path
            //lost unit for now 
            pather.ValueRW.PathCalculated = true;
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
