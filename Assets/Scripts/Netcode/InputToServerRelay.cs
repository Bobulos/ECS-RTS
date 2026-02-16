using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.NetCode;

public struct BittableInput
{
    public int Team;
    public InputType Type;

    public MoveUnitsData Move;
    public ActionData Action;
    public byte CodeSelect;
    public FixedSelectionData Select;
}
//rpcs
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


[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class InputToServerRelay : SystemBase
{
    private NativeList<BittableInput> _buffer;
    protected override void OnCreate()
    {
        _buffer = new NativeList<BittableInput>(Allocator.Persistent);
/*        ConstructionBridge.ConstructWalls += OnConstructWalls;
        ConstructionBridge.ConstructStructure += OnConstructStructure;*/
        InputBridge.OnMoveUnits += OnMoveUnits;
        InputBridge.OnClearUnits += OnClearUnits;
        InputBridge.OnSelectUnits += OnSelectUnits;
        InputBridge.OnCodeSelectUnits += OnCodeSelectUnits;
        UnitActionManager.OnAction += OnAction;
    }
    protected override void OnDestroy()
    {
        _buffer.Dispose();
/*        ConstructionBridge.ConstructWalls -= OnConstructWalls;
        ConstructionBridge.ConstructStructure -= OnConstructStructure;*/
        InputBridge.OnMoveUnits -= OnMoveUnits;
        InputBridge.OnClearUnits -= OnClearUnits;
        InputBridge.OnSelectUnits -= OnSelectUnits;
        InputBridge.OnCodeSelectUnits -= OnCodeSelectUnits;
        UnitActionManager.OnAction -= OnAction;
    }
    protected override void OnUpdate()
    {
        // Get the network connection entity

        if (!SystemAPI.TryGetSingletonEntity<NetworkId>(out var connectionEntity)) return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var r in _buffer)
        {
            var rpcEntity = EntityManager.CreateEntity();
            switch (r.Type)
            {
                case InputType.MoveUnits:
                    ecb.AddComponent(rpcEntity, new MoveUnitsRpc
                    {
                        Team = r.Team,
                        Move = r.Move
                    });
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                    {
                        TargetConnection = connectionEntity
                    });
                    //UnityEngine.Debug.Log("Sent move units rpc");
                    break;
                case InputType.ClearUnits:
                    ecb.AddComponent(rpcEntity, new ClearUnitsRpc
                    {
                        Team = r.Team,
                    });
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                    {
                        TargetConnection = connectionEntity
                    });
                    //UnityEngine.Debug.Log("Sent clear units rpc");
                    break;
                case InputType.Action:
                    ecb.AddComponent(rpcEntity, new ActionRpc
                    {
                        Team = r.Team,
                        Action = r.Action
                    });
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                    {
                        TargetConnection = connectionEntity
                    });
                    //UnityEngine.Debug.Log("Sent action rpc");
                    break;
                case InputType.CodeSelectUnits:
                    ecb.AddComponent(rpcEntity, new CodeSelectRpc
                    {
                        Team = r.Team,
                        CodeSelect = r.CodeSelect
                    });
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                    {
                        TargetConnection = connectionEntity
                    });
                    //UnityEngine.Debug.Log("Sent code select units rpc");
                    break;
                case InputType.SelectUnits:
                    ecb.AddComponent(rpcEntity, new FixedSelectionRpc
                    {
                        Team = r.Team,
                        Select = r.Select
                    });
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
                    {
                        TargetConnection = connectionEntity
                    });
                    //UnityEngine.Debug.Log("Sent selct units rpc");
                    break;
            }
        }
        _buffer.Clear();
        ecb.Playback(EntityManager);
        ecb.Dispose();
        // Create and send the RPC
    }
    public void OnAction(ActionData d, int team)
    {
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.Action,
            Action = d,
        });
    }
    public void OnCodeSelectUnits(byte code, int team)
    {
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.CodeSelectUnits,
            CodeSelect = code
        });
    }
    /*public void OnConstructWalls(ConstructWallData d, int team)
    {
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.ConstructWalls,
            Wall = d,
        });
    }*/
    /*public void OnConstructStructure(ConstructData d, int team)
    {
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.Construct,
            Structure = d,
        });
    }*/
    public void OnMoveUnits(MoveUnitsData d, int team)
    {
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.MoveUnits,
            Move = d,
        });
    }
    public void OnClearUnits(int team)
    {
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.ClearUnits,
        });
    }
    //0 is reg 1 is all
    public void OnSelectUnits(FixedSelectionData vertecies, int team)
    {
        if (vertecies.Value.Length == 0 || vertecies.Value.Length < 8) { return; }
        _buffer.Add(new BittableInput
        {
            Team = team,
            Type = InputType.SelectUnits,
            Select = vertecies,
        });
    }
}
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct InputReceiverDebugger : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (rpc, rpcSource, entity) in
                 SystemAPI.Query<RefRO<FixedSelectionRpc>, RefRO<ReceiveRpcCommandRequest>>()
                 .WithEntityAccess())
        {
            // Process the RPC
            UnityEngine.Debug.Log($"Server received RPC from connection {rpcSource.ValueRO.SourceConnection}");
            UnityEngine.Debug.Log($"Player team ID: {rpc.ValueRO.Team}");
            //ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}