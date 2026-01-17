using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public partial struct ProductionStructureSystem : ISystem
{
    const int MAX_QUEUE_SIZE = 8;
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (transform, prod) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<ProductionStructure>>()) 
        {
            //if (prod.ValueRO.Queue.Length)
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
