using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStateSystem))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct TestSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _done = true;
    }
    bool _done;
    const float sqr_radius = 80f;
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_done) { return; }
        _done = true;
        /*        var ecb = SystemAPI
                    .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged);

        */

        var prefab = SystemAPI.GetSingletonBuffer<UnitManifest>()[4].Value;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        ecb.Instantiate(prefab);
        for (float x = -sqr_radius; x < sqr_radius;  x+=1.5f)
        {
            for (float z = -sqr_radius; z < sqr_radius; z += 1.5f)
            {
                var e = ecb.Instantiate(prefab);
                ecb.SetComponent(e, new LocalTransform
                {
                    Position = new float3(x,0,z),
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();



  /*      var job = new SpawnerJob
        {
            Filter = new CollisionFilter
            {
                CollidesWith = 1 << 7,
                BelongsTo = CollisionFilter.Default.BelongsTo,
                GroupIndex = 0
            },
            CollisionWorld = collisionWorld,
            ECB = ecb,
            CurrentTime = SystemAPI.Time.ElapsedTime
        };

   
        state.Dependency = job.Schedule(state.Dependency);
        state.Dependency.Complete();*/
    }

    [BurstCompile]
    private partial struct SpawnerJob : IJobEntity
    {
        public CollisionFilter Filter;
        [ReadOnly] public CollisionWorld CollisionWorld;
        public EntityCommandBuffer ECB;
        [ReadOnly] public double CurrentTime;

        void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref TestSpawner spawner)
        {
            if (spawner.Count >= spawner.MaxCount)
                return;

            if (spawner.LastTime + spawner.Rate > CurrentTime)
                return;

            spawner.LastTime = CurrentTime;

            float3 offset = new float3(0, 10, 0);

            int gridX = (int)math.ceil(math.sqrt(spawner.Per));
            int gridZ = (int)math.ceil((float)spawner.Per / gridX);

            float spacingX = gridX > 1 ? (2f * spawner.Radius) / (gridX - 1) : 0;
            float spacingZ = gridZ > 1 ? (2f * spawner.Radius) / (gridZ - 1) : 0;

            int spawnedThisTick = 0;

            for (int z = 0; z < gridZ && spawnedThisTick < spawner.Per; z++)
            {
                for (int x = 0; x < gridX && spawnedThisTick < spawner.Per; x++)
                {
                    if (spawner.Count >= spawner.MaxCount)
                        return;

                    float xPos = -spawner.Radius + (x * spacingX);
                    float zPos = -spawner.Radius + (z * spacingZ);

                    float3 pos = transform.Position + new float3(xPos, 0, zPos);

                    var ray = new RaycastInput
                    {
                        Start = pos + offset,
                        End = pos - offset,
                        Filter = Filter
                    };

                    if (!CollisionWorld.CastRay(ray, out var hit))
                        continue;

                    if (hit.Position.y > 1.5f)
                        continue;

                    Entity e = ECB.Instantiate(spawner.Prefab);
                    ECB.SetComponent(e, LocalTransform.FromPosition(hit.Position));
                    ECB.AddComponent(e, new UnitMoveOrder { Dest = hit.Position });

                    spawner.Count++;
                    spawnedThisTick++;
                }
            }
        }
    }
}
