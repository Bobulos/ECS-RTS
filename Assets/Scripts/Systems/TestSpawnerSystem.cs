using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Physics;
using Random = Unity.Mathematics.Random;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStateSystem))]
[UpdateAfter(typeof(PhysicsSystemGroup))] // Ensure physics world is built
public partial struct TestSpawnerSystem : ISystem
{

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // This system only runs if there is at least one TestSpawner
        state.RequireForUpdate<TestSpawner>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Random currentRandom = new Random((uint)SystemAPI.Time.ElapsedTime+1);
        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        // 4. Create the job instance
        var job = new SpawnerJob
        {
            Filter = new CollisionFilter
            {
                CollidesWith = 1 << 7,
                BelongsTo = CollisionFilter.Default.BelongsTo,
                GroupIndex = 0
            },
            World = world,
            ECB = ecb,
            Random = currentRandom,
            CollisionWorld = collisionWorld,
            CurrentTime = SystemAPI.Time.ElapsedTime,
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);

        state.Dependency.Complete();
    }
    [BurstCompile]
    private partial struct SpawnerJob : IJobEntity
    {
        public CollisionFilter Filter;
        [ReadOnly] public PhysicsWorldSingleton World;
        public EntityCommandBuffer.ParallelWriter ECB;
        public Random Random; // Must be public (or ref) and non-readonly to be writable back
        [ReadOnly] public CollisionWorld CollisionWorld;
        [ReadOnly] public double CurrentTime;

        void Execute(
            Entity entity,
            [ChunkIndexInQuery] int sortKey,
            ref LocalTransform transform,
            ref TestSpawner spawner)
        {
            // Check timer and count
            if (spawner.LastTime + spawner.Rate < CurrentTime &&
                spawner.Count < spawner.MaxCount)
            {
                float3 offset = new float3(0,10,0);
                
                spawner.LastTime = CurrentTime;
                for (int i = 0; i < spawner.Per; i++)
                {
                    float3 pos = transform.Position + Random.NextFloat3(new float3(-spawner.Radius, 0, -spawner.Radius), new float3(spawner.Radius, 0, spawner.Radius));
                    var ray = new RaycastInput
                    {
                        Start = pos + offset,
                        End = pos - offset,
                        Filter = Filter
                    };
                    if (World.CastRay(ray, out var hit))
                    {
                        if (hit.Position.y > 1.5f) continue;
                        Entity e = ECB.Instantiate(sortKey, spawner.Prefab);
                        // Set the position of the newly spawned entity
                        ECB.SetComponent(sortKey, e, LocalTransform.FromPosition(hit.Position));
                    }
                    spawner.Count++;
                    // 5. Instantiate and Configure the New Entity
                    
                }
                    

                // Set initial state and target (using the job's random state for simplicity)
                //ECB.SetComponent(sortKey, e, new UnitTarget { Targ = Entity.Null, Last = CurrentTime });
                //ECB.SetComponent(sortKey, e, new UnitState { State = UnitStates.Idle });
            }
        }
    }
}