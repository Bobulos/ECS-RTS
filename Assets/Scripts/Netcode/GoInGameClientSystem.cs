using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct GoInGameClientSystem : ISystem
{
    private EntityQuery _netIdQuery;
    public void OnCreate(ref SystemState state)
    {
        _netIdQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Connected but not yet in-game -> send GoInGame RPC and update team
        foreach (var (_, connectionEntity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<NetworkStreamInGame, SentGoInGame>()
            .WithEntityAccess())
        {
            SystemAPI.GetSingletonRW<LocalPlayerData>().ValueRW.TeamID = _netIdQuery.CalculateEntityCount();
            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new GoInGameRequestRpc());
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = connectionEntity
            });

            ecb.AddComponent<SentGoInGame>(connectionEntity);
            UnityEngine.Debug.Log("<color=green>[Client] Sent GoInGameRequestRpc to server</color>");
        }

        // Server approved → mark client connection as in-game
        foreach (var (rpc, entity) in
            SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
            .WithAll<GoInGameApprovedRpc>()
            .WithEntityAccess())
        {
            ecb.AddComponent<NetworkStreamInGame>(rpc.ValueRO.SourceConnection);
            ecb.DestroyEntity(entity);
            UnityEngine.Debug.Log("<color=green>[Client] Approved — NetworkStreamInGame added</color>");
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
// ─── RPC Definitions ───────────────────────────────────────────────
public struct GoInGameRequestRpc : IRpcCommand { }
public struct GoInGameApprovedRpc : IRpcCommand { }

// ─── Shared Components ─────────────────────────────────────────────
public struct SentGoInGame : IComponentData { }

public struct CommandTarget : IComponentData
{
    public Entity Value;
}

[GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
public struct CommandSource : IComponentData { }