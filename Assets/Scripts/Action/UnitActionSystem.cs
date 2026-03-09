using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Construction;
using Unity.NetCode;
using RTS.InputLogging;

[InternalBufferCapacity(16)]
public struct ConstructRequest : IBufferElementData
{
    public float3 Position;
    public ConstructionDataBaked Data;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup)), BurstCompile, UpdateAfter(typeof(InputHandlerSystem))]
public partial struct UnitActionSystem : ISystem
{
    const float MAX_RAY_LENGTH = 300f;
    const float UNIT_RADIUS_MULTIPLIER = 0.9f;

    private CollisionFilter TERRAIN_FILTER;
    private CollisionFilter STRUCTURE_MASK;

    public void OnCreate(ref SystemState state)
    {
        TERRAIN_FILTER = new CollisionFilter
        {
            CollidesWith = 1 << 7,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
        STRUCTURE_MASK = new CollisionFilter
        {
            CollidesWith = 1 << 8,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
    }

    public void OnDestroy(ref SystemState state) { }

    #region Dispatch

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<CurrentTurnInput>(out var turnInput) || !turnInput.Ready)
            return;

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        ProcessInput(ref state, ref ecb, turnInput.Input0);
        ProcessInput(ref state, ref ecb, turnInput.Input1);

        var turnInputEntity = SystemAPI.GetSingletonEntity<CurrentTurnInput>();
        ecb.SetComponent(turnInputEntity, new CurrentTurnInput { Ready = false });

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void ProcessInput(ref SystemState state, ref EntityCommandBuffer ecb, BittableInput input)
    {
        if (input.Type != InputType.Action) return;
        OnAction(ref state, ref ecb, input.Action, input.TeamID);
    }

    [BurstCompile]
    private void OnAction(ref SystemState state, ref EntityCommandBuffer ecb, ActionUseData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<ActionInfoManifest>(out var manifest)) return;

        ActionInfo actionInfo = manifest.Blob.Value.UnitsActionInfo[action.SelectionKey][action.LocalActionIndex];

        switch (actionInfo.ActionType)
        {
            case ActionType.AddUnitToQueue:
                AddUnitToQueue(ref state, action, actionInfo, team);
                break;
            case ActionType.Move:
                Move(ref state, action, team);
                break;
            case ActionType.SetRallyPoint:
                SetRallyPoint(ref state, action, team);
                break;
            case ActionType.BuildStructure:
                BuildStructure(ref state, ref ecb, action, actionInfo, team);
                break;
        }
    }

    #endregion

    #region Move

    [BurstCompile]
    private void Move(ref SystemState state, ActionUseData action, int team)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin,
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,
            Filter = TERRAIN_FILTER
        };

        if (!physicsWorld.CastRay(raycastInput, out RaycastHit movCenter)) return;

        float3 calculatedCenter = float3.zero;
        int unitCount = 0;
        var unitPositions = new NativeList<float3>(64, Allocator.Temp);

        foreach (var (transform, selected) in SystemAPI.Query<LocalTransform, RefRO<Selected>>())
        {
            if (!selected.ValueRO.Value) continue;
            unitCount++;
            calculatedCenter += transform.Position;
            unitPositions.Add(transform.Position);
        }

        if (unitCount == 0)
        {
            unitPositions.Dispose();
            return;
        }

        calculatedCenter /= unitCount;

        float calculatedRadius = 0;
        foreach (float3 p in unitPositions)
            calculatedRadius += BMath.DistXZ(p, calculatedCenter);
        calculatedRadius = (calculatedRadius / unitCount) * UNIT_RADIUS_MULTIPLIER;

        bool compress = BMath.DistXZ(movCenter.Position, calculatedCenter) < calculatedRadius;
        float3 offset = new float3(0, 10, 0);

        foreach (var (transform, orders, selected, uTeam) in
            SystemAPI.Query<LocalTransform, RefRW<OrderList>, RefRO<Selected>, RefRO<Team>>())
        {
            if (uTeam.ValueRO.TeamID != team || !selected.ValueRO.Value) continue;

            float3 movPos = compress
                ? (transform.Position - calculatedCenter) / 2f + movCenter.Position
                : (transform.Position - calculatedCenter) + movCenter.Position;

            var ray = new RaycastInput
            {
                Start = movPos + offset,
                End = movPos - offset,
                Filter = TERRAIN_FILTER
            };

            if (!physicsWorld.CastRay(ray, out var hit)) continue;

            if (!action.Shifting) orders.ValueRW.Value.Clear();
            orders.ValueRW.Value.Add(new OrderElement
            {
                Type = OrderType.Move,
                Position = hit.Position,
                Data = -1,
            });
        }

        unitPositions.Dispose();
    }

    #endregion

    #region Rally Point

    [BurstCompile]
    private void SetRallyPoint(ref SystemState state, ActionUseData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;

        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin,
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,
            Filter = TERRAIN_FILTER
        };
        if (!world.CastRay(raycastInput, out var hit)) return;

        // Use SelectionKey from the packed input — authoritative across both clients
        foreach (var (selected, key, uTeam, prod) in SystemAPI.Query<
            RefRO<Selected>, RefRO<SelectionKey>, RefRO<Team>, RefRW<ProductionStructure>>())
        {
            if (!selected.ValueRO.Value || uTeam.ValueRO.TeamID != team || key.ValueRO.Value != action.SelectionKey) continue;
            prod.ValueRW.RallyPoint = hit.Position;
        }
    }

    #endregion

    #region Add Unit To Queue

    [BurstCompile]
    private void AddUnitToQueue(ref SystemState state, ActionUseData action, ActionInfo actionInfo, int team)
    {
        float time = (float)SystemAPI.Time.ElapsedTime;

        // Use SelectionKey from the packed input — authoritative across both clients
        foreach (var (selected, key, uTeam, prod) in SystemAPI.Query<
            RefRO<Selected>, RefRO<SelectionKey>, RefRO<Team>, RefRW<ProductionStructure>>())
        {
            if (!selected.ValueRO.Value || uTeam.ValueRO.TeamID != team || key.ValueRO.Value != action.SelectionKey) continue;
            if (prod.ValueRO.QueueCount >= prod.ValueRO.QueueSize) continue;
            if (prod.ValueRO.QueueCount >= prod.ValueRO.Queue.Capacity) continue;

            prod.ValueRW.QueueCount++;
            prod.ValueRW.Queue.Add(prod.ValueRO.Prefabs[actionInfo.PrefabIndex]);

            if (prod.ValueRO.QueueCount == 1) prod.ValueRW.StartTime = time;
        }
    }

    #endregion

    #region Build Structure

    [BurstCompile]
    private void BuildStructure(ref SystemState state, ref EntityCommandBuffer ecb,
        ActionUseData action, ActionInfo actionInfo, int team)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var constructionData = SystemAPI.GetSingletonBuffer<ConstructionDataManifest>();

        var raycastInput = new RaycastInput
        {
            Start = action.RayOrigin,
            End = action.RayOrigin + action.RayDirection * MAX_RAY_LENGTH,
            Filter = TERRAIN_FILTER
        };

        if (!physicsWorld.CastRay(raycastInput, out RaycastHit hit)) return;

        float3 roundPos = ConstructionUtil.SnapToGrid(physicsWorld, hit.Position, TERRAIN_FILTER);

        // Use SelectionKey from the packed input — authoritative across both clients
        foreach (var (selected, orders, key, uTeam, work) in SystemAPI.Query<
            RefRO<Selected>, RefRW<OrderList>, RefRO<SelectionKey>, RefRO<Team>, RefRW<Worker>>())
        {
            if (!selected.ValueRO.Value || uTeam.ValueRO.TeamID != team || key.ValueRO.Value != action.SelectionKey) continue;

            var cD = constructionData[work.ValueRO.ConstructKeys[actionInfo.PrefabIndex]];
            if (!ConstructionUtil.CheckValidStructurePlacement(physicsWorld, roundPos, cD.Size, STRUCTURE_MASK)) continue;

            if (!action.Shifting) orders.ValueRW.Value.Clear();
            orders.ValueRW.Value.Add(new OrderElement
            {
                Type = OrderType.BuildStructure,
                Position = roundPos,
                Data = actionInfo.PrefabIndex,
            });
            break;
        }
    }

    #endregion
}

namespace Construction
{
    [InternalBufferCapacity(8)]
    public struct ConstructionDataManifest : IBufferElementData
    {
        public ConstructionDataBaked Value;
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