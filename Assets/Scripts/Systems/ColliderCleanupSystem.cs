using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ColliderCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Find all entities that have our cleanup tag but NO LocalTransform 
        // (This means the entity was "destroyed", but the cleanup tag keeps it alive)
        foreach (var (cleanup, entity) in SystemAPI.Query<ColliderCleanup>()
                     .WithNone<LocalTransform>()
                     .WithEntityAccess())
        {
            // 1. Properly dispose the unmanaged Blob Asset memory
            if (cleanup.ColliderRef.IsCreated)
            {
                cleanup.ColliderRef.Dispose();
            }

            // 2. Remove the cleanup component so the Entity finally disappears
            ecb.RemoveComponent<ColliderCleanup>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
public struct ColliderCleanup : ICleanupComponentData
{
    public BlobAssetReference<Unity.Physics.Collider> ColliderRef;
}
