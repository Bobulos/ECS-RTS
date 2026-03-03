using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RTS.InputLogging;
public struct MoveUnitsData
{
    public bool Shifting;
    public float3 RayOrigin;
    public float3 RayDirection;
}

public struct SelectedVisualTag : IComponentData { }


//[UpdateAfter(typeof(UnitActionSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup)), UpdateAfter(typeof(LockstepSimulationGate)), BurstCompile]
public partial class InputHandlerSystem : SystemBase
{
    const float MAX_RAY_LENGTH = 300f;
    const float FORMATION_SPACING = 2f;
    const float UNIT_RADIUS_MULTIPLIER = 0.9f;

    private CollisionFilter TERRAIN_FILTER = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };

    #region Read input commands
    protected override void OnUpdate()
    {
        // if (!SystemAPI.TryGetSingleton<LockstepReady>(out var ready) || !ready.Value)
        //     return;
        if (!SystemAPI.TryGetSingleton<CurrentTurnInput>(out var turnInput) || !turnInput.Ready)
            return;
        //UnityEngine.Debug.Log($"Processing handler received");
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        // Process both players' inputs for this turn
        ProcessInput(ref ecb, turnInput.Input0);
        ProcessInput(ref ecb, turnInput.Input1);

        



        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void ProcessInput(ref EntityCommandBuffer ecb, BittableInput unpacked)
    {   
        //UnityEngine.Debug.Log($"Processing input of type {unpacked.Type} for team {unpacked.Team}");
        
        if (unpacked.Type == InputType.None) return;
        //UnityEngine.Debug.Log($"Processing input of type {unpacked.Type} for team {unpacked.Team}");
        switch (unpacked.Type)
        {
            case InputType.MoveUnits:
                //UnityEngine.Debug.Log($"Move units for team {unpacked.Team}");
                OnMoveUnits(unpacked.Move, unpacked.TeamID);
                break;
            case InputType.ClearUnits:
                //UnityEngine.Debug.Log($"Clearing selection for team {unpacked.Team}");
                OnClearSelection(ref ecb, unpacked.TeamID);
                break;
            case InputType.Action:
                // handled by action system
                break;
            case InputType.CodeSelectUnits:
                //UnityEngine.Debug.Log($"Code selection for team {unpacked.Team}");
                OnCodeSelectUnits(ref ecb, unpacked.CodeSelect, unpacked.TeamID);
                break;
            case InputType.SelectUnits:
                //UnityEngine.Debug.Log($"Handling select units input for team {unpacked.Team}");
                HandleUnitSelect(ref ecb, unpacked.Select, unpacked.TeamID);
                break;
        }
    }
    #endregion

    protected override void OnCreate()
    {
    }
    
    protected override void OnDestroy()
    {
    }
    
    #region CodeSelect
    private void OnCodeSelectUnits(ref EntityCommandBuffer ecb, byte code, int team)
    {
        UnityEngine.Debug.Log($"Code select {code} for team {team}");
        foreach (var (t, selected, e) in 
            SystemAPI.Query<RefRO<Team>, RefRW<Selected>>().
            WithEntityAccess().WithAll<UnitTag>())
        {
            if (t.ValueRO.TeamID == team)
            {
                ecb.SetComponent(e, new Selected { Value = true });
            }
        }
    }
    #endregion
    
    #region SelectUnits
    private void HandleUnitSelect(ref EntityCommandBuffer ecb, FixedSelectionData selectionData, int teamID)
    {
        UnityEngine.Debug.Log($"Handling unit select for team {teamID}");
        if (selectionData.Value.Length != 8)
        {
            Debug.LogError("Invalid selection data - expected 8 vertices");
            return;
        }

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorld.CollisionWorld;

        var tag = SystemAPI.GetComponentLookup<UnitTag>(true);
        var structureTag = SystemAPI.GetComponentLookup<StructureTag>(true);
        var team = SystemAPI.GetComponentLookup<Team>(true);

        float3 center = float3.zero;
        for (int i = 0; i < 8; i++)
            center += selectionData.Value[i];

        center /= 8f;

        var localVerts = new NativeArray<float3>(8, Allocator.Temp);
        for (int i = 0; i < 8; i++)
            localVerts[i] = selectionData.Value[i] - center;

        BlobAssetReference<Unity.Physics.Collider> collider =
            ConvexCollider.Create(localVerts, ConvexHullGenerationParameters.Default, CollisionFilter.Default);

        ColliderDebugUtil.DrawSelectionPrism(localVerts, center);

        localVerts.Dispose();

        if (!collider.IsCreated)
        {
            Debug.LogError("Failed to create selection collider");
            return;
        }
        
        var input = new ColliderCastInput(collider, center, center + new float3(0.01f, 0, 0), quaternion.identity, 1f);
        var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
        collisionWorld.CastCollider(input, ref hits);

        bool onlyStructures = true;
        NativeList<Entity> hitStructures = new NativeList<Entity>(16, Allocator.Temp);

        foreach (var h in hits)
        {
            Entity hitEntity = h.Entity;

            if (tag.HasComponent(hitEntity) && team.GetRefRO(hitEntity).ValueRO.TeamID == teamID)
            {
                onlyStructures = false;
                ecb.SetComponent(hitEntity, new Selected { Value = true });
            }
            else if (onlyStructures && structureTag.HasComponent(hitEntity))
            {
                hitStructures.Add(hitEntity);
            }
        }

        if (onlyStructures && hitStructures.Length > 0)
        {
            foreach (var h in hitStructures)
            {
                ecb.SetComponent(h, new Selected { Value = true });
            }
        }

        collider.Dispose();
        hits.Dispose();
        hitStructures.Dispose();
    }
    #endregion
    
    #region OnMoveUnits
    [BurstCompile]
    private void OnMoveUnits(MoveUnitsData m, int team)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var raycastInput = new RaycastInput
        {
            Start = m.RayOrigin,
            End = m.RayOrigin + m.RayDirection * MAX_RAY_LENGTH,
            Filter = TERRAIN_FILTER
        };

        UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, Color.red, 10f);

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
            
            foreach (var (transform, orders, selected, entity) in 
                     SystemAPI.Query<LocalTransform, RefRW<OrderList>, RefRO<Selected>>()
                     .WithEntityAccess())
            {
                if (!selected.ValueRO.Value) continue;
                
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
                    if (!m.Shifting) orders.ValueRW.Value.Clear();
                    
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
    
    #region ClearSelection
    [BurstCompile]
    private void OnClearSelection(ref EntityCommandBuffer ecb, int team)
    {
        foreach (var (selected, entity) in 
                 SystemAPI.Query<RefRW<Selected>>()
                 .WithEntityAccess())
        {
            if (selected.ValueRO.Value)
            {
                ecb.SetComponent(entity, new Selected { Value = false });
            }
        }
    }
    #endregion
}