using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Experimental.AI; // NavMesh types
using UnityEngine; // for Debug
using NUnit;
using Unity.Entities.UniversalDelegates;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public partial struct NavJob : IJobEntity
{
    const int MAX_NODE_LENGTH = 1024;

    const int MAX_QUERIES = 10000;
    //does not support parrallel MUST BE ON ONE THREAD

    const int MAX_ITERATIONS = 500;


    //TS NEEDS TO BE ASSIGNED
    public EntityCommandBuffer Ecb;
    public int QueryCount;
    [ReadOnly] public int Bucket;

    //o lawd
    [NativeDisableUnsafePtrRestriction]
    [ReadOnly] public NavMeshWorld NavWorld;

    public void Execute(Entity entity, RefRW<Pather> pather, RefRO<LocalTransform> transform)
    {

        //make sure agent needs it
        if (QueryCount > MAX_QUERIES || !pather.ValueRO.NeedsUpdate || 
        pather.ValueRO.Bucket != Bucket) return;

        var query = new NavMeshQuery(NavWorld, Allocator.Temp, MAX_NODE_LENGTH);

        float3 toPosition = pather.ValueRO.Dest;
        var extents = new float3(1f, 1f, 1f);

        var fromLocation = query.MapLocation(transform.ValueRO.Position, extents, 0);
        var toLocation = query.MapLocation(pather.ValueRO.Dest, extents, 0);

        if (!query.IsValid(fromLocation) || !query.IsValid(toLocation))
        {
            // mark path failed - keep PathCalculated false
            query.Dispose();
            return;
        }

        var status = query.BeginFindPath(fromLocation, toLocation);
        if (status != PathQueryStatus.InProgress && status != PathQueryStatus.Success)
        {
            query.Dispose();
            return;
        }

        // We'll let the query run a limited number of iterations (tweak as needed)
        status = query.UpdateFindPath(MAX_ITERATIONS, out _);
        if (status != PathQueryStatus.Success && status != PathQueryStatus.InProgress)
        {
            query.Dispose();
            return;
        }

        status = query.EndFindPath(out int pathSize);
        if (status != PathQueryStatus.Success || pathSize <= 0)
        {
            query.Dispose();
            return;
        }


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
            transform.ValueRO.Position,
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


            straightResult.Dispose();
            straightFlags.Dispose();
            vertexSide.Dispose();
            //mabeye do this mabeye fix
            polygonIds.Dispose();
            query.Dispose();
            return;
        }

        Ecb.SetBuffer<PatherWayPoint>(entity);

        // Append all straightResult locations
        for (int i = 0; i < straightCount; i++)
        {
            var loc = straightResult[i];
            float3 pos = new float3(loc.position.x, loc.position.y, loc.position.z);

            // simple sanity check
            if (math.any(pos != float3.zero))
            {
                Ecb.AppendToBuffer(entity, new PatherWayPoint { Position = pos });
            }
        }

        // Mark pather as calculated and update other fields
        pather.ValueRW.PathCalculated = true;
        pather.ValueRW.NeedsUpdate = false;
        pather.ValueRW.WaypointIndex = 0;
        straightResult.Dispose();
        straightFlags.Dispose();
        vertexSide.Dispose();
        //mabeye do this mabeye fix
        polygonIds.Dispose();
        query.Dispose();

        QueryCount ++;
    }
}