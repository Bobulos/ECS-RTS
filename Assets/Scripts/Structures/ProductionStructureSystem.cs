using Unity.Burst;
using Unity.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
/// Handles performance intenseive operations and non player controlled data mutation 
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(UnitMovement)), BurstCompile]
public partial struct ProductionStructureSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonBuffer<UnitManifest>(out var manifest)) return;

        var ecbSys = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        var m = manifest.ToNativeArray(Allocator.TempJob);
        
        float time = (float)SystemAPI.Time.ElapsedTime;

        var job = new ProductionJob
        {
            Manifest = m,
            Ecb = ecbSys.CreateCommandBuffer(state.World.Unmanaged),
            Time = time,
        };
        job.Schedule();
        // var handle = job.Schedule(state.Dependency);
        // state.Dependency = handle;
    }
    [BurstCompile]
    private partial struct ProductionJob : IJobEntity
    {
        [ReadOnly,DeallocateOnJobCompletion] public NativeArray<UnitManifest> Manifest;
        public EntityCommandBuffer Ecb;
        [ReadOnly]public float Time;
        [BurstCompile]
        void Execute(
            RefRO<LocalTransform> transform,
            RefRO<Team> team,
            RefRW<ProductionStructure> prod)
        {
            if (prod.ValueRO.QueueCount <= 0) return;
            if (Time - prod.ValueRO.StartTime >= Manifest[prod.ValueRO.Queue[0]].TrainingTime)
            {
                //Get que unit index
                var e = Ecb.Instantiate(Manifest[prod.ValueRO.Queue[0]].Unit);

                prod.ValueRW.Queue.RemoveAt(0);
                prod.ValueRW.QueueCount--;
                prod.ValueRW.StartTime = Time;

                Ecb.SetComponent(e, new Team { TeamID = team.ValueRO.TeamID });
                Ecb.SetComponent(e, new LocalTransform
                {
                    Position = transform.ValueRO.Position + prod.ValueRO.SpawnOffset,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                float3 dest;
                if (math.distancesq(prod.ValueRO.RallyPoint, float3.zero) < 0.1f) 
                //Default rally point
                {
                    dest = transform.ValueRO.Position + 5 * prod.ValueRO.SpawnOffset;
                } else
                {
                    dest = prod.ValueRO.RallyPoint;
                }
                var l = new FixedList512Bytes<OrderElement>();
                l.Add(new OrderElement
                {
                    Type = OrderType.Move,
                    Position = dest,
                    Data = -1//unused
                });
                Ecb.SetComponent(e, new OrderList { Value = l});
            }
        }
    }
}