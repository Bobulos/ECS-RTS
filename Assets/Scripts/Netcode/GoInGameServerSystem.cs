using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GoInGameServerSystem : ISystem
{
    
    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (rpc, entity) in 
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
            .WithAll<GoInGameRequestRpc>()
            .WithEntityAccess())
        {
            ecb.AddComponent(rpc.ValueRO.SourceConnection, new NetworkStreamInGame());
            
            ecb.DestroyEntity(entity);

            UnityEngine.Debug.Log("Client go in game request received");
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
