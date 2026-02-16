using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct MoveUnitsData
{
    public bool Shifting;
    // We can keep the direction and the current ray's origin for the DOTS system 
    // to reuse in its raycast calculations.
    public float3 RayOrigin;
    public float3 RayDirection;
}


[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovementSystem)), BurstCompile]
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

    //SelectionBox _selectionBox = null;
    #region Receive rpcs
    protected override void OnUpdate()
    {
        /*        if (_selectionBox == null)
                {
                    _selectionBox = GameObject.FindFirstObjectByType<SelectionBox>();
                }*/
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (rpc, rpcSource, entity) in
                 SystemAPI.Query<RefRO<FixedSelectionRpc>, RefRO<ReceiveRpcCommandRequest>>()
                 .WithEntityAccess())
        {
            
            //_selectionBox.UpdatePerspectiveSelection(rpc.ValueRO.Select);
            //var e = _selectionBox.GetColliderEntity();
            HandleUnitSelect(ref ecb, rpc.ValueRO.Select, rpc.ValueRO.Team);
            ecb.DestroyEntity(entity);
        }
        foreach (var (rpc, rpcSource, entity) in
         SystemAPI.Query<RefRO<CodeSelectRpc>, RefRO<ReceiveRpcCommandRequest>>()
         .WithEntityAccess())
        {

            //_selectionBox.UpdatePerspectiveSelection(rpc.ValueRO.Select);
            //var e = _selectionBox.GetColliderEntity();
            OnCodeSelectUnits(ref ecb, rpc.ValueRO.CodeSelect, rpc.ValueRO.Team);
            ecb.DestroyEntity(entity);
        }
        foreach (var (rpc, rpcSource, entity) in
         SystemAPI.Query<RefRO<MoveUnitsRpc>, RefRO<ReceiveRpcCommandRequest>>()
         .WithEntityAccess())
        {

            //_selectionBox.UpdatePerspectiveSelection(rpc.ValueRO.Select);
            //var e = _selectionBox.GetColliderEntity();
            /*UnityEngine.Debug.
                Log($"received move units rpc {rpc.ValueRO.Move.RayOrigin}, {rpc.ValueRO.Move.RayDirection}");*/

            
            OnMoveUnits(ref ecb, rpc.ValueRO.Move, rpc.ValueRO.Team);
            ecb.DestroyEntity(entity);
        }
        foreach (var (rpc, rpcSource, entity) in
         SystemAPI.Query<RefRO<ClearUnitsRpc>, RefRO<ReceiveRpcCommandRequest>>()
         .WithEntityAccess())
        {

            //_selectionBox.UpdatePerspectiveSelection(rpc.ValueRO.Select);
            //var e = _selectionBox.GetColliderEntity();
            OnClearSelection(ref ecb, rpc.ValueRO.Team);
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    #endregion
    //private EntityQuery _assetQuery;

    //DEPRECATED
    /*private Terrain terrain;
    private TerrainData terrainData;
    private float3 terrainPos;
    private float3 terrainSize;*/
    protected override void OnCreate()
    {
/*        InputBridge.OnClearUnits += OnClearSelection;
        InputBridge.OnMoveUnits += OnMoveUnits;
        InputBridge.OnSelectUnits += HandleUnitSelect;
        InputBridge.OnCodeSelectUnits += OnCodeSelectUnits;*/
        
        
        //DEPRECATED
        /*        var t = GameObject.FindFirstObjectByType<Terrain>();
                if (t != null)
                {
                    terrain = t;
                    terrainData = t.terrainData;
                    terrainPos = t.transform.position;
                    terrainSize = terrainData.size;
                }*/
    }
    protected override void OnDestroy()
    {
        //_assetQuery.Dispose();
/*        InputBridge.OnClearUnits -= OnClearSelection;
        InputBridge.OnMoveUnits -= OnMoveUnits;
        InputBridge.OnSelectUnits -= HandleUnitSelect;
        InputBridge.OnCodeSelectUnits -= OnCodeSelectUnits;*/
    }
    #region CodeSelect
    private void OnCodeSelectUnits(ref EntityCommandBuffer ecb, byte code, int team)
    {

        if (!SystemAPI.TryGetSingleton<AssetSingleton>(out var assetSingleton)) { return; }

        //var ecb = new EntityCommandBuffer(Allocator.Temp);
        //0 is all others are command groups
        foreach (var (t, e) in 
            SystemAPI.Query<RefRO<Team>>().
            WithEntityAccess().WithAll<UnitTag>())
        {
            if (t.ValueRO.TeamID == team)
            {
                AddSelection(ref ecb, e, assetSingleton);
            }
        }
    }
    #endregion
    #region  SelectUnits
    private void HandleUnitSelect(ref EntityCommandBuffer ecb, FixedSelectionData selectionData, int teamID)
    {
        if (!SystemAPI.TryGetSingleton<AssetSingleton>(out var assetSingleton)) { return; }

        // Validate we have 8 vertices
        if (selectionData.Value.Length != 8)
        {
            Debug.LogError("Invalid selection data - expected 8 vertices");
            return;
        }

        //var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        var collisionWorld = physicsWorld.CollisionWorld;

        var tag = SystemAPI.GetComponentLookup<UnitTag>(true);
        var structureTag = SystemAPI.GetComponentLookup<StructureTag>(true);
        var team = SystemAPI.GetComponentLookup<Team>(true);

        float3 center = float3.zero;
        for (int i = 0; i < 8; i++)
            center += selectionData.Value[i];

        center /= 8f;

        // Convert to LOCAL space
        var localVerts = new NativeArray<float3>(8, Allocator.Temp);
        for (int i = 0; i < 8; i++)
            localVerts[i] = selectionData.Value[i] - center;

        // Create collider
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

        //collider.Value.CastCollider(input, out var s);
        //UnityEngine.Debug.Log($"S hits of {hits.Length}");

        bool onlyStructures = true;
        NativeList<Entity> hitStructures = new NativeList<Entity>(16, Allocator.Temp);

        //UnityEngine.Debug.Log($"Hit {hits.Length} entitys");
        foreach (var h in hits)
        {
            Entity hitEntity = h.Entity;

            if (tag.HasComponent(hitEntity) && team.GetRefRO(hitEntity).ValueRO.TeamID == teamID)
            {
                onlyStructures = false;
                AddSelection(ref ecb, hitEntity, assetSingleton);
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
                AddStructureSelection(ref ecb, h, assetSingleton);
            }
        }

        // Clean up
        collider.Dispose();
        hits.Dispose();
        hitStructures.Dispose();
        //ecb.Playback(EntityManager);
        //ecb.Dispose();
    }
    #region OnMoveUnits
    [BurstCompile]
    private void OnMoveUnits(ref EntityCommandBuffer ecb, MoveUnitsData m, int team)
    {
        
        //var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var raycastInput = new RaycastInput
        {
            Start = m.RayOrigin, // Ray origin
            End = m.RayOrigin + m.RayDirection * MAX_RAY_LENGTH,   // Ray end point
            Filter = TERRAIN_FILTER // Or a custom filter
        };

        UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, Color.red, 10f);

        float3 calculatedCenter = float3.zero;

        //assigned after the center has been calculated;
        int unitCount = 0;

        //given 64 to reduce memory churn
        var unitPositions = new NativeList<float3>(64, Allocator.Temp);
        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit movCenter))
        {
            UnityEngine.Debug.Log($"Sent move order to {movCenter.Position}");
            foreach (var transform in SystemAPI.Query<LocalTransform>().WithAll<UnitSelecetedTag>())
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
                                                                        //remove this later
            bool mode = BMath.DistXZ(movCenter.Position, calculatedCenter) < calculatedRadius;

            float3 offset = new float3(0,10,0);
            foreach (var (transform, orders, entity) in SystemAPI.Query<LocalTransform, RefRW<OrderList>>().WithAll<UnitSelecetedTag>().WithEntityAccess())
            {
                //if its outside then
                float3 movPos = (transform.Position - calculatedCenter) + movCenter.Position;
                //if its inside then
                if (mode)
                {
                    movPos = (transform.Position - calculatedCenter)/2f + movCenter.Position;
                }
                var ray = new RaycastInput
                {
                    Start = movPos + offset,
                    End = movPos - offset,
                    Filter = TERRAIN_FILTER 
                };
                if (physicsWorld.CastRay(ray, out var hit))
                {
                    //UnityEngine.Debug.Log($"Move units to {hit.Position}");
                    if (!m.Shifting) orders.ValueRW.Value.Clear();
                    orders.ValueRW.Value.Add(new OrderElement
                    {
                        Type = OrderType.Move,
                        Position = hit.Position,
                        //not needed for this guy
                        Data = -1,
                    });
                }
            }
        }

        unitPositions.Dispose();
       /* ecb.Playback(EntityManager);
        ecb.Dispose();*/
    }
    #endregion
    #region  ClearSelection
    [BurstCompile]
    private void OnClearSelection(ref EntityCommandBuffer ecb, int team)
    {

        //EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        // Query for all units that are currently selected (the Parents)
        var selectedUnitsQuery = SystemAPI.QueryBuilder().WithAll<UnitSelecetedTag>().Build();

        // Query for all selection visuals (the Children)
        var visualQuery = SystemAPI.QueryBuilder().WithAll<SelectedVisualTag, Parent>().Build();

        // This provides a list of all units that need to be unselected.
        var selectedParentEntities = selectedUnitsQuery.ToEntityArray(Allocator.TempJob);

        foreach (var e in selectedParentEntities)
        {
            ecb.RemoveComponent(e, typeof(UnitSelecetedTag));
        }

        var visualEntities = visualQuery.ToEntityArray(Allocator.TempJob);

        var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);


        var clearJob = new ClearSelectionJob
        {
            Ecb = ecb,
            SelectedParentEntities = selectedParentEntities,
            VisualEntities = visualEntities,
            ParentLookup = parentLookup,
        };

        var jobHandle = clearJob.Schedule();
        jobHandle.Complete();

        //ecb.Playback(EntityManager);
        //ecb.Dispose();
        visualEntities.Dispose();
        selectedParentEntities.Dispose();
    }
    #endregion
    #region SelectStructure
    [BurstCompile]
    private void AddStructureSelection(ref EntityCommandBuffer ecb, Entity unit, AssetSingleton assetSingleton)
    {
        ecb.AddComponent<UnitSelecetedTag>(unit);

        if (!EntityManager.HasBuffer<Child>(unit))
        {
            ecb.AddBuffer<Child>(unit);
        }

        var visual = ecb.Instantiate(assetSingleton.SelectedVisual);

        ecb.AddComponent(visual, new Parent { Value = unit });
        ecb.SetComponent(visual, new LocalTransform
        {
            Position = new float3(0, 0, 0),
            Rotation = quaternion.identity,
            Scale = 5f
        });
        ecb.AddComponent<SelectedVisualTag>(visual);
    }

    [BurstCompile]
    private void AddSelection(ref EntityCommandBuffer ecb, Entity unit, AssetSingleton assetSingleton)
    {
        ecb.AddComponent(unit, new UnitSelecetedTag {Value = 1 });

        if (!EntityManager.HasBuffer<Child>(unit))
        {
            ecb.AddBuffer<Child>(unit);
        }

        var visual = ecb.Instantiate(assetSingleton.SelectedVisual);

        ecb.AddComponent(visual, new Parent { Value = unit });
        ecb.SetComponent(visual, new LocalTransform
        {
            Position = new float3(0, 0, 0),
            Rotation = quaternion.identity,
            Scale = 1f
        });
        ecb.AddComponent<SelectedVisualTag>(visual);

        // 5. Append child reference to parent
        //ecb.AppendToBuffer(unit, new Child { Value = visual });
    }
    #endregion
}
#region ClearJob
[BurstCompile]
public partial struct ClearSelectionJob : IJob
{

    public EntityCommandBuffer Ecb;
    public NativeArray<Entity> VisualEntities;
    public NativeArray<Entity> SelectedParentEntities;
    [ReadOnly] public ComponentLookup<Parent> ParentLookup;
    public void Execute()
    {
        foreach (var visualEntity in VisualEntities)
        {
            // Must check HasComponent because the query only guarantees it had the component 
            // when the query was built, but the entity might have been destroyed elsewhere.
            if (ParentLookup.HasComponent(visualEntity))
            {
                Entity parentEntity = ParentLookup[visualEntity].Value;

                // Check if this visual's parent is in our list of entities we just unselected.
                // Note: NativeArray.Contains is an O(N) linear search, which is acceptable 
                // for small selection sizes (e.g., up to 100 units).
                foreach (var unselectedParent in SelectedParentEntities)
                {
                    if (unselectedParent.Equals(parentEntity))
                    {
                        Ecb.DestroyEntity(visualEntity);
                        // Once we find the parent, we can stop searching the parent list for this visual.
                        break;
                    }
                }
            }
        }
    }
}
#endregion

public struct SelectedVisualTag : IComponentData { }
#endregion