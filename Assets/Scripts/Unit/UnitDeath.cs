using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
public partial struct UnitDeadTagSystem : ISystem
{
    // Run this system after other logic that reduces HP
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        Entity explosion = SystemAPI.GetSingleton<FXManifest>().Explosion;
        //var l = SystemAPI.GetComponentLookup<UnitHP>();
        foreach (var (hp, transform, e) in SystemAPI.Query<RefRO<UnitHP>, RefRO<LocalTransform>>().WithNone<DeadTag>().WithEntityAccess())
        {
            if (hp.ValueRO.HP <= 0)
            {
                ecb.AddComponent<DeadTag>(e);
                var d = ecb.Instantiate(explosion);
                ecb.SetComponent(d, new LocalTransform { Position = transform.ValueRO.Position, Rotation = quaternion.identity, Scale = 1f });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
public partial struct TagDeadUnitsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    public int sortKey;

    // Automatically queries all entities with UnitHP
    void Execute(ref UnitHP hp, Entity entity)
    {
        if (hp.HP <= 0)
        {
            // Add a DeadTag safely in parallel
            ECB.AddComponent<DeadTag>(sortKey, entity);
        }
    }
}

// Separate system to destroy entities with DeadTag
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStateSystem))]
public partial struct DestroyDeadUnitsSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var ecbSys = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecbD = ecbSys.CreateCommandBuffer(state.WorldUnmanaged);
        //var c = SystemAPI.GetComponentLookup<PhysicsCollider>(true);

        var b = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

        foreach (var (t, d, e) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<DeadTag>>().WithEntityAccess())
        {
            if (b.TryGetBuffer(e, out var l))
            {
                foreach (var i in l)
                {
                    ecb.DestroyEntity(i.Value);
                }
            }
            ecb.DestroyEntity(e);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
public partial struct DestroyDeadJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in DeadTag dead)
    {
        // Destroy the entity safely at the end of the frame
        ECB.DestroyEntity(sortKey, entity);
    }
}

// Simple tag component
public struct DeadTag : IComponentData { }
