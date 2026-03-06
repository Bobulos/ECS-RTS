using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Construction;
using Unity.NetCode;
using UnityEngine;
using RTS.InputLogging;
[InternalBufferCapacity(16)]
public struct ConstructRequest : IBufferElementData
{   
    public float3 Position;
    //add end pos later
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
        //_count = 100;
        //UnitActionManager.OnAction += OnAction;
        // EntityManager.CreateSingletonBuffer<ConstructRequest>();
        // var entity = EntityManager.CreateEntity(typeof(ConstructRequests));
        // EntityManager.AddBuffer<ConstructRequests>(entity);

    }
    public void OnDestroy(ref SystemState state)
    {
        //UnitActionManager.OnAction -= OnAction;
    }
    #region  Read input commands
    [BurstCompile]
    private void OnAction(ref SystemState state, ref EntityCommandBuffer ecb, ActionUseData action, int team)
    {
        if (!SystemAPI.TryGetSingleton<ActionInfoManifest>(out var manifest))
        {
            return;
        }
        ActionInfo actionInfo = 
        manifest.Blob.Value.UnitsActionInfo[action.SelectionKey][action.LocalActionIndex];
        switch (actionInfo.ActionType)
        {
            case  ActionType.AddUnitToQueue:
                AddUnitToQueue(ref state, actionInfo, team);
                //UnityEngine.Debug.Log("Added unit to queue");
                break;
            case ActionType.Move:
                //UnityEngine.
                //Debug.Log("Move order");
                Move(ref state, action, team);
                break;
            case ActionType.SetRallyPoint:
                //UnityEngine.Debug.Log("Set rally point");
                SetRallyPoint(ref state, action, team);
                break;
            case ActionType.BuildStructure:
                BuildStructure(ref state, ref ecb, action, actionInfo, team);
                break;
        }
    }
    [BurstCompile]

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<CurrentTurnInput>(out var turnInput) || !turnInput.Ready)
            return;

        //UnityEngine.Debug.Log("Action system running");

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        ProcessInput(ref state, ref ecb, turnInput.Input0);
        ProcessInput(ref state, ref ecb, turnInput.Input1);

        // Clear ready flag so we don't process the same turn twice
        var turnInputEntity = SystemAPI.GetSingletonEntity<CurrentTurnInput>();
        ecb.SetComponent(turnInputEntity, new CurrentTurnInput { Ready = false });

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        
    }
    [BurstCompile]
    private void ProcessInput(ref SystemState state, ref EntityCommandBuffer ecb, BittableInput unpacked)
    {
        
        if (unpacked.Type == InputType.None) return;

        switch (unpacked.Type)
        {
            case InputType.Action:
                OnAction(ref state, ref ecb, unpacked.Action, unpacked.TeamID);
                break;
        }
    }
    #endregion
    #region  Move
    //shared no check for key
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

        UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, Color.aquamarine, 100f);
        float3 calculatedCenter = float3.zero;
        int unitCount = 0;
        var unitPositions = new NativeList<float3>(64, Allocator.Temp);
        
        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit movCenter))
        {
            foreach (var (transform, selected) in SystemAPI.Query<LocalTransform, RefRO<Selected>>())
            {
                if (selected.ValueRO.Value)
                {
                    unitCount++;
                    calculatedCenter += transform.Position;
                    unitPositions.Add(transform.Position);
                }
            }
            
            if (unitCount == 0)
            {
                unitPositions.Dispose();
                return;
            }

            float calculatedRadius = 0;
            calculatedCenter /= unitCount;
            
            foreach (float3 p in unitPositions)
            {
                calculatedRadius += BMath.DistXZ(p, calculatedCenter);
            }

            calculatedRadius /= unitCount;
            calculatedRadius *= UNIT_RADIUS_MULTIPLIER;
            
            bool mode = BMath.DistXZ(movCenter.Position, calculatedCenter) < calculatedRadius;

            float3 offset = new float3(0, 10, 0);
            
            foreach (var (transform, orders, selected, uTeam, entity) in 
                     SystemAPI.Query<LocalTransform, RefRW<OrderList>, RefRO<Selected>, RefRO<Team>>()
                     .WithEntityAccess())
            {
                if (uTeam.ValueRO.TeamID != team || !selected.ValueRO.Value) continue;
                
                float3 movPos = (transform.Position - calculatedCenter) + movCenter.Position;
                
                if (mode)
                {
                    movPos = (transform.Position - calculatedCenter) / 2f + movCenter.Position;
                }
                
                var ray = new RaycastInput
                {
                    Start = movPos + offset,
                    End = movPos - offset,
                    Filter = TERRAIN_FILTER
                };
                
                if (physicsWorld.CastRay(ray, out var hit))
                {
                    if (!action.Shifting) orders.ValueRW.Value.Clear();
                    
                    orders.ValueRW.Value.Add(new OrderElement
                    {
                        Type = OrderType.Move,
                        Position = hit.Position,
                        Data = -1,
                    });
                }
            }
        }

        unitPositions.Dispose();
    }
    #endregion
    #region Rallypoint
    [BurstCompile]
    private void SetRallyPoint(ref SystemState state, ActionUseData action, int team)
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

        foreach (var (key, selected, uTeam, prod) in SystemAPI.Query<
            RefRO<SelectionKey>, RefRO<Selected>, RefRO<Team>, RefRW<ProductionStructure>>())
        {
            //check that it is the type that needs to be modified
            if (uTeam.ValueRO.TeamID != team || !selected.ValueRO.Value || key.ValueRO.Value != targetKey) continue;
            
            prod.ValueRW.RallyPoint = hit.Position;
            //UnityEngine.Debug.Log($"Set rally point to{hit.Position}");
            // Set the structures rally point
        }
    }
    #endregion
    #region  Add Unit to Queue
    [BurstCompile]
    private void AddUnitToQueue(ref SystemState state, ActionInfo action, int team)
    {
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;

        // This is the first index of the selected units
        int targetKey = selectedUnits.Buckets[0].Key;

        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (key, selected, uTeam, prod) in SystemAPI.Query<
            RefRO<SelectionKey>,
            RefRO<Selected>,
            RefRO<Team>,
            RefRW<ProductionStructure>>())
        {
            //check that it is the type that needs to be modified
            if (uTeam.ValueRO.TeamID != team || !selected.ValueRO.Value || key.ValueRO.Value != targetKey) continue;

            if (prod.ValueRO.QueueCount < prod.ValueRO.QueueSize && prod.ValueRO.QueueCount < prod.ValueRO.Queue.Capacity)
            {
                //UnityEngine.Debug.Log(action.PrefabIndex);
                prod.ValueRW.QueueCount++;
                prod.ValueRW.Queue.Add(prod.ValueRO.Prefabs[action.PrefabIndex]);

                //if it is the first in list need to start cycle
                if (prod.ValueRO.QueueCount == 1) prod.ValueRW.StartTime = time;
            }
            
        }
    }
    #endregion
    //public static Action s;
    //[BurstCompile]
    #region  Build Structure
    [BurstCompile]
    private void BuildStructure(ref SystemState state, ref EntityCommandBuffer ecb, 
    ActionUseData ActionUseData, ActionInfo actionInfo, int team)
    {
        //UnityEngine.Debug.Log("Build structure");
        if (!SystemAPI.TryGetSingleton<LocalSelectedUnits>(out var selectedUnits)) return;
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        //var structures = SystemAPI.GetSingletonBuffer<StructureManifest>();

        //var constructBufferEntity = SystemAPI.GetSingletonEntity<ConstructRequest>();
        var constructionData = SystemAPI.GetSingletonBuffer<ConstructionDataManifest>();

        var raycastInput = new RaycastInput
        {
            Start = ActionUseData.RayOrigin, // Ray origin
            End = ActionUseData.RayOrigin + ActionUseData.RayDirection * MAX_RAY_LENGTH,   // Ray end point
            Filter = CollisionFilter.Default // Or a custom filter
        };

        // This is the first index of the selected units
        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
        {
            //round the raycast pos
            float3 roundPos = ConstructionUtil.SnapToGrid(physicsWorld, hit.Position, TERRAIN_FILTER);

            int targetKey = selectedUnits.Buckets[0].Key;
            foreach (var (orders, key, uTeam, work, selected, entity) in SystemAPI.Query<
                RefRW<OrderList>,
                RefRO<SelectionKey>,
                RefRO<Team>,
                RefRW<Worker>,
                RefRO<Selected>>().WithEntityAccess())
            {
            //need to add position rounding
            
                //UnityEngine.Debug.Log("GOGOGOGOOG");
                //check that it is the type that needs to be modified
                if (uTeam.ValueRO.TeamID != team || !selected.ValueRO.Value || key.ValueRO.Value != targetKey) continue;
                // Get the construction data
                var cD = constructionData[work.ValueRO.ConstructKeys[actionInfo.PrefabIndex]];
                //UnityEngine.Debug.Log($"Built as index {work.ValueRO.ConstructKeys[action.Info.PrefabIndex]}");
                //UnityEngine.Debug.Log($"Built as size of {cD.Size}");
                if (!ConstructionUtil.CheckValidStructurePlacement(physicsWorld, roundPos, cD.Size, STRUCTURE_MASK)) continue;
                
                if (!ActionUseData.Shifting) {orders.ValueRW.Value.Clear();}
                orders.ValueRW.Value.Add(new OrderElement
                {
                   Type = OrderType.BuildStructure,
                   Position = roundPos,
                   //construction index of the structure
                   Data = actionInfo.PrefabIndex,
                });
                break;
                //ecb.AddComponent(entity, new UnitMoveOrder {Dest = roundPos});
            }
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