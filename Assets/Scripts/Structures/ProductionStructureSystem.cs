using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
[GenerateTestsForBurstCompatibility]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ProductionStructureSystem : ISystem
{
    const int MAX_QUEUE_SIZE = 8;
    private int _count;
    public void OnCreate(ref SystemState state)
    {
        _count = 1;
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        DynamicBuffer<UnitManifest> manifest;
        if (SystemAPI.TryGetSingletonBuffer<UnitManifest>(out var m))
        {
            manifest = m;
        }
        else { return; }
        
        foreach (var (transform, prod) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<ProductionStructure>>()) 
        {
            //enough room in here
            if (_count > 0 && prod.ValueRO.QueueCount < prod.ValueRO.Queue.Capacity-1)
            {
                prod.ValueRW.Queue.Add(prod.ValueRO.Prefabs[0]);
                prod.ValueRW.QueueCount++;
                _count--;
            } else if (prod.ValueRO.QueueCount > 0)
            {
                //Get que unit index
                var e = ecb.Instantiate(manifest[prod.ValueRO.Queue[0]].Value);
                //optimize this later
                prod.ValueRW.Queue.RemoveAt(0);
                prod.ValueRW.QueueCount--;
                ecb.SetComponent(e, new LocalTransform
                {
                    Position = transform.ValueRO.Position+prod.ValueRO.SpawnOffset,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
            }
            
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
