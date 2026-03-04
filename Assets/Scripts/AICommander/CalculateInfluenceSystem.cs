using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;
using System;
using UnityEditor.Experimental.GraphView;
namespace AICommander
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CalculateInfluenceSystem : ISystem
    {
        private int _tickFrequency;
        private int _curTick;
        private bool _initialized;
        private NativeList<UnitInfluenceData> _influenceData;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            UnityEngine.Debug.Log($"CalculateInfluenceSystem created");
            //state.RequireForUpdate<MapData>();
            _influenceData = new NativeList<UnitInfluenceData>(1024, Allocator.Persistent);
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

                UnityEngine.Debug.Log($"Initializing influence map...");
                int mapSize = SystemAPI.GetSingleton<MapData>().Size.x;
                SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes =
                    InfluenceMapUtil.BuildMap(mapSize);
                
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
                //var mapNodes = SystemAPI.GetSingletonRW<InfluenceMap>().ValueRW.MapNodes;
                var mapSingleton = SystemAPI.GetSingletonRW<InfluenceMap>();
                var nodes = mapSingleton.ValueRW.MapNodes;

                var calcJob = new CalculateInfluenceMapJob
                {
                    MapSize = mapSize,
                    SpatialMap = spatialMap,
                    MapNodes = nodes
                };
                
                var calcHandle = calcJob.Schedule(buildHandle);
                calcHandle.Complete();

                mapSingleton.ValueRW.MapNodes = calcJob.MapNodes;

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
            //int halfNodeSize = InfluenceMapUtil.NODE_SIZE / 2;
            int gridSize = MapSize / InfluenceMapUtil.NODE_SIZE;
            int totalNodes = gridSize * gridSize;
            
            //starting at 0 index matched team
            // -1 is nuetral so wont count
            FixedList128Bytes<int> strengthCount = new FixedList128Bytes<int>();
            for (int t = 0; t < 15; t++)
                strengthCount.Add(0);

            for (int i = 0; i < totalNodes; i++)
            {
                int2 nodeCenter = InfluenceMapUtil.GetPositionOfNode(i, gridSize);
                int2 nodeCell = nodeCenter / InfluenceMapUtil.NODE_SIZE;



                

                if (SpatialMap.TryGetFirstValue( nodeCell, out UnitInfluenceData influ, out var it))
                {
                    do
                    {
                        strengthCount[influ.TeamID]+=1;
                        //UnityEngine.Debug.Log("Unit in hash");
                    }while (SpatialMap.TryGetNextValue(out influ, ref it));
                }
                //handles 15 teams
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
                // Bind to byte size
                if (strongest >= 255) strongest = 255;

                sbyte favor = (sbyte)strongestTeam;
                MapNodes[i] = new InfluenceMapNode
                {
                    TeamFavor = favor,
                    Strength = (byte)strongest
                };
            }
        }
    }
}
