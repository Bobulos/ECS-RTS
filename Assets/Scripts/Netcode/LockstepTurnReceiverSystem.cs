using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

// ---- Client: receive TurnReady, queue for simulation ----

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class LockstepTurnReceiverSystem : SystemBase
{
    // Queue of turns ready to simulate
    public NativeQueue<TurnReadyRpc> PendingTurns;

    protected override void OnCreate()
    {
        PendingTurns = new NativeQueue<TurnReadyRpc>(Allocator.Persistent);
        RequireForUpdate<NetworkStreamInGame>();
    }

    protected override void OnDestroy() => PendingTurns.Dispose();

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (rpc, entity) in
            SystemAPI.Query<RefRO<TurnReadyRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            UnityEngine.Debug.Log($"Received turn ready for turn {rpc.ValueRO.Input0.Type} from server");
            //UnityEngine.Debug.Log($"Received turn ready for turn {rpc.ValueRO.TurnNumber} from server");
            PendingTurns.Enqueue(rpc.ValueRO);
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}