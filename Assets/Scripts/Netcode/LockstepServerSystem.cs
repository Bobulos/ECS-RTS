using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.VisualScripting;


// ---- Server: collect inputs, broadcast when all ready ----

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class LockstepServerSystem : SystemBase
{
    // Map from NetworkId → input for this turn
    private NativeHashMap<int, PackedBittableInput> _collectedInputs;
    private int _expectedPlayers = 1; // or track dynamically
    private ushort _currentTurn = 0;

    protected override void OnCreate()
    {
        _collectedInputs = new NativeHashMap<int, PackedBittableInput>(8, Allocator.Persistent);
        RequireForUpdate<NetworkStreamInGame>();
    }

    protected override void OnDestroy() => _collectedInputs.Dispose();

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Receive inputs from clients
        foreach (var (rpc, request, entity) in
            SystemAPI.Query<RefRO<ClientInputRpc>, RefRO<ReceiveRpcCommandRequest>>()
            .WithEntityAccess())
        {
            //UnityEngine.Debug.Log($"Received input for turn {rpc.ValueRO.TurnNumber} from connection {request.ValueRO.SourceConnection}");
            var networkId = SystemAPI.GetComponent<NetworkId>(request.ValueRO.SourceConnection);
            _collectedInputs[networkId.Value] = rpc.ValueRO.Value;

            ecb.DestroyEntity(entity);
        }

        // Once all players have submitted, broadcast TurnReady
        if (_collectedInputs.Count >= _expectedPlayers)
        {
            // Grab inputs (expand for more players)
            _collectedInputs.TryGetValue(1, out var input0);
            _collectedInputs.TryGetValue(2, out var input1);

            // Broadcast to all connections
            foreach (var (_, connectionEntity) in
                SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
            {
                //UnityEngine.Debug.Log($"Broadcasting turn {_currentTurn}");
                var rpcEntity = ecb.CreateEntity();
                ecb.AddComponent(rpcEntity, new TurnReadyRpc
                {
                    TurnNumber = _currentTurn,
                    Input0 = input0,
                    Input1 = input1,
                });
                ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                {
                    TargetConnection = connectionEntity
                });
            }

            _collectedInputs.Clear();
            _currentTurn++;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}