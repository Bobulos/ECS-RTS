using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;


// Separate system to destroy entities with DeadTag
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStateSystem))]
public partial struct DestroyDeadUnitsSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<FXManifest>(out var m)) return;

        var ecbSys = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        //var c = SystemAPI.GetComponentLookup<PhysicsCollider>(true);
        
        var job = new DestroyAndTagDeadJob
        {
            Ecb = ecbSys.CreateCommandBuffer(state.WorldUnmanaged),
            Explosion = m.Explosion,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }
}
[BurstCompile]
public partial struct DestroyAndTagDeadJob : IJobEntity
{
    public Entity Explosion;
    public EntityCommandBuffer Ecb;
    void Execute(Entity entity, RefRO<LocalTransform> transform, RefRO<UnitHP> hp)
    {
        if (!(hp.ValueRO.HP <= 0)) return;

        var e = Ecb.Instantiate(Explosion);
        Ecb.SetComponent(e, new LocalTransform
        {
            Position = transform.ValueRO.Position,
            Rotation = quaternion.identity,
            Scale = 1f
        });

        Ecb.AddComponent<DeadTag>(entity);
        // Destroy the entity safely at the end of the frame
        Ecb.DestroyEntity(entity);
    }
}

// Simple tag component
public struct DeadTag : IComponentData { }
