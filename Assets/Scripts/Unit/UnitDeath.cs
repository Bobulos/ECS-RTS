using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;


// Separate system to destroy entities with DeadTag
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStateSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct DestroyDeadUnitsSystem : ISystem
{
    JobHandle _prevJob;
    NativeReference<int> _prevKilled;
    bool _jobSet;
    public void OnCreate(ref SystemState state)
    {
        _jobSet = false;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<FXManifest>(out var m) || !SystemAPI.TryGetSingletonRW<GameStats>(out var stats)) return;

        var ecbSys = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();


        //try readback previous job
        if (_jobSet && _prevJob.IsCompleted)
        {
            stats.ValueRW.Killed += _prevKilled.Value;
            _prevKilled.Dispose();
        }

        //var c = SystemAPI.GetComponentLookup<PhysicsCollider>(true);
        _prevKilled = new NativeReference<int>(Allocator.TempJob);
        _prevKilled.Value = 0;

        
        var job = new DestroyAndTagDeadJob
        {
            Ecb = ecbSys.CreateCommandBuffer(state.WorldUnmanaged),
            Explosion = m.Explosion,
            Killed = _prevKilled,
        };

            


        var handle = job.Schedule(state.Dependency);
        state.Dependency = handle;
        
        _jobSet = true;
        _prevJob = handle;
        
    }
}
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct DestroyAndTagDeadJob : IJobEntity
{
    public Entity Explosion;
    public EntityCommandBuffer Ecb;
    public NativeReference<int> Killed;
    void Execute(Entity entity, RefRO<LocalTransform> transform, RefRO<UnitHP> hp)
    {
        if (!(hp.ValueRO.HP <= 0)) return;

        Killed.Value ++;
        var e = Ecb.Instantiate(Explosion);
        Ecb.SetComponent(e, new LocalTransform
        {
            Position = transform.ValueRO.Position,
            Rotation = quaternion.identity,
            Scale = 1f
        });

        Ecb.AddComponent(entity, new DeadTag{Value = 1});
        // Destroy the entity safely at the end of the frame
        Ecb.DestroyEntity(entity);
    }
}

// Simple tag component
[GhostComponent]
public struct DeadTag : IComponentData { [GhostField] public byte Value; }
