using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Entities.UniversalDelegates;

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
    public int TurnNumber;
    public BittableInput Input;
}

// Server → All Clients: "all inputs for turn N, go simulate"
public struct TurnReadyRpc : IRpcCommand
{
    public int TurnNumber;
    public BittableInput Input0; // Player 0's input
    public BittableInput Input1; // Player 1's input
    // Expand for max player count, or use a fixed array
}
//rpcs
/// <summary>
/// Get rid of these just no more input rpcs,
/// </summary>
public struct MoveUnitsRpc : IRpcCommand
{
    public int Team;
    public MoveUnitsData Move;
}
public struct ClearUnitsRpc : IRpcCommand
{
    public int Team;
}
public struct ActionRpc : IRpcCommand
{
    public int Team;
    public ActionData Action;
}
public struct CodeSelectRpc : IRpcCommand
{
    public int Team;
    public byte CodeSelect;
}
public struct FixedSelectionRpc : IRpcCommand
{
    public int Team;
    public FixedSelectionData Select;
}


public struct PlayerInputRpc : IRpcCommand
{
    public BittableInput Value;
}

//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class LockstepInputSenderSystem : SystemBase
{

    const int LOCKSTEP_TICKS = 6; // e.g. 100ms at 60hz
    private int _currentTurn = 0;
    private BittableInput _pendingInput;
    private NativeList<BittableInput> _buffer;

    //private BittableInput _tempInput;

    protected override void OnCreate()
    {
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

    protected override void OnUpdate()
    {
        var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
        if (!tick.IsValid) return;

        // Send at the boundary of each lockstep interval
        if (tick.TickIndexForValidTick % LOCKSTEP_TICKS != 0) return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

         //Don't allow for inputs when lockste isnt ready

        // Find connection entity and send RPC
        foreach (var (_, connectionEntity) in
            SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
        {
            if (_pendingInput.Type == InputType.None) continue; // Skip if no input to send
            UnityEngine.Debug.Log($"Sending input for turn from client");
            var rpcEntity = EntityManager.CreateEntity();
            ecb.AddComponent(rpcEntity, new ClientInputRpc
            {
                TurnNumber = _currentTurn,
                Input = _pendingInput
            });
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

    // ==== UI EVENTS ====

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
}


/// <summary>
/// Test reading system
/// </summary>
// [UpdateInGroup(typeof(SimulationSystemGroup))]
// partial struct TestInputReaderSystem : ISystem
// {
//     public void OnUpdate(ref SystemState state)
//     {
//         foreach (var input in SystemAPI.Query<RefRW<PlayerInputCommand>>().WithAll<Simulate>())
//         {
//             var c = input.ValueRO.Value;
//             if (c.Type == InputType.None) continue; // Skip if no input
//             //if (!c.Active) continue; // Only read if active, otherwise skip (and keep inactive for next frame)
//             UnityEngine.Debug.Log($"Read input command of type {c.Type} for team {c.Team}");
//             //input.ValueRW.Active = false;
//             //input.ValueRW = new PlayerInputCommand(); // Clear after reading, not really necessary but just to be safe
//         }
//     }
// }

// [GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
// public struct CommandSource : IComponentData {}

// public struct CommandTarget : IComponentData {}


public struct PlayerInputCommand : IInputComponentData
{
    public BittableInput Value;
}