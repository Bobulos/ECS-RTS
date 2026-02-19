using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine.Experimental.AI; // NavMesh types


/// <summary>
/// Probalbly all these shouldnt be synced causing issues deterministic anyway
/// Server owns dest and everything else not to worried abt cheating lmao
/// </summary>
//[GhostComponent]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public struct Pather : IComponentData
{
    public int AgentID;
    public bool NeedsUpdate;
    //[GhostField] 
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

//[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct NavSystem : ISystem
{
    private NavMeshWorld _navWorld;
    private NativeArray<NavMeshQuery> _queries;
    private NativeList<int> _freeIndices;  // Track free slots explicitly
    private int _maxQueries;

    public void OnCreate(ref SystemState state)
    {
        _navWorld = NavMeshWorld.GetDefaultWorld();
        _maxQueries = SimConfigLoader.LoadSim().maxNavQueries;

        _queries = new NativeArray<NavMeshQuery>(_maxQueries, Allocator.Persistent);
        _freeIndices = new NativeList<int>(_maxQueries, Allocator.Persistent);

        // Initialize all as free
        for (int i = 0; i < _maxQueries; i++)
        {
            _freeIndices.Add(i);
        }
    }

    [BurstCompile]
    private int AllocateQuery(ref EntityCommandBuffer ecb, Entity e)
    {
        if (_freeIndices.Length == 0)
        {
            UnityEngine.Debug.LogError("Out of NavMeshQueries!");
            return -1;
        }

        // Get a free index
        int index = _freeIndices[_freeIndices.Length - 1];
        _freeIndices.RemoveAt(_freeIndices.Length - 1);

        // Create the query
        _queries[index] = new NavMeshQuery(_navWorld, Allocator.Persistent, 512);
        ecb.AddComponent(e, new PatherCleanup { QuerieIndex = index });

        return index;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        int numDisposed = 0;
        // Dispose ALL queries that were ever allocated
        for (int i = 0; i < _maxQueries; i++)
        {
            // Check if this index is NOT in the free list (meaning it's allocated)
            bool isFree = false;
            for (int j = 0; j < _freeIndices.Length; j++)
            {
                if (_freeIndices[j] == i)
                {
                    isFree = true;
                    break;
                }
            }

            if (!isFree)
            {
                numDisposed ++;
                _queries[i].Dispose();
            }
        }

        _queries.Dispose();
        _freeIndices.Dispose();

        UnityEngine.Debug.Log($"Disposed of {numDisposed} Nav Queries");
    }

    [BurstCompile]
    private void FreeQuery(int index)
    {
        // Dispose the query
        _queries[index].Dispose();

        // Add index back to free list
        _freeIndices.Add(index);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

        // Handle cleanup
        foreach (var (cleanup, e) in SystemAPI.Query<RefRO<PatherCleanup>>()
            .WithEntityAccess()
            .WithNone<Pather>())
        {
            FreeQuery(cleanup.ValueRO.QuerieIndex);
            ecb.RemoveComponent<PatherCleanup>(e);
        }

        // Handle path requests
        foreach (var (transform, p, e) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRW<Pather>>()
            .WithEntityAccess().WithNone<DeadTag>())
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
            //job.Schedule();
            state.Dependency = job.Schedule(state.Dependency);
        }
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
            Ecb.AppendToBuffer(REntity, new PatherWayPoint { Position = ToPos });
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
        float3 extents = new float3(10f, 10f, 10f);


        //Add agent types later
        var fromLoc = Query.MapLocation(FromPos, extents, RPather.AgentID);
        var toLoc = Query.MapLocation(ToPos, extents, RPather.AgentID);

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