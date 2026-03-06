using System;
using System.Diagnostics;
using Construction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine.UIElements;

// public struct ConstructionRequest : IBufferElementData
// {
//     public float3 Position;

//     public int Key;
// }

public struct ConstructionRequest : IComponentData
{
    public int Key;
    public int TeamID;
    public float3 Position;
}
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ConstructionRequestHandler : ISystem
{
    // private CollisionFilter _structureFilter;
    // private CollisionFilter _terrainFilter;
    // private void OnCreate()
    // {
    //     _structureFilter = new CollisionFilter
    //     {
    //         CollidesWith = 1 << 8,
    //         BelongsTo = CollisionFilter.Default.BelongsTo,
    //         GroupIndex = 0
    //     };
    //     _terrainFilter = new CollisionFilter
    //     {
    //         CollidesWith = 1 << 7,
    //         BelongsTo = CollisionFilter.Default.BelongsTo,
    //         GroupIndex = 0
    //     };
    // }
    //const float BUILD_ARRIVE = 5f;
    const float STRUCTURE_CHECK_BEVEL = 0.3f;
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;
        if (!SystemAPI.TryGetSingletonBuffer<StructureManifest>(out var manifest)) return;
        if (!SystemAPI.TryGetSingletonBuffer<ConstructionDataManifest>(out var construction)) return;

        var ecbSys = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        var ecb = ecbSys.CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (req, entity) in SystemAPI.Query<ConstructionRequest>().WithEntityAccess())
        {
            //UnityEngine.Debug.Log($"is valid of {CheckValidStructurePlacement(physicsWorld, req.Position, construction[req.Key].Size)}");
            if (CheckValidStructurePlacement(physicsWorld, req.Position, construction[req.Key].Size))
            {
                var s = ecb.Instantiate(manifest[req.Key].Value);
                ecb.SetComponent(s, new LocalTransform
                {
                    Position = req.Position,
                    Scale = 1f,
                    Rotation = quaternion.identity
                });
                ecb.SetComponent(s, new Team{TeamID = req.TeamID});
            }

            ecb.DestroyEntity(entity);
            //UnityEngine.Debug.Log("There is a build request");
        }
    }
    
    private bool CheckValidStructurePlacement(PhysicsWorldSingleton world, float3 position, int3 size)
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
            Filter =  new CollisionFilter
            {
                CollidesWith = 1 << 8,
                BelongsTo = CollisionFilter.Default.BelongsTo,
                GroupIndex = 0
            },
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
    // public bool HasRequest;
    // public ConstructionRequest BuildRequest;
    //public float3 BuildDest;
    public FixedList128Bytes<int> ConstructKeys;
}
