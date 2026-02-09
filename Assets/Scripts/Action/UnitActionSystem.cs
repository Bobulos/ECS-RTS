using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using ConstructionMan;
[InternalBufferCapacity(16)]
public struct ConstructRequest : IBufferElementData
{   
    public float3 Position;
    //add end pos later
    public ConstructionDataBaked Data;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
partial class UnitActionSystem : SystemBase
{
    const float MAX_RAY_LENGTH = 300f;
    const float UNIT_RADIUS_MULTIPLIER = 0.9f;

    private CollisionFilter TERRAIN_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    
    private CollisionFilter STRUCTURE_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 8,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    protected override void OnCreate()
    {
        //_count = 100;
        UnitActionManager.OnAction += OnAction;
        // EntityManager.CreateSingletonBuffer<ConstructRequest>();
        // var entity = EntityManager.CreateEntity(typeof(ConstructRequests));
        // EntityManager.AddBuffer<ConstructRequests>(entity);

    }
    protected override void OnDestroy()
    {
        UnitActionManager.OnAction -= OnAction;
    }
    private void OnAction(ActionData action, int team)
    {
        switch (action.Info.ActionType)
        {
            case  ActionType.AddUnitToQueue:
                AddUnitToQueue(action, team);
                break;
            case ActionType.Move:
                Move(action, team);
                break;
            case ActionType.SetRallyPoint:
                SetRallyPoint(action, team);
                break;
            case ActionType.BuildStructure:
                BuildStructure(action, team);
                break;
        }
    }
    protected override void OnUpdate()
    {
    }

    //shared no check for key
    [BurstCompile]
    private void Move(ActionData action, int team)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin, // Ray origin
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,   // Ray end point
            Filter = CollisionFilter.Default // Or a custom filter
        };

        float3 calculatedCenter = float3.zero;

        //assigned after the center has been calculated;
        int unitCount = 0;

        //given 64 to reduce memory churn
        var unitPositions = new NativeList<float3>(64, Allocator.Temp);
        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit movCenter))
        {

            foreach (var transform in 
            SystemAPI.Query<LocalTransform>()
            .WithAll<UnitSelecetedTag>()
            .WithNone<UnitMoveOrder, StructureTag>())
            {
                unitCount++;
                calculatedCenter += transform.Position;
                unitPositions.Add(transform.Position);
            }
            if (unitCount == 0)
            {
                ecb.Dispose();
                unitPositions.Dispose();
                return;
            }

            float calculatedRadius = 0;
            calculatedCenter /= unitCount;
            //calculate avg radius arround center
            foreach (float3 p in unitPositions)
            {
                calculatedRadius += BMath.DistXZ(p, calculatedCenter);
            }

            //average everything out
            calculatedRadius /= unitCount;
            calculatedRadius *= UNIT_RADIUS_MULTIPLIER;

            bool mode = BMath.DistXZ(movCenter.Position, calculatedCenter) < calculatedRadius;

            foreach (var (transform, entity) in SystemAPI.Query<LocalTransform>().WithAll<UnitSelecetedTag>().WithNone<UnitMoveOrder>().WithEntityAccess())
            {
                //if its outside then
                float3 movPos = (transform.Position - calculatedCenter) + movCenter.Position;
                //if its inside then
                if (mode)
                {
                    movPos = (transform.Position - calculatedCenter)/2f + movCenter.Position;
                }
                UnitOrderUtil.UnitMoveOrder(ref ecb, physicsWorld, entity, movPos);
            }
        }

        unitPositions.Dispose();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    [BurstCompile]
    private void SetRallyPoint(ActionData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;


        //Get point hit
        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin,
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,
            Filter = CollisionFilter.Default // Or a custom filter
        };
        if (!world.CastRay(raycastInput, out var hit)) return;

        //UnityEngine.Debug.Log("GIOGIKGO");
        //this is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (key, prod) in SystemAPI.Query<
            RefRO<SelectionKey>,
            RefRW<ProductionStructure>>().WithAll<UnitSelecetedTag>())
        {
            //check that it is the type that needs to be modified
            if (key.ValueRO.Value != targetKey) continue;
            
            prod.ValueRW.RallyPoint = hit.Position;
            //UnityEngine.Debug.Log($"Set rally point to{hit.Position}");
            // Set the structures rally point
        }
    }
    [BurstCompile]
    private void AddUnitToQueue(ActionData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;

        // This is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (key, prod) in SystemAPI.Query<
            RefRO<SelectionKey>,
            RefRW<ProductionStructure>>().WithAll<UnitSelecetedTag>())
        {
            //check that it is the type that needs to be modified
            if (key.ValueRO.Value != targetKey) continue;
            if (prod.ValueRO.QueueCount < prod.ValueRO.QueueSize)

            prod.ValueRW.QueueCount++;
            prod.ValueRW.Queue.Add(prod.ValueRO.Prefabs[action.Info.PrefabIndex]);

            //if it is the first in list need to start cycle
            if (prod.ValueRO.QueueCount == 1) prod.ValueRW.StartTime = time;
        }
    }
    //public static Action s;
    //[BurstCompile]
    private void BuildStructure(ActionData action, int team)
    {
        
        UnityEngine.Debug.Log("Build structure");
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var structures = SystemAPI.GetSingletonBuffer<StructureManifest>();

        //var constructBufferEntity = SystemAPI.GetSingletonEntity<ConstructRequest>();
        var constructionData = SystemAPI.GetSingletonBuffer<ConstructionDataManifest>();

        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin, // Ray origin
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,   // Ray end point
            Filter = CollisionFilter.Default // Or a custom filter
        };

        // This is the first index of the selected units
        if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
        {
            //round the raycast pos
            float3 roundPos = SnapToGrid(physicsWorld, hit.Position);

            int targetKey = selectedUnits.Buckets[0].Key;
            foreach (var (transform, key, work, entity) in SystemAPI.Query<
                RefRO<LocalTransform>,
                RefRO<SelectionKey>,
                RefRW<Worker>>().WithAll<UnitSelecetedTag>().WithEntityAccess())
            {
            //need to add position rounding
            
                //UnityEngine.Debug.Log("GOGOGOGOOG");
                //check that it is the type that needs to be modified
                if (key.ValueRO.Value != targetKey) continue;
                // Get the construction data
                var cD = constructionData[work.ValueRO.ConstructKeys[action.Info.PrefabIndex]];
                
                if (!CheckValidStructurePlacement(physicsWorld, roundPos, cD.Size)) continue;
                
                work.ValueRW.BuildRequest = new ConstructionRequest
                {
                    Dest = roundPos,
                    Size = cD.Size,
                    //Spacing = cD.Spacing,
                    PrimaryKey = cD.PrimaryKey
                };
                work.ValueRW.HasRequest = true;
                //yai
                // var e = ecb.Instantiate(structures[work.ValueRO.ConstructKeys[action.Info.PrefabIndex]].Value);
                // ecb.SetComponent(e, new LocalTransform {Position = hit.Position, Rotation = quaternion.identity, Scale = 1f});
                ecb.AddComponent(entity, new UnitMoveOrder {Dest = roundPos});
            }
        }

        

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    const float STRUCTURE_CHECK_BEVEL = 0.3f;
    bool CheckValidStructurePlacement(PhysicsWorldSingleton world, float3 position, int3 size)
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
            Filter = STRUCTURE_MASK,
        };

        bool hasOverlap = world.OverlapAabb(box, ref hits);
        hits.Dispose();
        
        return !hasOverlap;
    }
    private const float GRID_SIZE = 3f;
    float3 SnapToGrid(PhysicsWorldSingleton world, float3 position)
    {
        float3 gridSnapped = math.round(position / GRID_SIZE) * GRID_SIZE;
        
        if (TryGetGroundPoint(world, gridSnapped, out float3 groundPos))
        {
            return groundPos;
        }
        
        return gridSnapped;
    }
    private const float DEPTH_TEST_HEIGHT = 10f;
    private bool TryGetGroundPoint(PhysicsWorldSingleton world, float3 pos, out float3 result)
    {
        float3 upOffset = new float3(0, DEPTH_TEST_HEIGHT, 0);
        RaycastInput ray = new RaycastInput
        {
            Start = pos + upOffset,
            End = pos - upOffset,
            Filter = TERRAIN_MASK,
        };

        if (world.CastRay(ray, out RaycastHit hit))
        {
            result = hit.Position;
            return true;
        }

        result = float3.zero;
        return false;
    }
}
public struct ConstructionRequest
{
    public float3 Dest;
    public int3 Size;
    //public float Spacing;
    public int PrimaryKey;
}
namespace ConstructionMan
{
    [InternalBufferCapacity(8)]
    public struct ConstructionDataManifest : IBufferElementData
    {
        //public ConstructionDataBaked Value;
        public ConstructionMode Mode;
        public float Spacing;
        public int3 Size;
        public int PrimaryKey;
        public int SecondaryKey;
    }
}

public struct ConstructionDataBaked
{
    public ConstructionMode Mode;
    public float Spacing;
    public float3 Size;
    public int PrimaryKey;
    public int SecondaryKey;
}