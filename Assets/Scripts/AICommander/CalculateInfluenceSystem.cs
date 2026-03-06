using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace AICommander
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CalculateInfluenceSystem : ISystem
    {
        const int MAX_ALLOC = 4096*2;
        private int _tickFrequency;
        private int _curTick;
        private bool _initialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _tickFrequency = 100;
            _curTick = 0;
            _initialized = false;

            state.EntityManager.CreateSingleton(new InfluenceMap
            {
                MapNodes = new FixedList4096Bytes<InfluenceMapNode>()
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<MapData>(out var mapData))
                return;

            if (!_initialized)
            {
                _initialized = true;
                int mapSize = mapData.Size.x;
                SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes =
                    InfluenceMapUtil.BuildMap(mapSize);
            }

            if (_curTick % _tickFrequency == 0)
            {
                int mapSize = mapData.Size.x;
                int gridSize = mapSize / InfluenceMapUtil.NODE_SIZE;
                int totalNodes = gridSize * gridSize;

                // Step 1: Collect unit data into spatial map via IJobEntity
                var spatialMap = new NativeParallelMultiHashMap<int2, UnitInfluenceData>(
                    MAX_ALLOC, Allocator.TempJob);

                var collectJob = new CollectUnitsJob
                {
                    SpatialMap = spatialMap.AsParallelWriter()
                };
                // Schedule against all entities with LocalTransform + Team
                var collectHandle = collectJob.ScheduleParallel(state.Dependency);

                // Step 2: Calculate influence map from spatial data
                var outputNodes = new NativeArray<InfluenceMapNode>(totalNodes, Allocator.TempJob);

                var calcJob = new CalculateInfluenceMapJob
                {
                    MapSize = mapSize,
                    SpatialMap = spatialMap,
                    OutputNodes = outputNodes
                };
                var calcHandle = calcJob.Schedule(collectHandle);
                calcHandle.Complete();

                // Write results back to singleton
                var map = new FixedList4096Bytes<InfluenceMapNode>();
                for (int i = 0; i < outputNodes.Length; i++)
                    map.Add(outputNodes[i]);

                SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes = map;

                outputNodes.Dispose();
                spatialMap.Dispose();

                state.Dependency = calcHandle;
            }

            _curTick++;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
    // Replaces both the manual foreach loop AND BuildSpatialHashMapJob
    [BurstCompile]
    public partial struct CollectUnitsJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int2, UnitInfluenceData>.ParallelWriter SpatialMap;

        // Automatically queries entities with these components
        public void Execute(RefRO<LocalTransform> transform, RefRO<Team> team)
        {
            int2 pos = BMath.FlatPosition(transform.ValueRO.Position);
            int2 cell = pos / InfluenceMapUtil.NODE_SIZE;

            SpatialMap.Add(cell, new UnitInfluenceData
            {
                Position = pos,
                TeamID = (sbyte)team.ValueRO.TeamID
            });
        }
    }

    public struct UnitInfluenceData
    {
        public int2 Position;
        public sbyte TeamID;
    }

    [BurstCompile]
    public struct CalculateInfluenceMapJob : IJob
    {
        [ReadOnly] public int MapSize;
        [ReadOnly] public NativeParallelMultiHashMap<int2, UnitInfluenceData> SpatialMap;
        public NativeArray<InfluenceMapNode> OutputNodes;

        public void Execute()
        {
            int gridSize = MapSize / InfluenceMapUtil.NODE_SIZE;
            int totalNodes = gridSize * gridSize;

            FixedList128Bytes<int> strengthCount = new FixedList128Bytes<int>();
            for (int t = 0; t < 15; t++)
                strengthCount.Add(0);

            for (int i = 0; i < totalNodes; i++)
            {
                for (int t = 0; t < strengthCount.Length; t++)
                    strengthCount[t] = 0;

                int2 nodeCenter = InfluenceMapUtil.GetPositionOfNode(i, gridSize);
                int2 nodeCell = nodeCenter / InfluenceMapUtil.NODE_SIZE;

                if (SpatialMap.TryGetFirstValue(nodeCell, out UnitInfluenceData influ, out var it))
                {
                    do { strengthCount[influ.TeamID] += 1; }
                    while (SpatialMap.TryGetNextValue(out influ, ref it));
                }

                int strongest = 0;
                int strongestTeam = -1;
                for (int t = 0; t < strengthCount.Length; t++)
                {
                    if (strengthCount[t] > strongest)
                    {
                        strongest = strengthCount[t];
                        strongestTeam = t;
                    }
                }

                if (strongest >= 255) strongest = 255;

                OutputNodes[i] = new InfluenceMapNode
                {
                    TeamFavor = (sbyte)strongestTeam,
                    Strength = (byte)strongest
                };
            }
        }
    }
}