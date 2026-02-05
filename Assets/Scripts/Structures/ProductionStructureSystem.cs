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

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (transform, team, prod) in SystemAPI.Query<
            RefRO<LocalTransform>,
            RefRO<Team>,
            RefRW<ProductionStructure>>())
        {
            if (prod.ValueRO.QueueCount <= 0) continue;
            if (time - prod.ValueRO.StartTime >= manifest[prod.ValueRO.Queue[0]].TrainingTime)
            {
                //Get que unit index
                var e = ecb.Instantiate(manifest[prod.ValueRO.Queue[0]].Unit);

                prod.ValueRW.Queue.RemoveAt(0);
                prod.ValueRW.QueueCount--;
                prod.ValueRW.StartTime = time;

                ecb.SetComponent(e, new Team { TeamID = team.ValueRO.TeamID });
                ecb.SetComponent(e, new LocalTransform
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
                ecb.AddComponent(e, new UnitMoveOrder 
                { Dest = prod.ValueRO.RallyPoint, });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}