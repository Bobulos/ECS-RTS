using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GoInGameServerSystem : ISystem
{
    int _count;
    public void OnCreate(ref SystemState state)
    {
        _count = 0;
        //need at least one player to join before we run the system
        //state.RequireForUpdate<NetworkId>();
        //state.RequireForUpdate<PlayerSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        //var playerPrefab = SystemAPI.GetSingleton<PlayerSpawner>().PlayerPrefab;

        foreach (var (rpc, rpcEntity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
            .WithAll<GoInGameRequestRpc>()
            .WithEntityAccess())
        {
            var connectionEntity = rpc.ValueRO.SourceConnection;
            
            var networkId = SystemAPI.GetComponent<NetworkId>(connectionEntity);
            UnityEngine.Debug.Log($"<color=blue>[Server] Client {networkId.Value} joined — approving</color>");
            ecb.DestroyEntity(rpcEntity);
            ecb.AddComponent<NetworkStreamInGame>(connectionEntity);
            // Create player entity — only needs to exist as an RPC command target
            foreach (var (_, entity) in SystemAPI.Query<UnConsumedPlayerTag>().WithAll<PlayerTag>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new GhostOwner { NetworkId = networkId.Value });
                ecb.AddComponent(connectionEntity, new CommandTarget { Value = entity });
                ecb.RemoveComponent<UnConsumedPlayerTag>(entity);
                break; // only one player entity for now
            }
            
            //ecb.SetComponent(connectionEntity, new CommandTarget { Value = playerPrefab });
                        // No prefab needed, just create it directly
            // var playerEntity = ecb.CreateEntity();
            // ecb.AddComponent(playerEntity, new GhostOwner { NetworkId = networkId.Value });
            // ecb.AddComponent(connectionEntity, new CommandTarget { Value = playerEntity });

            // Tell the client that they where approved
            var approvedRpcEntity = ecb.CreateEntity();
            ecb.AddComponent(approvedRpcEntity, new GoInGameApprovedRpc());
            ecb.AddComponent(approvedRpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = connectionEntity
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}