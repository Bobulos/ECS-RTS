using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
public struct PatherCleanup : ICleanupComponentData
{
    public int QuerieIndex;
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct NavSystem : ISystem
{
    private NavMeshWorld _navWorld;
    //private NavMeshQuery _query;

    // hard cap so nav never spikes a frame
    private const int MAX_PATHS_PER_FRAME = 4;
    private int _bucket;
    private int _maxBucket;


    private NativeArray<NavMeshQuery> _queries;
    private NativeArray<byte> _queryUsed;
    private int _maxQueries;
    //[BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _navWorld = NavMeshWorld.GetDefaultWorld();

        _maxQueries = ConfigLoader.LoadSim().maxNavQueries;
        _queries = new NativeArray<NavMeshQuery>(_maxQueries, Allocator.Persistent);
        _queryUsed = new NativeArray<byte>(_maxQueries, Allocator.Persistent);
    }
    [BurstCompile]
    private int AllocateQuery(ref EntityCommandBuffer ecb, Entity e)
    {
        for (int i = 0; i < _maxQueries; i++)
        {
            if (_queryUsed[i] == 0)
            {
                _queryUsed[i] = 1;
                _queries[i] = new NavMeshQuery(_navWorld, Allocator.Persistent, 512);
                ecb.AddComponent(e, new PatherCleanup { QuerieIndex = i });
                return i;
            }
        }

        UnityEngine.Debug.LogError("Out of NavMeshQueries!");
        return -1;
    }
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        for (int i = 0; i < _maxQueries; i++)
        {
            if (_queryUsed[i] == 1)
            {
                _queries[i].Dispose();
            }
        }

        _queries.Dispose();
        _queryUsed.Dispose();
    }
    [BurstCompile]
    private void FreeQuery(int index)
    {
        if (_queryUsed[index] == 1)
        {
            _queries[index].Dispose();
            _queryUsed[index] = 0;
        }
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        var ecbSystem =
            SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (transform, p, e) in
            SystemAPI.Query<RefRO<LocalTransform>, 
            RefRW<Pather>>()
            .WithEntityAccess())
        {
            if (!p.ValueRO.QuerySet)
            {
                int q = AllocateQuery(ref ecb, e);
                if (q < 0) continue;

                p.ValueRW.QuerySet = true;
                p.ValueRW.QueryIndex = q;
                continue;
            }

            if (!p.ValueRO.NeedsUpdate)
                continue;

            var job = new NavQueryJob
            {
                FromPos = transform.ValueRO.Position,
                ToPos = p.ValueRO.Dest,
                RPather = p.ValueRO,
                REntity = e,
                Query = _queries[p.ValueRO.QueryIndex],
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged)
            };

            state.Dependency = job.Schedule(state.Dependency);
        }

        foreach (var (p,e) in SystemAPI.Query<PatherCleanup>()
            .WithEntityAccess()
            .WithNone<Pather>())
        {
            FreeQuery(p.QuerieIndex);
            ecb.DestroyEntity(e);
        }
        //ecbSystem.AddJobHandleForProducer(state.Dependency);
    }

}

[BurstCompile]
public struct NavQueryJob : IJob
{
    public float3 FromPos;
    public float3 ToPos;


    //public NativeReference<Pather> Pather;
    //will set with ecb
    public Pather RPather;
    public Entity REntity;

    public NavMeshQuery Query;
    
    public EntityCommandBuffer Ecb;


    //public int Bucket;
    private const int MAXIT = 512/2;

    //[ReadOnly] public NavMeshWorld World; 
    [BurstCompile]
    public void Execute()
    {
        //UnityEngine.Debug.Log($"Index of {index}");
        //ignore buckets for know || pather.Bucket != Bucket
        TryCalculatePath(REntity, out bool calculated, out bool inValid);
        RPather.WaypointIndex = 0;
        RPather.PathCalculated = true;
        RPather.NeedsUpdate = false;
       //UnityEngine.Debug.Log($"Path calculated {calculated }");

        //straight path addition
        if (!calculated && !inValid)
        {
            Ecb.SetBuffer<PatherWayPoint>(REntity);
            Ecb.AppendToBuffer<PatherWayPoint>(REntity, new PatherWayPoint { Position = ToPos });
        }
        
        Ecb.SetComponent(REntity, RPather);



        
        //UnityEngine.Debug.Log("164");

        //Index.Value = index+1;
    }
    [BurstCompile]
    private void TryCalculatePath(
    Entity entity,
    out bool calculated,
    out bool inValid)
    {
        calculated = false;
        inValid = false;
        //float3 toPosition = pather.Dest;
        float3 extents = new float3(1, 2, 1);

        var fromLoc = Query.MapLocation(FromPos, extents, 0);
        var toLoc = Query.MapLocation(ToPos, extents, 0);

        if (!Query.IsValid(fromLoc) || !Query.IsValid(toLoc))
        {
            inValid = true;
            //UnityEngine.Debug.Log("180");
            return;
        }

        if (Query.BeginFindPath(fromLoc, toLoc) != PathQueryStatus.InProgress)
        {
            //UnityEngine.Debug.Log("187");
            return;
        }

        var status = Query.UpdateFindPath(MAXIT, out _);
        if (status != PathQueryStatus.Success && status != PathQueryStatus.InProgress)
        {
            //calculated = true;
            //UnityEngine.Debug.Log("195");
            return;
        }

        if (Query.EndFindPath(out int pathSize) != PathQueryStatus.Success || pathSize == 0)
        {
            
            //UnityEngine.Debug.Log("202");
            inValid = true;
            return;
        }

        var polys = new NativeArray<PolygonId>(pathSize + 1, Allocator.Temp);
        Query.GetPathResult(polys);

        var straight = new NativeArray<NavMeshLocation>(pathSize + 1, Allocator.Temp);
        var flags = new NativeArray<StraightPathFlags>(pathSize + 1, Allocator.Temp);
        var sides = new NativeArray<float>(pathSize + 1, Allocator.Temp);

        int straightCount = 0;

        var straightStatus = PathUtils.FindStraightPath(
            Query,
            FromPos,
            ToPos,
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
            calculated = true;
            //UnityEngine.Debug.Log("EVIL");
            return;
        }

        // clear + write waypoints
        Ecb.SetBuffer<PatherWayPoint>(entity);

        for (int i = 0; i < straightCount; i++)
        {
            float3 pos = straight[i].position;
            Ecb.AppendToBuffer(entity, new PatherWayPoint { Position = pos });
        }

        straight.Dispose();
        flags.Dispose();
        sides.Dispose();

        calculated = true;
        //UnityEngine.Debug.Log("254");
        
    }
}