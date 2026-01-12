using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.VisualScripting;
using System.Linq;

public struct UnitData
{
    public float3 Position;
    public float2 Velocity;
    public float Radius;
    public int TeamID;
    public Entity Entity;
}
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)),UpdateBefore(typeof(UnitMovementSystem))]
[BurstCompile]
public partial struct UnitSpatialPartitioning : ISystem
{
    // Persistent container reused across frames to avoid per-frame allocations.
    private NativeList<UnitData> _unitData;
    private EntityQuery _query;
    private NativeParallelMultiHashMap<int, UnitData> _spatialMap;
    private uint _bucket;
    private uint _maxBucket;

    public void OnCreate(ref SystemState state)
    {

        //SimulationSettings
        var config = ConfigLoader.Load<SimulationConfig>("SimulationConfig");
        _maxBucket = (uint)config.TargetBucketCount;

        _bucket = 0;
        _query = state.GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadWrite<UnitTarget>(),
            ComponentType.ReadOnly<UnitTeam>(),
            ComponentType.ReadOnly<UnitMovement>()
        );

        // Initialize persistent list. Reserve some capacity to reduce resizing churn.
        _unitData = new NativeList<UnitData>(16, Allocator.Persistent);
        _spatialMap = new NativeParallelMultiHashMap<int, UnitData>(config.SpatialPartitionTargetCount, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        _unitData.Dispose();
        _spatialMap.Dispose();
//        if (_unitData.IsCreated)
            
    }
    public void OnUpdate(ref SystemState state)
    {
        _unitData.Clear();


        int unitCount = _query.CalculateEntityCount();

        if (_unitData.Capacity < unitCount)
            _unitData.Capacity = unitCount;
        _spatialMap.Clear();
        if (_spatialMap.Capacity < unitCount)
            _spatialMap.Capacity = unitCount;

        // This is cheap memory-wise because we reuse the list memory every frame.
        foreach (var (team, mov, transform, entity) in SystemAPI.Query<UnitTeam,UnitMovement,LocalTransform>().WithEntityAccess())
        {
            unitCount ++;
            UnitData ud = new UnitData
            {
                Entity = entity,
                Position = transform.Position,
                Velocity = mov.Velocity,
                Radius = mov.Radius,
                TeamID = team.TeamID,
            };
            _unitData.Add(ud);
            int hashKey = SpatialHash.GetHashKey(transform.Position);
            _spatialMap.Add(hashKey, ud);
        }

        var dedLookup = SystemAPI.GetComponentLookup<DeadTag>(true);

        var job = new FindTargetsJob
        {
            T = SystemAPI.Time.ElapsedTime,
            DeadLookup = dedLookup,
            UnitSpatialMap = _spatialMap,
            Bucket = _bucket,
        };

        // Schedule the IJobEntity in parallel. We do NOT call Complete() — let the scheduler run it async.
        // The returned JobHandle is stored in state.Dependency so that subsequent jobs/systems respect it.
        
        JobHandle handle = job.ScheduleParallel(state.Dependency);
        state.Dependency = handle;
        handle.Complete();
        

        //DO ALL OF THE LOCAL AVOIDANCE DOWN HERE
        var avoidanceJob = new UnitLocalAvoidanceJob
        {
            SpatialMap = _spatialMap,
            CellSize = SpatialHash.CellSize,
            TimeHorizon = 2.5f,
        };

        avoidanceJob.Schedule();
        _bucket += 1;
        if (_bucket > _maxBucket) { _bucket = 0; }
    }
}
[BurstCompile]
public partial struct FindTargetsJob : IJobEntity
{
    [ReadOnly] public uint Bucket;
    [ReadOnly] public double T;
    [ReadOnly] public ComponentLookup<DeadTag> DeadLookup;
    [ReadOnly] public NativeParallelMultiHashMap<int, UnitData> UnitSpatialMap;

    public void Execute(Entity entity, ref LocalTransform transform, in UnitTeam team, ref UnitTarget target)
    {
        if (target.Bucket == Bucket)
        {
            float min = target.Range * target.Range;
            float sqrRange = min;
            Entity closest = Entity.Null;

            int cellX = (int)math.floor(transform.Position.x / SpatialHash.CellSize);
            int cellZ = (int)math.floor(transform.Position.z / SpatialHash.CellSize);

            // 3x3 neighborhood
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int nx = cellX + dx;
                    int nz = cellZ + dz;
                    int hashKey = (nx * 73856093) ^ (nz * 19349663);
                    if (UnitSpatialMap.TryGetFirstValue(hashKey, out UnitData u, out var it))
                    {
                        do
                        {
                            if (u.Entity == entity ||
                                u.TeamID == team.TeamID ||
                                DeadLookup.HasComponent(u.Entity))
                                continue;

                            float d = DistsqXZ(transform.Position, u.Position);

                            if (d <= sqrRange && d < min)
                            {
                                min = d;
                                closest = u.Entity;
                            }
                        }
                        while (UnitSpatialMap.TryGetNextValue(out u, ref it));
                    }
                }
            }

            target.Targ = closest;
            target.DistSq = min;
        }
    }
    public float DistsqXZ(float3 a, float3 b)
    {
        a.y = 0;
        b.y = 0;
        return math.distancesq(a, b);
    }
}
public static class SpatialHash
{
    // Define the size of your grid cells (e.g., 10x10 meters)
    public const float CellSize = 5f;

    // Calculates a unique integer key for a 2D position.
    public static int GetHashKey(float3 position)
    {
        // Calculate the grid coordinates
        int x = (int)math.floor(position.x / CellSize);
        int z = (int)math.floor(position.z / CellSize);

        // Simple spatial hash function: prime numbers prevent common artifacts
        return (x * 73856093) ^ (z * 19349663);
    }
}