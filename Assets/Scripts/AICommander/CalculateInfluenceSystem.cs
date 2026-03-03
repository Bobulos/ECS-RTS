using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct CalculateInfluenceSystem : ISystem
{
    private int _tickFrequency;
    private int _curTick;
    private bool _influenceInitialized;
    private NativeList<UnitInfluenceData> _influenceData;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _influenceData = new NativeList<UnitInfluenceData>(1024, Allocator.Persistent);
        _tickFrequency = 50;
        _curTick = 0;
        _influenceInitialized = false;

        state.EntityManager.CreateSingleton(new InfluenceMap
        {
            MapNodes = new FixedList4096Bytes<InfluenceMapNode>()
        });
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_influenceInitialized)
        {
            int mapSize = SystemAPI.GetSingleton<MapData>().Size.x;
            SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes =
                InfluenceMapUtil.BuildMap(mapSize);
            _influenceInitialized = true;
        }

        if (_curTick % _tickFrequency == 0)
        {
            _influenceData.Clear();

            foreach (var (pos, team) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<Team>>())
            {
                _influenceData.Add(new UnitInfluenceData
                {
                    Position = BMath.FlatPosition(pos.ValueRO.Position),
                    TeamID = (sbyte)team.ValueRO.TeamID
                });
            }

            int mapSize = SystemAPI.GetSingleton<MapData>().Size.x;

            // Build spatial hashmap: gridCell -> list index isn't possible directly,
            // so we use int2->UnitInfluenceData with a MultiHashMap for multiple units per cell
            var spatialMap = new NativeParallelMultiHashMap<int2, UnitInfluenceData>(
                _influenceData.Length, Allocator.TempJob);

            // Populate spatial hashmap job
            var buildMapJob = new BuildSpatialHashMapJob
            {
                Units = _influenceData.AsArray(),
                SpatialMap = spatialMap.AsParallelWriter()
            };
            var buildHandle = buildMapJob.Schedule(_influenceData.Length, 64);

            // Get writable map nodes ref
            var mapNodes = SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes;

            var calcJob = new CalculateInfluenceMapJob
            {
                MapSize = mapSize,
                SpatialMap = spatialMap,
                MapNodes = mapNodes
            };
            var calcHandle = calcJob.Schedule(buildHandle);
            calcHandle.Complete();

            SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes = calcJob.MapNodes;

            spatialMap.Dispose();
        }

        _curTick++;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _influenceData.Dispose();
    }
}

public struct UnitInfluenceData
{
    public int2 Position;
    public sbyte TeamID;
}

[BurstCompile]
public struct BuildSpatialHashMapJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<UnitInfluenceData> Units;
    public NativeParallelMultiHashMap<int2, UnitInfluenceData>.ParallelWriter SpatialMap;

    public void Execute(int index)
    {
        var unit = Units[index];
        // Key by grid cell so nearby node lookups are O(1)
        int2 cell = unit.Position / InfluenceMapUtil.NODE_SIZE;
        SpatialMap.Add(cell, unit);
    }
}

[BurstCompile]
public struct CalculateInfluenceMapJob : IJob
{
    [ReadOnly] public int MapSize;
    [ReadOnly] public NativeParallelMultiHashMap<int2, UnitInfluenceData> SpatialMap;
    public FixedList4096Bytes<InfluenceMapNode> MapNodes;

    // How many grid cells to search in each direction
    private const int INFLUENCE_RADIUS_CELLS = 1;

    public void Execute()
    {
        int halfNodeSize = InfluenceMapUtil.NODE_SIZE / 2;
        int sqMapSize = MapSize * MapSize;

        for (int i = 0; i < sqMapSize; i++)
        {
            int2 nodeCenter = InfluenceMapUtil.GetPositionOfNode(i, MapSize);
            int2 nodeCell = nodeCenter / InfluenceMapUtil.NODE_SIZE;

            sbyte favor = 0;
            byte strength = 0;

            // Search surrounding cells in the hashmap instead of all units
            for (int dx = -INFLUENCE_RADIUS_CELLS; dx <= INFLUENCE_RADIUS_CELLS; dx++)
            {
                for (int dz = -INFLUENCE_RADIUS_CELLS; dz <= INFLUENCE_RADIUS_CELLS; dz++)
                {
                    int2 searchCell = nodeCell + new int2(dx, dz);

                    if (!SpatialMap.TryGetFirstValue(searchCell, out var unit, out var it))
                        continue;

                    do
                    {
                        uint dist = BMath.ManhattanDist2D(nodeCenter, unit.Position);
                        if (dist <= halfNodeSize * INFLUENCE_RADIUS_CELLS)
                        {
                            // Closer units contribute more strength
                            byte contribution = (byte)math.max(1, 10 - dist / halfNodeSize);
                            strength = (byte)math.min(255, strength + contribution);
                            favor = (sbyte)math.clamp(favor + unit.TeamID * contribution, -127, 127);
                        }
                    }
                    while (SpatialMap.TryGetNextValue(out unit, ref it));
                }
            }

            MapNodes[i] = new InfluenceMapNode
            {
                TeamFavor = favor,
                Strength = strength
            };
        }
    }
}