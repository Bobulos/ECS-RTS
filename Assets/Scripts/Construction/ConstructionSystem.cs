using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

[UpdateAfter(typeof(TurretLookSystem))]
public partial class ConstructionSystem : SystemBase
{
    const float SEGEMENT_SIZE_OFFSET = 1f;
    const float MAX_RAY_LENGTH = 400f;
    const float STRUCTURE_CHECK_BEVEL = 0.3f;
    const float GRID_SIZE = 3f;
    const float WALL_CHECK_RADIUS = 1f;
    const float DEPTH_TEST_HEIGHT = 10f;
    
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
        UnitActionManager.VisualizeStructure += VisualizeStructure;
        UnitActionManager.CancelStructure += CancelConstruction;

        ConstructionBridge.VisualizeWalls += VisualizeWalls;
        ConstructionBridge.ConstructWalls += ConstructWalls;
        ConstructionBridge.CancelContrstruction += CancelConstruction;
        ConstructionBridge.VisualizeStructure += VisualizeStructure;
        ConstructionBridge.ConstructStructure += ConstructStructure;
    }
    
    protected override void OnDestroy()
    {
        UnitActionManager.VisualizeStructure -= VisualizeStructure;
        UnitActionManager.CancelStructure -= CancelConstruction;

        ConstructionBridge.VisualizeWalls -= VisualizeWalls;
        ConstructionBridge.VisualizeStructure -= VisualizeStructure;
        ConstructionBridge.ConstructWalls -= ConstructWalls;
        ConstructionBridge.CancelContrstruction -= CancelConstruction;
        ConstructionBridge.ConstructStructure -= ConstructStructure;
    }
    
    protected override void OnUpdate() { }

    #region Public Methods
    
    void CancelConstruction()
    {
        //UnityEngine.Debug.Log("Cancel construction");
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var lookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>();
        foreach (var (t, e) in SystemAPI.Query<StructureVisualTag>().WithEntityAccess())
        {
            if (lookup.TryGetBuffer(e, out var l))
            {
                foreach (var c in l)
                {
                    ecb.DestroyEntity(c.Value);
                }
            }

            ecb.DestroyEntity(e);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    void ConstructWalls(ConstructWallData d, int team)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;
        
        // Snap both points
        float3 snappedStart = SnapWallPoint(world, d.start);
        float3 snappedEnd = SnapWallPoint(world, d.end);
        
        // Calculate direction and distance from snapped points
        float dist = math.distance(snappedStart, snappedEnd);
        if (dist < 0.01f) return;
        
        float3 dir = math.normalize(snappedEnd - snappedStart);
        
        if (!CheckValidWallPlacement(world, snappedStart, dir, dist, d.constructData.spacing, false))
            return;

        var ecbSys = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSys.CreateCommandBuffer(World.Unmanaged);

        int segmentCount = math.max(1, (int)math.ceil(dist / d.constructData.spacing));
        float actualSpacing = dist / segmentCount;

        float3 prevNode = float3.zero;
        bool hasPrev = false;

        for (int i = 0; i <= segmentCount; i++)
        {
            float3 pos = snappedStart + dir * (i * actualSpacing);

            if (!TryGetStructureFromDB(d.constructData.primary.key, out Entity nodePrefab))
                continue;

            var node = ecb.Instantiate(nodePrefab);
            ecb.SetComponent(node, new LocalTransform
            {
                Position = pos,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            ecb.SetComponent(node, new Team
            {
                TeamID = team,
                UnitID = 0,
            });

            // Build segment between nodes
            if (hasPrev && TryGetStructureFromDB(d.constructData.secondary.key, out Entity segmentPrefab))
            {
                float3 midpoint = (prevNode + pos) * 0.5f;
                float3 forward = math.normalize(pos - prevNode);
                float segLength = math.distance(prevNode, pos);

                var segment = EntityManager.Instantiate(segmentPrefab);

                ecb.SetComponent(segment, new LocalTransform
                {
                    Position = midpoint,
                    Rotation = quaternion.LookRotationSafe(forward, math.up()),
                    Scale = 1f
                });
                ecb.SetComponent(segment, new Team
                {
                    TeamID = team,
                    UnitID = 0,
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

                    ecb.AddComponent(segment, new ColliderCleanup { ColliderRef = col });
                    ecb.SetComponent(segment, new PhysicsCollider { Value = col });
                }

                ecb.AddComponent<PostTransformMatrix>(segment);
                ecb.SetComponent(segment, new PostTransformMatrix
                {
                    Value = float4x4.Scale(new float3(1f, 1f, segLength - SEGEMENT_SIZE_OFFSET))
                });
            }

            prevNode = pos;
            hasPrev = true;
        }
    }
    
    void ConstructStructure(ConstructData d, int team)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;

        // Get ground position from raycast
        float3 groundPos = GetGroundPositionFromRay(world, d.Origin, d.Dir);
        
        // Apply grid snapping
        float3 snappedPos = SnapToGrid(world, groundPos);
        
        if (!CheckValidStructurePlacement(world, snappedPos, d.Data.size))
            return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        if (TryGetStructureFromDB(d.Data.primary.key, out Entity prefab))
        {
            var e = ecb.Instantiate(prefab);
            ecb.SetComponent(e, new LocalTransform
            {
                Position = snappedPos,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            ecb.SetComponent(e, new Team
            {
                TeamID = team,
                UnitID = 0,
            });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    void VisualizeStructure(ConstructData d)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;

        // Get ground position from raycast
        float3 groundPos = GetGroundPositionFromRay(world, d.Origin, d.Dir);
        
        // Apply grid snapping
        float3 snappedPos = SnapToGrid(world, groundPos);
        
        bool isValid = CheckValidStructurePlacement(world, snappedPos, d.Data.size);

        // Update or create visual
        if (SystemAPI.TryGetSingletonEntity<StructureVisualTag>(out Entity vis))
        {
            EntityManager.SetComponentData(vis, new LocalTransform
            {
                Position = snappedPos,
                Scale = 1,
                Rotation = quaternion.identity,
            });
            SetValidMat(vis, isValid);
        }
        else
        {
            if (TryGetStructureFromDB(d.Data.primary.key, out var prefab))
            {
                var e = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(e, new LocalTransform
                {
                    Position = snappedPos,
                    Scale = 1,
                    Rotation = quaternion.identity,
                });
                EntityManager.AddComponent<StructureVisualTag>(e);
                EntityManager.RemoveComponent<PhysicsCollider>(e);
                //vision
                EntityManager.RemoveComponent<Vision>(e);
                EntityManager.RemoveComponent<LocalVisibility>(e);
                if (EntityManager.HasComponent<ProductionStructure>(e))
                {
                    EntityManager.RemoveComponent<ProductionStructure>(e);
                }
                if (EntityManager.HasComponent<UnitHP>(e))
                {
                    EntityManager.RemoveComponent<UnitHP>(e);
                }
                SetValidMat(e, isValid);
            }
        }
    }
    
    void VisualizeWalls(ConstructWallData d)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;
        if (!TryGetStructureFromDB(d.constructData.primary.key, out Entity prefab))
            return;

        // Snap both points
        float3 snappedStart = SnapWallPoint(world, d.start);
        float3 snappedEnd = SnapWallPoint(world, d.end);
        
        // Calculate direction and distance from snapped points
        float dist = math.distance(snappedStart, snappedEnd);
        float3 dir = dist > 0.001f ? math.normalize(snappedEnd - snappedStart) : float3.zero;
        
        int segmentCount = dist > 0 ? (int)math.ceil(dist / d.constructData.spacing) : 0;
        float actualSpacing = dist > 0 && segmentCount > 0 ? dist / segmentCount : 0;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var visualQuery = SystemAPI.QueryBuilder().WithAll<StructureVisualTag>().Build();
        var existingVisuals = visualQuery.ToEntityArray(Allocator.TempJob);

        // Shed excess visuals
        for (int i = segmentCount + 1; i < existingVisuals.Length; i++)
        {
            ecb.DestroyEntity(existingVisuals[i]);
        }

        // Grow - create new visuals if needed
        for (int i = existingVisuals.Length; i < segmentCount + 1; i++)
        {
            var newVisual = ecb.Instantiate(prefab);
            ecb.RemoveComponent<PhysicsCollider>(newVisual);
            ecb.AddComponent<StructureVisualTag>(newVisual);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
        existingVisuals.Dispose();

        // Refresh visual array
        existingVisuals = visualQuery.ToEntityArray(Allocator.TempJob);

        bool valid = CheckValidWallPlacement(world, snappedStart, dir, dist, d.constructData.spacing, d.isSingleVis);

        // Place visuals along the line
        for (int i = 0; i <= segmentCount; i++)
        {
            if (i >= existingVisuals.Length) break;

            float3 pos = snappedStart + dir * (i * actualSpacing);
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

    #endregion

    #region Snapping Methods
    
    /// <summary>
    /// Snaps a wall point to nearby wall nodes or ground
    /// </summary>
    float3 SnapWallPoint(PhysicsWorldSingleton world, float3 position)
    {
        var wallNodeLookup = SystemAPI.GetComponentLookup<WallNode>(true);
        var posLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
        float3 snappedPos = position;
        bool snappedToNode = false;

        // Check for nearby wall nodes
        if (world.OverlapSphere(position, WALL_CHECK_RADIUS, ref hits, STRUCTURE_MASK))
        {
            float minDistance = math.INFINITY;

            foreach (var hit in hits)
            {
                if (!wallNodeLookup.HasComponent(hit.Entity)) continue;

                if (hit.Distance < minDistance)
                {
                    minDistance = hit.Distance;
                    snappedPos = posLookup.GetRefRO(hit.Entity).ValueRO.Position;
                    snappedToNode = true;
                }
            }
        }

        hits.Dispose();

        // Ground snap if not snapped to a node
        if (!snappedToNode && TryGetGroundPoint(world, snappedPos, out float3 groundPos))
        {
            snappedPos = groundPos;
        }

        return snappedPos;
    }
    
    /// <summary>
    /// Snaps a position to the construction grid and ground
    /// </summary>
    float3 SnapToGrid(PhysicsWorldSingleton world, float3 position)
    {
        float3 gridSnapped = math.round(position / GRID_SIZE) * GRID_SIZE;
        
        if (TryGetGroundPoint(world, gridSnapped, out float3 groundPos))
        {
            return groundPos;
        }
        
        return gridSnapped;
    }
    
    /// <summary>
    /// Gets ground position from origin and direction raycast
    /// </summary>
    float3 GetGroundPositionFromRay(PhysicsWorldSingleton world, float3 origin, float3 direction)
    {
        var rayIn = new RaycastInput
        {
            Start = origin,
            End = origin + (direction * MAX_RAY_LENGTH),
            Filter = TERRAIN_MASK
        };

        if (world.CastRay(rayIn, out var hit))
        {
            return hit.Position;
        }
        
        return origin; // Fallback to origin if no hit
    }

    #endregion

    #region Validation Methods
    
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
    
    bool CheckValidWallPlacement(PhysicsWorldSingleton world, float3 origin, float3 direction, 
        float distance, float spacing, bool isSingleVis)
    {
        if (isSingleVis) return true;
        if (distance < 2f) return false;

        int segmentCount = math.max(1, (int)math.ceil(distance / spacing));
        float actualSpacing = distance / segmentCount;

        float3 prev = float3.zero;
        bool hasPrev = false;

        for (int i = 0; i <= segmentCount; i++)
        {
            float3 cur = origin + direction * (i * actualSpacing);

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

        return !world.SphereCast(castStart, WALL_CHECK_RADIUS, dirNorm, maxDist, STRUCTURE_MASK);
    }

    #endregion

    #region Helper Methods
    
    void SetValidMat(Entity e, bool valid)
    {
        if (!EntityManager.HasBuffer<LinkedEntityGroup>(e))
            return;

        DynamicBuffer<LinkedEntityGroup> buffer = EntityManager.GetBuffer<LinkedEntityGroup>(e);
        
        for (int i = 0; i < buffer.Length; i++)
        {
            Entity element = buffer[i].Value;

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

    private bool TryGetStructureFromDB(int key, out Entity e)
    {
        if (SystemAPI.TryGetSingletonBuffer<StructureManifest>(out var structDb))
        {
            e = structDb[key].Value;
            return true;
        }
        e = Entity.Null;
        return false;
    }

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

    #endregion
}

public struct StructureVisualTag : IComponentData { }