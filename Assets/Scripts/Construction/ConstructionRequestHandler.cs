using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ConstructionRequestHandler : ISystem
{
    private CollisionFilter _structureFilter;
    private CollisionFilter _terrainFilter;
    private void OnCreate()
    {
        _structureFilter = new CollisionFilter
        {
            CollidesWith = 1 << 8,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
        _terrainFilter = new CollisionFilter
        {
            CollidesWith = 1 << 7,
            BelongsTo = CollisionFilter.Default.BelongsTo,
            GroupIndex = 0
        };
    }
    const float BUILD_ARRIVE = 5f;
    const float STRUCTURE_CHECK_BEVEL = 0.3f;
    private void OnUpdate(ref SystemState state)
    {
        //UnityEngine.Debug.Log("FOUND A BUFFER");
        if (!SystemAPI.TryGetSingletonBuffer<StructureManifest>(out var manifest)) return;
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;

        //var ecbSys = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        //UnityEngine.Debug.Log("RUNNING");

        foreach (var (transform, work, team) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<Worker>, RefRO<Team>>())
        {
            //UnityEngine.Debug.Log("FOUND SOME FELLAS");
            //UnityEngine.Debug.Log(BMath.DistXZsq(transform.ValueRO.Position, work.ValueRO.BuildRequest.Dest));
            //if (work.ValueRO.HasRequest) UnityEngine.Debug.Log(BMath.DistXZsq(transform.ValueRO.Position, work.ValueRO.BuildRequest.Dest));
            if (work.ValueRO.HasRequest
             && BMath.DistXZ(transform.ValueRO.Position, work.ValueRO.BuildRequest.Dest) <= BUILD_ARRIVE)
            {
                work.ValueRW.HasRequest = false;
                //check if valid with aab
                if (!CheckValidStructurePlacement(physicsWorld, work.ValueRO.BuildRequest.Dest, work.ValueRO.BuildRequest.Size)) continue;
                //UnityEngine.Debug.Log("BUILD FROM REQUEST");
                //can build the guy
                var e = ecb.Instantiate(manifest[work.ValueRO.BuildRequest.PrimaryKey].Value);
                ecb.SetComponent(e, new LocalTransform {Position = work.ValueRO.BuildRequest.Dest, Rotation = quaternion.identity , Scale = 1f});
                ecb.SetComponent(e, new Team { TeamID = team.ValueRO.TeamID});
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        // foreach (var r in requests)
        // {
        //     UnityEngine.Debug.Log($"Request {r}");
        //     r.Position

        // }
    }
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
            Filter = _structureFilter,
        };

        bool hasOverlap = world.OverlapAabb(box, ref hits);
        hits.Dispose();
        //UnityEngine.Debug.Log($"has overlap of {hasOverlap}");
        return !hasOverlap;
    }
    
    // private const float MAX_RAY_LENGTH = 500f;
    // float3 GetGroundPositionFromRay(PhysicsWorldSingleton world, float3 origin, float3 direction)
    // {
    //     var rayIn = new RaycastInput
    //     {
    //         Start = origin,
    //         End = origin + (direction * MAX_RAY_LENGTH),
    //         Filter = _terrainFilter
    //     };

    //     if (world.CastRay(rayIn, out var hit))
    //     {
    //         return hit.Position;
    //     }
        
    //     return origin; // Fallback to origin if no hit
    // }
}
public struct UnderConstruction : IComponentData
{
    public float Start;
}
public struct Worker : IComponentData
{
    public bool HasRequest;
    public ConstructionRequest BuildRequest;
    //public float3 BuildDest;
    public FixedList128Bytes<int> ConstructKeys;
}
