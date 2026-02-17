using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct GoInGameClientSystem : ISystem
{
    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (NetworkId, entity) in 
        SystemAPI.Query<RefRO<NetworkId>>()
        .WithNone<NetworkStreamInGame>().WithEntityAccess())
        {
            ecb.AddComponent<NetworkStreamInGame>(entity);
            UnityEngine.Debug.Log("Client sent go in game request");


            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new GoInGameRequestRpc());
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest());
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
public struct GoInGameRequestRpc : IRpcCommand
{
    
}