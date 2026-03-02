using UnityEngine;
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct LobbyServerSystem : ISystem
{
    private int _playerCount;
    private LobbyData _lobbyData;
    public void OnCreate(ref SystemState state)
    {
        _playerCount = 0;
        _lobbyData = new LobbyData { PlayerCount = 0, PlayerNames = "" };
    }
    public void OnUpdate(ref SystemState state)
    {
        _playerCount = state.EntityManager.CreateEntityQuery(typeof(NetworkId)).CalculateEntityCount();
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        // receive join lobby requests from clients and update lobby data accordingly
        foreach (var (joinReq, entity) in
            SystemAPI.Query<RefRO<ClientJoinLobbyRpc>>()
            .WithEntityAccess())
        {
            _lobbyData.PlayerCount++;
            _lobbyData.PlayerNames += joinReq.ValueRO.PlayerName + ";";

            
            UnityEngine.Debug.Log($"<color=cyan>[Lobby][Server] Player count changed: {_playerCount}</color>");

            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new LobbyDataUpdateRpc { Data = _lobbyData});
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = Entity.Null  // broadcasts to ALL clients
            });


            ecb.DestroyEntity(entity);  // destroy the join request entity after handling
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
public struct LobbyDataUpdateRpc : IRpcCommand
{
    public LobbyData Data;
}

public struct LocalLobbyData : IComponentData
{
    public LobbyData Data;
}
public struct ClientJoinLobbyRpc : IRpcCommand
{
    public FixedString32Bytes PlayerName;
}
public struct LobbyData
{
    public int PlayerCount;
    //32 bytes per player
    public FixedString128Bytes PlayerNames; // up to 4 players
}
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct LobbyClientSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkId>();
        state.EntityManager.CreateSingleton(new LocalLobbyData { Data = new LobbyData { PlayerCount = 0, PlayerNames = "" } });
    }
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        // Send join lobby request to server
        foreach (var (req, entity) in SystemAPI.Query<RefRO<EnterLobbyClientRequest>>()
                .WithEntityAccess())
        {
            var playerName = req.ValueRO.PlayerName;
            UnityEngine.Debug.Log("<color=cyan>[Lobby][Client] Received enter lobby request forwarding to server</color>");

            ecb.DestroyEntity(entity);

            var rpcEntity = ecb.CreateEntity();
            ecb.AddComponent(rpcEntity, new ClientJoinLobbyRpc
            {
                PlayerName = playerName
            });
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = Entity.Null  // Entity.Null sends to server from client
            });
        }

        // Receive lobby data updates from server
        foreach (var (rpc, entity) in SystemAPI
            .Query<RefRO<LobbyDataUpdateRpc>>()
            .WithAll<ReceiveRpcCommandRequest>()  // only process incoming RPCs
            .WithEntityAccess())
        {
            var data = rpc.ValueRO.Data;
            UnityEngine.Debug.Log($"<color=cyan>[Lobby][Client] Received lobby update: {data.PlayerCount} players</color>");

            if (!SystemAPI.HasSingleton<LocalLobbyData>())
                state.EntityManager.CreateSingleton(new LocalLobbyData { Data = data });
            else
            {
                var localData = SystemAPI.GetSingleton<LocalLobbyData>();
                localData.Data = data;
                SystemAPI.SetSingleton(localData);
            }

            ecb.DestroyEntity(entity);  // always destroy after handling
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}