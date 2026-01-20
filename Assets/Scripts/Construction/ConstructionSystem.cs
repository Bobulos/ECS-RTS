using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
//using UnityEngine;

public partial class ConstructionSystem : SystemBase
{
    const float SEGEMENT_SIZE_OFFSET = 1f;
    private CollisionFilter TERRAIN_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };

    /*    int VALID_MAT_ID;
        int INVALID_MAT_ID;*/
    protected override void OnCreate()
    {
        ConstructionBridge.VisualizeWalls += VisualizeWalls;
        ConstructionBridge.ConstructWalls += ConstructWalls;
        ConstructionBridge.CancelContrstruction += CancelConstruction;
        ConstructionBridge.VisualizeStructure += VisualizeStructure;
        ConstructionBridge.ConstructStructure += ConstructStructure;
    }
    protected override void OnDestroy()
    {
        ConstructionBridge.VisualizeWalls -= VisualizeWalls;
        ConstructionBridge.VisualizeStructure -= VisualizeStructure;
        ConstructionBridge.ConstructWalls -= ConstructWalls;
        ConstructionBridge.CancelContrstruction -= CancelConstruction;
        ConstructionBridge.ConstructStructure -= ConstructStructure;
    }
    void CancelConstruction()
    {
        //UnityEngine.Debug.Log("Cancel Construction");
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Query for ALL entities with the WallVisualTag
        foreach (var (t, e) in SystemAPI.Query<StructureVisualTag>().WithEntityAccess())
        {
            ecb.DestroyEntity(e);
        }



        // Apply structural changes immediately
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void ConstructWalls(ConstructWallData d, uint team)
    {
        ApplyWallSnap(ref d);
        if (!CheckValidWallPlacement(d)) return;

        float3 dir = math.normalize(d.end - d.start);
        float dist = math.distance(d.start, d.end);
        if (dist < 0.01f) return;

        int segmentCount = math.max(1, (int)math.ceil(dist / d.constructData.spacing));
        float actualSpacing = dist / segmentCount;

        float3 prevNode = float3.zero;
        bool hasPrev = false;

        for (int i = 0; i <= segmentCount; i++)
        {
            float3 pos = d.start + dir * (i * actualSpacing);

            if (!TryGetStructureFromDB(d.constructData.key, out Entity nodePrefab))
                continue;

            var node = EntityManager.Instantiate(nodePrefab);

            EntityManager.SetComponentData(node, new LocalTransform
            {
                Position = pos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            if (hasPrev &&
                TryGetStructureFromDB(d.constructData.secondaryKey, out Entity segmentPrefab))
            {
                float3 midpoint = (prevNode + pos) * 0.5f;
                float3 forward = math.normalize(pos - prevNode);
                float segLength = math.distance(prevNode, pos);

                var segment = EntityManager.Instantiate(segmentPrefab);

                EntityManager.SetComponentData(segment, new LocalTransform
                {
                    Position = midpoint,
                    Rotation = quaternion.LookRotationSafe(forward, math.up()),
                    Scale = 1f
                });

                if (EntityManager.HasComponent<PhysicsCollider>(segment))
                {
                    var col = BoxCollider.Create(new BoxGeometry
                    {
                        Center = float3.zero,
                        Orientation = quaternion.identity,
                        Size = new float3(1f, 10f, segLength),
                        BevelRadius = 0.05f
                    });

                    EntityManager.AddComponentData(segment, new ColliderCleanup { ColliderRef = col });
                    EntityManager.SetComponentData(segment, new PhysicsCollider { Value = col });
                }

                EntityManager.AddComponent<PostTransformMatrix>(segment);
                EntityManager.SetComponentData(segment, new PostTransformMatrix
                {
                    Value = float4x4.Scale(new float3(1f, 1f, segLength - SEGEMENT_SIZE_OFFSET))
                });
            }

            prevNode = pos;
            hasPrev = true;
        }
    }
    void ConstructStructure(ConstructData d, uint team)
    {
        ApplySnap(ref d);
        if (TryGetStructureFromDB(d.constructData.key, out Entity prefab))
        {
            var e = EntityManager.Instantiate(prefab);
            EntityManager.SetComponentData(e, new LocalTransform
            {
                Position = d.pos,
                Rotation = quaternion.identity,
                Scale = 1f
            });
        }
    }
    void ApplyWallSnap(ref ConstructWallData d)
    {
        var startSnap = SnapWallPoint(d.start);
        var endSnap = SnapWallPoint(d.end);

        d.start = startSnap.position;
        d.end = endSnap.position;
    }
    SnapResult SnapWallPoint(float3 input)
    {
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var wallNodeLookup = SystemAPI.GetComponentLookup<WallNode>();
        var posLookup = SystemAPI.GetComponentLookup<LocalTransform>();

        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

        SnapResult result = new SnapResult
        {
            position = input,
            snappedToNode = false
        };

        if (world.OverlapSphere(input, WALL_CHECK_RADIUS, ref hits, STRUCTURE_MASK))
        {
            float min = math.INFINITY;

            foreach (var hit in hits)
            {
                if (!wallNodeLookup.HasComponent(hit.Entity)) continue;

                if (hit.Distance < min)
                {
                    min = hit.Distance;
                    result.position = posLookup.GetRefRO(hit.Entity).ValueRO.Position;
                    result.snappedToNode = true;
                }
            }
        }

        hits.Dispose();

        // Only ground snap if NOT snapped to a node
        if (!result.snappedToNode &&
            TryGetGroundPoint(ref world, result.position, out float3 ground))
        {
            result.position = ground;
        }

        return result;
    }

    void VisualizeStructure(ConstructData d)
    {
        ApplySnap(ref d);
        //already a visual
        if (SystemAPI.TryGetSingletonEntity<StructureVisualTag>(out Entity vis))
        {
            EntityManager.SetComponentData(vis, new LocalTransform
            {
                Position = d.pos,
                Scale = 1,
                Rotation = quaternion.identity,
            });
            SetValidMat(vis, CheckValidStructurePlacement(ref d));
        }
        else
        {
            if (TryGetStructureFromDB(d.constructData.key, out var prefab))
            {
                var e = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(e, new LocalTransform
                {
                    Position = d.pos,
                    Scale = 1,
                    Rotation = quaternion.identity,
                });
                EntityManager.AddComponent<StructureVisualTag>(e);
                EntityManager.RemoveComponent<PhysicsCollider>(e);
                SetValidMat(e, true);
            }
        }
    }
    void VisualizeWalls(ConstructWallData d)
    {
        ApplyWallSnap(ref d);
        if (!TryGetStructureFromDB(d.constructData.key, out Entity prefab))
            return;

        float dist = math.distance(d.start, d.end);
        float3 dir = float3.zero;

        // 1. Handle Direction and Distance
        // Only calculate dir if we actually have distance to avoid NaN errors
        if (dist > 0.001f)
        {
            dir = math.normalize(d.end - d.start);
        }

        // 2. Determine Segment Count
        // If dist is 0, segmentCount becomes 0, which correctly spawns 1 node (segmentCount + 1)
        int segmentCount = (int)math.ceil(dist / d.constructData.spacing);
        float actualSpacing = dist > 0 ? dist / segmentCount : 0;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var visualQuery = SystemAPI.QueryBuilder().WithAll<StructureVisualTag>().Build();
        var existingVisuals = visualQuery.ToEntityArray(Allocator.TempJob);

        // --- SHED ---
        for (int i = segmentCount + 1; i < existingVisuals.Length; i++)
        {
            ecb.DestroyEntity(existingVisuals[i]);
        }

        // --- GROW ---
        for (int i = existingVisuals.Length; i < segmentCount + 1; i++)
        {
            var newVisual = ecb.Instantiate(prefab);
            ecb.RemoveComponent<PhysicsCollider>(newVisual);
            ecb.AddComponent<StructureVisualTag>(newVisual);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();

        existingVisuals.Dispose();
        existingVisuals = visualQuery.ToEntityArray(Allocator.TempJob);

        bool valid = CheckValidWallPlacement(d);

        // --- PLACE ---
        for (int i = 0; i <= segmentCount; i++)
        {
            if (i >= existingVisuals.Length) break;

            // If dist is 0, this results in just d.start for the first and only iteration
            float3 pos = d.start + dir * (i * actualSpacing);

            Entity e = existingVisuals[i];

            EntityManager.SetComponentData(e, new LocalTransform
            {
                Position = pos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            SetValidMat(e, valid);
        }

        existingVisuals.Dispose();
    }



    public CollisionFilter STRUCTURE_MASK = new CollisionFilter
    {
        CollidesWith = 1 << 8,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    const float STRUCTURE_CHECK_BEVEL = 0.3f;
    const float GRID_SIZE = 3f;
    void ApplySnap(ref ConstructData d)
    {
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        float3 rndedPos = math.round(d.pos / GRID_SIZE) * GRID_SIZE;

        if (TryGetGroundPoint(ref world, rndedPos, out float3 ground))
        {
            d.pos = ground;
        }
    }
    bool CheckValidStructurePlacement(ref ConstructData d)
    {
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        NativeList<int> hits = new NativeList<int>(Allocator.Temp);
        float3 halfExtent = ((float3)d.constructData.size / 2) - new float3(STRUCTURE_CHECK_BEVEL, STRUCTURE_CHECK_BEVEL, STRUCTURE_CHECK_BEVEL);
        var box = new OverlapAabbInput
        {
            Aabb = new Aabb
            {
                Max = d.pos + halfExtent,
                Min = d.pos - halfExtent,
            },
            Filter = STRUCTURE_MASK,
        };

        if (world.OverlapAabb(box, ref hits) && TryGetGroundPoint(ref world, d.pos, out float3 hit))
        {
            hits.Dispose();
            return false;
        }
        hits.Dispose();
        return true;

    }
    //start and end this many units closer
    // Kept at 0f for testing as requested
    public const float WALL_CHECK_RADIUS = 1f;
    bool CheckValidWallPlacement(ConstructWallData d)
    {
        if (d.isSingleVis) { return true; }
        float dist = math.distance(d.start, d.end);

        if (dist < 2f)
            return false;

        float3 dir = math.normalize(d.end - d.start);
        int segmentCount = math.max(1, (int)math.ceil(dist / d.constructData.spacing));
        float spacing = dist / segmentCount;

        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        float3 prev = float3.zero;
        bool hasPrev = false;

        for (int i = 0; i <= segmentCount; i++)
        {
            float3 cur = d.start + dir * (i * spacing);

            if (hasPrev)
            {
                if (!CheckValidSegment(world, cur, prev))
                    return false;
            }

            prev = cur;
            hasPrev = true;
        }

        return true;
    }

    bool CheckValidSegment(PhysicsWorldSingleton world, float3 a, float3 b)
    {
        float3 totalDir = b - a;
        float totalDist = math.length(totalDir);

        float3 dirNorm = math.normalize(totalDir);

        float3 castStart = a + dirNorm * WALL_CHECK_RADIUS * 2;

        float maxDist = totalDist - (2 * WALL_CHECK_RADIUS * 2);

        if (maxDist <= 0)
        {
            return true;
        }

        //UnityEngine.Debug.DrawRay(castStart, dirNorm * maxDist, UnityEngine.Color.red, 1f);

        if (world.SphereCast(
                castStart,
                WALL_CHECK_RADIUS,
                dirNorm, // Direction is normalized
                maxDist,
                STRUCTURE_MASK
            ))
        {
            // If it hit structures on the way
            return false;
        }

        return true;
    }
    // Helper Methods
    void SetValidMat(Entity e, bool valid)
    {
        if (!EntityManager.HasBuffer<LinkedEntityGroup>(e))
        {
            //Parent entity does not have a Child buffer;
            return;
        }

        DynamicBuffer<LinkedEntityGroup> buffer = EntityManager.GetBuffer<LinkedEntityGroup>(e);
        for (int i = 0; i < buffer.Length; i++)
        {
            Entity element = buffer[i].Value;

            // make sure it has a rendering component
            if (EntityManager.HasComponent<MaterialMeshInfo>(element))
            {
                if (SystemAPI.TryGetSingleton<AssetSingleton>(out var m))
                {
                    int mat = valid ? m.ValidMaterialID : m.InvalidMaterialID;
                    var r = EntityManager.GetComponentData<MaterialMeshInfo>(element);
                    r.Material = mat;
                    EntityManager.SetComponentData(element, r);
                }
            }
        }

    }
    public const float DEPTH_TEST_HEIGHT = 10f;


    private bool TryGetStructureFromDB(int key, out Entity e)
    {
        if (SystemAPI.TryGetSingleton<StructureManifest>(out var structDb))
        {
            e = structDb.Manifest[key];
            return true;
        }
        e = Entity.Null; return false;
    }
    private bool TryGetGroundPoint(ref PhysicsWorldSingleton world, float3 pos, out float3 result)
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
    protected override void OnUpdate()
    {
    }

    struct SnapResult
    {
        public float3 position;
        public bool snappedToNode;
    }

}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ColliderCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Find all entities that have our cleanup tag but NO LocalTransform 
        // (This means the entity was "destroyed", but the cleanup tag keeps it alive)
        foreach (var (cleanup, entity) in SystemAPI.Query<ColliderCleanup>()
                     .WithNone<LocalTransform>()
                     .WithEntityAccess())
        {
            // 1. Properly dispose the unmanaged Blob Asset memory
            if (cleanup.ColliderRef.IsCreated)
            {
                cleanup.ColliderRef.Dispose();
            }

            // 2. Remove the cleanup component so the Entity finally disappears
            ecb.RemoveComponent<ColliderCleanup>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
public struct ColliderCleanup : ICleanupComponentData
{
    public BlobAssetReference<Unity.Physics.Collider> ColliderRef;
}

//public struct WallVisualTag : IComponentData {}
public struct StructureVisualTag : IComponentData { }