using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Entities.UniversalDelegates;
using System;
using System;
using System.Runtime.InteropServices;
using Unity.Physics;

public struct BittableInput
{
    public int Team;
    public InputType Type;

    public MoveUnitsData Move;
    public ActionData Action;
    public byte CodeSelect;
    public FixedSelectionData Select;
}

public struct LockstepInput : IComponentData
{
    public BittableInput Input;
    public int NetworkId;
    public bool Ready;
}

// Client → Server: "here is my input for turn N"
public struct ClientInputRpc : IRpcCommand
{
    //public ushort TurnNumber;
    public PackedBittableInput Value;
}

// Server → All Clients: "all inputs for turn N, go simulate"
public struct TurnReadyRpc : IRpcCommand
{
    public ushort TurnNumber;
    public PackedBittableInput Input0; // Player 0's input
    public PackedBittableInput Input1; // Player 1's input
    // Expand for max player count, or use a fixed array
}

//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class LockstepInputSenderSystem : SystemBase
{

    private ushort _lockstepTicks = 6; // 30hz
    private ushort _currentTurn = 0;
    private BittableInput _pendingInput;
    private NativeList<BittableInput> _buffer;

    private bool _logging = false;

    //private BittableInput _tempInput;

    protected override void OnCreate()
    {
        //_logging = NetworkConfigLoader.LoadNetwork().logNetwork;
        //_lockstepTicks = NetworkConfigLoader.LoadNetwork().networkTicks;
        int size = Marshal.SizeOf<PackedBittableInput>();
        UnityEngine.Debug.Log($"Marshaled size of PackedBittableInput: {size} bytes");
        

        RequireForUpdate<NetworkStreamInGame>();
        // if (!SystemAPI.TryGetSingleton<NetworkStreamInGame>(out var inGame))
        //     return;
        _buffer = new NativeList<BittableInput>(Allocator.Persistent);

        InputBridge.OnMoveUnits += OnMoveUnits;
        InputBridge.OnClearUnits += OnClearUnits;
        InputBridge.OnSelectUnits += OnSelectUnits;
        InputBridge.OnCodeSelectUnits += OnCodeSelectUnits;
        UnitActionManager.OnAction += OnAction;


    }

    protected override void OnDestroy()
    {
        _buffer.Dispose();

        InputBridge.OnMoveUnits -= OnMoveUnits;
        InputBridge.OnClearUnits -= OnClearUnits;
        InputBridge.OnSelectUnits -= OnSelectUnits;
        InputBridge.OnCodeSelectUnits -= OnCodeSelectUnits;
        UnitActionManager.OnAction -= OnAction;
    }
    private ushort _confirmedTurn = 0;
    protected override void OnUpdate()
    {
        //UnityEngine.Debug.Log("<color=green>[Client] Input sender system running</color>");
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (rpc, entity) in
            SystemAPI.Query<RefRO<TurnReadyRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            if (rpc.ValueRO.TurnNumber >= _confirmedTurn)
                _confirmedTurn = (ushort)(rpc.ValueRO.TurnNumber + 1);
            // don't destroy here — let SimulationGate handle it
        }

        var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
        var phys = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        if (!tick.IsValid) return;
        if (tick.TickIndexForValidTick % _lockstepTicks != 0) return;

        // Only send if we haven't gotten ahead of confirmed turns
        if (_currentTurn > _confirmedTurn) return;


        //
        // Sending Inputs
        //

        // Find connection entity and send RPC
        foreach (var (_, connectionEntity) in
            SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
        {
            //if (_pendingInput.Type != InputType.None) UnityEngine.Debug.Log($"Sending input for turn {_currentTurn} from client with type {_pendingInput.Type}"); // Skip if no input to send
            //UnityEngine.Debug.Log($"Sending input for turn from client");
            if (_logging) UnityEngine.Debug.Log($"<color=green>[Client] Sending input for turn {_currentTurn} from client with type {_pendingInput.Type}</color>");
            
            //if (_pendingInput.Type == InputType.Action) UnityEngine.Debug.Log("Sending action rpc");
            var rpcEntity = EntityManager.CreateEntity();
            ecb.AddComponent(rpcEntity, new ClientInputRpc 
            { Value = PackerUtil.Pack(phys, _currentTurn, _pendingInput)});
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = connectionEntity
            });

            _pendingInput = new BittableInput(); // clear after sending
            _currentTurn++;
            break;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    #region  Collection
    void OnMoveUnits(MoveUnitsData d, int team)
        => _pendingInput = new BittableInput { Team = team, Type = InputType.MoveUnits, Move = d };

    void OnClearUnits(int team)
        => _pendingInput = new BittableInput { Team = team, Type = InputType.ClearUnits };

    void OnAction(ActionData d, int team)
        => _pendingInput = new BittableInput { Team = team, Type = InputType.Action, Action = d };

    void OnCodeSelectUnits(byte code, int team)
        => _pendingInput = new BittableInput { Team = team, Type = InputType.CodeSelectUnits, CodeSelect = code };

    void OnSelectUnits(FixedSelectionData verts, int team)
    {
        //UnityEngine.Debug.Log($"Select units with {verts.Value.Length} verts for team {team} added to buffer");
        if (verts.Value.Length < 8) return;
        _pendingInput = new BittableInput { Team = team, Type = InputType.SelectUnits, Select = verts };
    }
    #endregion
}