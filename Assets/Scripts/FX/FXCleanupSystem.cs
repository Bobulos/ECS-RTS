using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

partial struct FXCleanupSystem : ISystem
{
    private const float MAX_FX_LIFETIME = 5f;
    private const int MAX_DESTROY_PER_FRAME = 30;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        int destroyed = 0;
        float dt = SystemAPI.Time.DeltaTime;
        foreach(var (t, e) in SystemAPI.Query<RefRW<TempFX>>().WithEntityAccess())
        {
            if (destroyed >= MAX_DESTROY_PER_FRAME) break;

            t.ValueRW.Life += dt;
            if (t.ValueRO.Life > MAX_FX_LIFETIME)
            {
                ecb.DestroyEntity(e);
                destroyed++;
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
public struct TempFX : IComponentData
{
    public float Life;
}