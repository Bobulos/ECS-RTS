using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
partial class UnitActionSystem : SystemBase
{
    const float MAX_RAY_LENGTH = 300f;
    const float UNIT_RADIUS_MULTIPLIER = 0.9f;
    protected override void OnCreate()
    {
        //_count = 100;
        UnitActionManager.OnAction += OnAction;
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
}
