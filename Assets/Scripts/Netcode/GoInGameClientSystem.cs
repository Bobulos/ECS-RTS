using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct GoInGameClientSystem : ISystem
{
    private float _initStartTime;
    private const float MAX_WAIT_TIME = 1f;
    public void OnCreate(ref SystemState state)
    {
        _initStartTime = (float)SystemAPI.Time.ElapsedTime;
        state.EntityManager.CreateSingleton<LocalPlayerData>(new LocalPlayerData { TeamID = -10000 });
    }

    public void OnUpdate(ref SystemState state)
    {
        // if (!SystemAPI.TryGetSingletonEntity<StartGameFlag>(out var flagEntity))
        // {
        //     return;
        // }


        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Send GoInGameRequestRpc to server if loaded into map
        if (!SystemAPI.HasSingleton<MapData>()) return;

        foreach (var (_, connectionEntity) in
            SystemAPI.Query<RefRO<NetworkId>>()
            .WithNone<NetworkStreamInGame, SentGoInGame>()
            .WithEntityAccess())
        {
            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new GoInGameRequestRpc());
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = connectionEntity
            });

            ecb.AddComponent<SentGoInGame>(connectionEntity);
            UnityEngine.Debug.Log("<color=green>[Client] Sent GoInGameRequestRpc to server</color>");
        }

        foreach (var (rpc, receive, entity) in
            SystemAPI.Query<RefRO<GoInGameApprovedRpc>, RefRO<ReceiveRpcCommandRequest>>()
            .WithEntityAccess())
        {
            SystemAPI.GetSingletonRW<LocalPlayerData>().ValueRW.TeamID = rpc.ValueRO.TeamID;
            ecb.AddComponent<NetworkStreamInGame>(receive.ValueRO.SourceConnection);
            ecb.DestroyEntity(entity);
            UnityEngine.Debug.Log("<color=green>[Client] Approved — NetworkStreamInGame added</color>");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
// ─── RPC Definitions ───────────────────────────────────────────────
public struct GoInGameRequestRpc : IRpcCommand { }
public struct GoInGameApprovedRpc : IRpcCommand
{
    public int TeamID;
}

// ─── Shared Components ─────────────────────────────────────────────
public struct SentGoInGame : IComponentData { }

public struct CommandTarget : IComponentData
{
    public Entity Value;
}

[GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
public struct CommandSource : IComponentData { }