using System.Collections.Generic;
using System.Security.Principal;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
public partial class InputHandlerSystem : SystemBase
{
    const float MAX_RAY_LENGTH = 300f;
    const float FORMATION_SPACING = 2f;
    const float UNIT_RADIUS_MULTIPLIER = 0.9f;

    //DEPRECATED
    /*private Terrain terrain;
    private TerrainData terrainData;
    private float3 terrainPos;
    private float3 terrainSize;*/
    protected override void OnCreate()
    {
        InputBridge.OnClearUnits += OnClearSelection;
        InputBridge.OnMoveUnits += OnMoveUnits;
        InputBridge.OnSelectUnits += HandleUnitSelect;
        InputBridge.OnCodeSelectUnits += OnCodeSelectUnits;

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
        InputBridge.OnClearUnits -= OnClearSelection;
        InputBridge.OnMoveUnits -= OnMoveUnits;
        InputBridge.OnSelectUnits -= HandleUnitSelect;
        InputBridge.OnCodeSelectUnits -= OnCodeSelectUnits;
    }
    private void OnCodeSelectUnits(byte code, uint team)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var assetSingleton = SystemAPI.GetSingleton<AssetSingleton>();
        //0 is all others are command groups
        foreach (var (t, e) in SystemAPI.Query<RefRO<UnitTeam>>().WithEntityAccess())
        {
            if (t.ValueRO.TeamID == team)
            {
                AddSelection(ref ecb, e, assetSingleton);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    private void HandleUnitSelect(Entity selectionEntity, SelectionData unused, uint t)
    {
        if (selectionEntity == Entity.Null) { return; }
        //UnityEngine.Debug.Log("Events working properly");
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var collisionWorld = physicsWorld.CollisionWorld;

        var tag = SystemAPI.GetComponentLookup<UnitTag>(true);
        var structureTag = SystemAPI.GetComponentLookup<StructureTag>(true);
        var team = SystemAPI.GetComponentLookup<UnitTeam>(true);

        // Get collider from your selection entity
        var collider = EntityManager.GetComponentData<PhysicsCollider>(selectionEntity);

        // Build an input for OverlapCollider
        float3 pos = EntityManager.GetComponentData<LocalTransform>(selectionEntity).Position;
        var input = new ColliderCastInput(collider.Value, pos, pos + new float3(0.1f, 0, 0), quaternion.identity);
        // Collect results
        var hits = new NativeList<ColliderCastHit>(Allocator.Temp);

        //UnityEngine.Debug.Log(hits.Length);
        collisionWorld.CastCollider(input, ref hits);

        bool onlyStructures = true;
        NativeList<Entity> hitStructures = new NativeList<Entity>(16, Allocator.Temp);

        var assetSingleton = SystemAPI.GetSingleton<AssetSingleton>();

        // --- Process results ---
        foreach (var h in hits)
        {
            //UnityEngine.Debug.Log("There are elemts");
            Entity hitEntity = h.Entity;

            if (hitEntity == selectionEntity) continue; // skip self

            if (tag.HasComponent(hitEntity) && team.GetRefRO(hitEntity).ValueRO.TeamID == t)
            {
                onlyStructures = false;
                AddSelection(ref ecb, hitEntity, assetSingleton);
            } else if (onlyStructures && structureTag.HasComponent(hitEntity))
            {
                hitStructures.Add(hitEntity);
            }
        }
        //handle structure selection
        if (onlyStructures && hitStructures.Length > 0)
        {
            foreach (var h in hitStructures)
            {
                AddStructureSelection(ref ecb, h, assetSingleton);
            }
        }

        //your use has exspired me
        ecb.DestroyEntity(selectionEntity);
        hits.Dispose();
        hitStructures.Dispose();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    //private bool terrainInitialized => terrain != null;
    protected override void OnUpdate()
    {
    }
    /*private void EnsureTerrain()
    {
        if (terrainInitialized) return;

        var t = GameObject.FindFirstObjectByType<Terrain>();
        if (t == null) return; // still nothing in scene

        terrain = t;
        terrainData = t.terrainData;
        terrainPos = t.transform.position;
        terrainSize = terrainData.size;
    }*/
    [BurstCompile]
    private void OnMoveUnits(MoveUnitsData m, uint team)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var raycastInput = new RaycastInput
        {
            Start = m.CurrentRayOrigin, // Ray origin
            End = m.CurrentRayOrigin + m.CurrentRayDirection * MAX_RAY_LENGTH,   // Ray end point
            Filter = CollisionFilter.Default // Or a custom filter
        };

        float3 calculatedCenter = float3.zero;

        //assigned after the center has been calculated;
        

        int unitCount = 0;

        //given 64 to reduce memory churn
        var unitPositions = new NativeList<float3>(64, Allocator.Temp);
        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit movCenter))
        {

            foreach (var transform in SystemAPI.Query<LocalTransform>().WithAll<UnitSelecetedTag>().WithNone<UnitMoveOrder>())
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

            

            /*UnityEngine.Debug.DrawLine(calculatedCenter, calculatedCenter +
                new float3(calculatedRadius, 0, 0),
                UnityEngine.Color.red, 5f);*/

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
    private void OnClearSelection(uint team)
    {

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
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

        ecb.Playback(EntityManager);
        ecb.Dispose();
        visualEntities.Dispose();
        selectedParentEntities.Dispose();
    }
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
            Scale = 1f
        });
        ecb.AddComponent<SelectedVisualTag>(visual);

        // 5. Append child reference to parent
        //ecb.AppendToBuffer(unit, new Child { Value = visual });
    }
}
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

public struct SelectedVisualTag : IComponentData { }
