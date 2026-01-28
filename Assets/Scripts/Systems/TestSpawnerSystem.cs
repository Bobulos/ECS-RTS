using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

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
        Random currentRandom = new Random((uint)SystemAPI.Time.ElapsedTime + 1);
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
                float3 offset = new float3(0, 10, 0);

                spawner.LastTime = CurrentTime;

                // Calculate grid dimensions
                int gridSize = (int)math.ceil(math.sqrt(spawner.Per));
                float spacing = (2f * spawner.Radius) / math.max(1, gridSize - 1);

                int spawned = 0;
                for (int x = 0; x < gridSize && spawned < spawner.Per; x++)
                {
                    for (int z = 0; z < gridSize && spawned < spawner.Per; z++)
                    {
                        // Calculate grid position relative to center
                        float xPos = -spawner.Radius + (x * spacing);
                        float zPos = -spawner.Radius + (z * spacing);

                        // Check if position is within radius (circular boundary)
                        float distFromCenter = math.sqrt(xPos * xPos + zPos * zPos);
                        if (distFromCenter > spawner.Radius)
                            continue;

                        float3 pos = transform.Position + new float3(xPos, 0, zPos);
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
                            ECB.AddComponent(sortKey, e, new UnitMoveOrder { Dest = hit.Position });

                            spawned++;
                            spawner.Count++;
                        }
                    }
                }
            }
        }
    }
}