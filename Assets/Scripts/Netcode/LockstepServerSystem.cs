using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.VisualScripting;

// 
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class LockstepServerSystem : SystemBase
{
    // Map from NetworkId → input for this turn
    private NativeHashMap<int, PackedBittableInput> _collectedInputs;
    private int _expectedPlayers = 2; // or track dynamically
    private ushort _currentTurn = 0;
    bool _logging;
    protected override void OnCreate()
    {
        _expectedPlayers = GameLoadConfig.ExpectedPlayers;
        //_logging = NetworkConfigLoader.LoadNetwork().logNetwork;
        _collectedInputs = new NativeHashMap<int, PackedBittableInput>(8, Allocator.Persistent);
        RequireForUpdate<NetworkStreamInGame>();
    }

    protected override void OnDestroy() => _collectedInputs.Dispose();

    protected override void OnUpdate()
    {
        //UnityEngine.Debug.Log("Server system running");

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Receive inputs from clients
        foreach (var (rpc, request, entity) in
            SystemAPI.Query<RefRO<ClientInputRpc>, RefRO<ReceiveRpcCommandRequest>>()
            .WithEntityAccess())
        {
            var packed = rpc.ValueRO.Value;
            var networkId = SystemAPI.GetComponent<NetworkId>(request.ValueRO.SourceConnection).Value;

            // Ignore old inputs
            if (packed.Turn < _currentTurn)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            // Ignore future inputs (client ahead)
            if (packed.Turn > _currentTurn)
            {
                // optional: store in future buffer later
                ecb.DestroyEntity(entity);
                continue;
            }

            _collectedInputs[networkId] = packed;
            ecb.DestroyEntity(entity);
        }

        //UnityEngine.Debug.Log($"Collected {_collectedInputs.Count} inputs for turn {_currentTurn}, waiting for {_expectedPlayers}");

        _collectedInputs.TryGetValue(1, out var inp);
        //UnityEngine.Debug.Log($"Dif in turns {inp.Turn - _currentTurn}");
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
                //UnityEngine.Debug.Log($"Packed input0 for turn {_currentTurn}: ");
                ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                {
                    TargetConnection = Entity.Null
                });
                if (_logging)
                {
                    UnityEngine.Debug.Log($"<color=blue>[Server] Sending input for turn {_currentTurn} from client with type {PackerUtil.Unpack(input0).Type}</color>");
                UnityEngine.Debug.Log($"<color=blue>[Server] Sending input for turn {_currentTurn} from client with type {PackerUtil.Unpack(input1).Type}</color>");
                }
                
                       
                //var dType = PackerUtil.Unpack(input0).Type;
                //if (dType != InputType.None) UnityEngine.Debug.Log($"Broadcasted turn to clients {_currentTurn} with inputs of type {dType}");
            }
            _currentTurn++;
            _collectedInputs.Clear();

        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}