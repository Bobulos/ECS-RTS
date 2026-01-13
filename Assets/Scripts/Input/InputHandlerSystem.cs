using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
public partial class InputHandlerSystem : SystemBase
{
    const float MAX_RAY_LENGTH = 300f;
    const float FORMATION_SPACING = 2f;


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
    }
    private void HandleUnitSelect(Entity selectionEntity, SelectionVertecies unused, uint t)
    {
        if (selectionEntity == Entity.Null) { return; }
        //UnityEngine.Debug.Log("Events working properly");
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        //var playerData = SystemAPI.GetSingleton<LocalPlayerData>();
        var collisionWorld = physicsWorld.CollisionWorld;

        var tag = SystemAPI.GetComponentLookup<UnitTag>(true);
        var team = SystemAPI.GetComponentLookup<UnitTeam>(true);

        // Get collider from your selection entity
        var collider = EntityManager.GetComponentData<PhysicsCollider>(selectionEntity);

        // --- Build an input for OverlapCollider ---
        float3 pos = EntityManager.GetComponentData<LocalTransform>(selectionEntity).Position;
        var input = new ColliderCastInput(collider.Value, pos, pos+new float3(0.1f,0,0), quaternion.identity);
        // Collect results
        var hits = new NativeList<ColliderCastHit>(Allocator.Temp);

        //UnityEngine.Debug.Log(hits.Length);
        collisionWorld.CastCollider(input, ref hits);
        // --- Process results ---
        foreach (var h in hits)
        {
            //UnityEngine.Debug.Log("There are elemts");
            Entity hitEntity = h.Entity;

            if (hitEntity == selectionEntity) continue; // skip self

            if (tag.HasComponent(hitEntity) && team.GetRefRO(hitEntity).ValueRO.TeamID == t)
            {
                AddSelection(ref ecb, hitEntity);
            }
        }

        //your use has exspired me
        ecb.DestroyEntity(selectionEntity);
        hits.Dispose();
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
        int unitCount = 0;
        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit movCenter))
        {
            foreach (var transform in SystemAPI.Query<LocalTransform>().WithAll<UnitSelecetedTag>().WithNone<UnitMoveOrder>())
            {
                unitCount++;
                calculatedCenter += transform.Position;
            }
            if (unitCount == 0)
            {
                ecb.Dispose();
                return;
            }
            calculatedCenter /= unitCount;

            foreach (var (transform, entity) in SystemAPI.Query<LocalTransform>().WithAll<UnitSelecetedTag>().WithNone<UnitMoveOrder>().WithEntityAccess())
            {
                float3 movPos = (transform.Position - calculatedCenter)+movCenter.Position;
                float3 vo = new float3(0, DEPTH_TEST, 0);
                var ray = new RaycastInput
                {
                    Start = movPos + vo,
                    End = movPos - vo,
                    Filter = TERRAIN_MASK,
                };
                if (physicsWorld.CastRay(ray, out var hit))
                {
                    ecb.AddComponent(entity, new UnitMoveOrder { Dest = hit.Position });
                }
            }
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }


    /*[BurstCompile]
    private void OnMoveUnits(MoveUnitsData m, uint team)
    {
        //EnsureTerrain();   

        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var orderLookup = SystemAPI.GetComponentLookup<UnitMoveOrder>(false);
        var raycastInput = new RaycastInput
        {
            Start = m.CurrentRayOrigin, // Ray origin
            End = m.CurrentRayOrigin + m.CurrentRayDirection * MAX_RAY_LENGTH,   // Ray end point
            Filter = CollisionFilter.Default // Or a custom filter
        };


        if (physicsWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
        {
            var selectedEntities = new NativeList<(Entity entity, float3 pos)>(16, Allocator.Temp);
            foreach (var (tag, transform, entity) in
                     SystemAPI.Query<RefRO<UnitSelecetedTag>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                selectedEntities.Add((entity, transform.ValueRO.Position));
            }

            int count = selectedEntities.Length;
            if (count == 0)
            {
                selectedEntities.Dispose();
                ecb.Dispose();
                return;
            }

            // Formation grid dimensions
            int gridSize = (int)math.ceil(math.sqrt(count));
            float3 basePos = hit.Position;

            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            for (int i = 0; i < count; i++)
            {
                var (entity, currentPos) = selectedEntities[i];
                int row = i / gridSize;
                int col = i % gridSize;

                // Calculate offset in a centered grid formation
                float offsetX = (col - (gridSize - 1) / 2f) * FORMATION_SPACING;
                float offsetZ = (row - (gridSize - 1) / 2f) * FORMATION_SPACING;

                //float3 targetPos = basePos + new float3(offsetX, 0, offsetZ);
                float3 targetXZ = basePos + new float3(offsetX, 0, offsetZ);

                if (SampleTerrainHeight(world, targetXZ, out float3 targetPos))
                {
                    // Update or add the UnitMoveOrder component
                    if (orderLookup.HasComponent(entity))
                    {
                        ecb.SetComponent(entity, new UnitMoveOrder { Dest = targetPos });
                    }
                    else
                    {
                        ecb.AddComponent(entity, new UnitMoveOrder
                        {
                            Dest = targetPos
                        });
                    }
                }
            }

            selectedEntities.Dispose();
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
        //physicsWorld.Dispose();
    }*/
    private CollisionFilter TERRAIN_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    const float DEPTH_TEST = 10;

    private bool SampleTerrainHeight(PhysicsWorldSingleton world, float3 worldPos, out float3 terrainPos)
    {
        float3 offset = new float3(0, DEPTH_TEST, 0);

        var ray = new RaycastInput
        {
            Start = worldPos + DEPTH_TEST,
            End = worldPos - DEPTH_TEST,
            Filter = TERRAIN_MASK
        };
        if (world.CastRay(ray, out var hit))
        {
            terrainPos = hit.Position;
            return true;
        }
        terrainPos = worldPos;
        return false;
        
    }
/*    private float3 SampleTerrainHeight(float3 worldPos)
    {
        float3 localPos = worldPos - terrainPos;
        float u = Mathf.Clamp01(localPos.x / terrainSize.x);
        float v = Mathf.Clamp01(localPos.z / terrainSize.z);
        float height = terrainData.GetInterpolatedHeight(u, v) + terrainPos.y;
        return new float3(worldPos.x, height, worldPos.z);
    }*/

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
    private void AddSelection(ref EntityCommandBuffer ecb, Entity unit)
    {
        var entityManager = EntityManager;

        // 1. Skip already selected units
        if (entityManager.HasComponent<UnitSelecetedTag>(unit))
            return;

        var assetSingleton = SystemAPI.GetSingleton<AssetSingleton>();
        ecb.AddComponent<UnitSelecetedTag>(unit);

        // 2. Ensure the Child buffer exists *using ECB* (not EntityManager)
        // If you use EntityManager.AddBuffer here, it makes a live structural change,
        // which can cause crashes if called mid-frame. Use ECB safely instead.
        if (!entityManager.HasBuffer<Child>(unit))
        {
            ecb.AddBuffer<Child>(unit);
        }

        // 3. Instantiate visual and attach it properly
        var visual = ecb.Instantiate(assetSingleton.SelectedVisual);

        // 4. Link the visual as a child of the unit
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
