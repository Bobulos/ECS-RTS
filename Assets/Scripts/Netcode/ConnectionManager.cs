using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;
using Unity.Entities.UniversalDelegates;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private string _mapSceneName = "BattlesMultiplayered";
    [SerializeField] private TMP_InputField _ipField;
    //[SerializeField] private TMP_InputField _portField;

    [SerializeField] private string _loadingSceneName = "LoadingScene";


    //private EntityQuery _startGameQuery;
    private void Start()
    {
        _ipField.text = "127.0.0.1";
        //_portField.text = "7979";
    }
    private World _clientWorld;
    void Update()
    {
        if (_clientWorld == null && ClientServerBootstrap.ClientWorld != null)
        {
            _clientWorld = ClientServerBootstrap.ClientWorld;
        }
        if (_clientWorld != null)
        {
            var query = _clientWorld.EntityManager.CreateEntityQuery(typeof(StartGameFlag));
            if (query.HasSingleton<StartGameFlag>())
            {
                UnityEngine.Debug.Log($"<color=green>[Client] StartGameFlag detected, entering game</color>");
                EnterGame();
            }
        }
    }

    public void OnHostPressed()
    {
        StartHost("127.0.0.1", ushort.Parse("7979"));
    }

    public void OnJoinPressed()
    {
        StartClient(_ipField.text, ushort.Parse("7979"));
    }
    public void HostStartGame()
    {
        var em = ClientServerBootstrap.ServerWorld.EntityManager;
        UnityEngine.Debug.Log($"Send start game RPC request to all clients");
        var entity = em.CreateEntity();
        em.AddComponentData(entity, new StartGameHostRequest());
    }
    // ---------------- HOST ----------------
    void StartHost(string ip, ushort port)
    {
        //DestroyExistingWorlds();

        var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        Listen(serverWorld, port);
        Connect(clientWorld, ip, port);
    }
    
    // ---------------- CLIENT ----------------
    void StartClient(string ip, ushort port)
    {
        //DestroyExistingWorlds();
        _joinIp = ip;

        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        Connect(clientWorld, ip, port);
    }
    string _joinIp = "127.0.0.1";
    public void EnterGame()
    {
        GameLoadConfig.ServerIp = _joinIp;
        GameLoadConfig.MapSceneName = _mapSceneName;
        SceneManager.LoadScene(_loadingSceneName);
    }
    // ---------------- NETWORK ----------------
    void Listen(World world, ushort port)
    {
        var query = world.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
        var driver = query.GetSingletonRW<NetworkStreamDriver>();

        driver.ValueRW.Listen(NetworkEndpoint.AnyIpv4.WithPort(port));
        Debug.Log($"<color=blue>[Server] Listening on {port}</color>");
    }

    void Connect(World world, string ip, ushort port)
    {
        var query = world.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
        var driver = query.GetSingletonRW<NetworkStreamDriver>();

        var endpoint = NetworkEndpoint.Parse(ip, port);
        driver.ValueRW.Connect(world.EntityManager, endpoint);

        Debug.Log($"<color=green>[Client] Connecting to {ip}:{port}</color>");
    }

    // ---------------- WORLD CLEANUP ----------------
    static void DestroyExistingWorlds()
    {
        foreach (var world in World.All)
        {
            world.Dispose();
        }

        World.DefaultGameObjectInjectionWorld = null;
    }
}

public struct StartGameHostRequest : IComponentData
{
}
public struct StartGameFlag : IComponentData
{
}
public struct StartGameRpc : IRpcCommand
{
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct SendStartGameHostRequestSystem : ISystem
{
    int _curTeamID;
    public void OnCreate(ref SystemState state)
    {
        _curTeamID = 0;
        //need at least one player to join before we run the system
        //state.RequireForUpdate<NetworkId>();
        //state.RequireForUpdate<PlayerSpawner>();
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        if (!SystemAPI.TryGetSingletonEntity<StartGameHostRequest>(out var req))
        {
            //No start game request yet, do nothing
            return;
        }

        ecb.DestroyEntity(req);
        //Send to all clients that the host has started the game
        foreach (var (connection, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess())
        {
            Entity rpcEnt = ecb.CreateEntity();
            ecb.AddComponent(rpcEnt, new StartGameRpc());
            ecb.AddComponent(rpcEnt, new SendRpcCommandRequest { TargetConnection = entity });
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct ReceiveStartGameRpcSystem : ISystem
{
    int _curTeamID;
    public void OnCreate(ref SystemState state)
    {
        _curTeamID = 0;
        //need at least one player to join before we run the system
        //state.RequireForUpdate<NetworkId>();
        //state.RequireForUpdate<PlayerSpawner>();
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (rpc, receive, rpcEntity) in SystemAPI.Query<RefRO<StartGameRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
        {
            //Create start game flag that the client systems can react to in order to transition to the game scene
            var e = ecb.CreateEntity();
            ecb.AddComponent(e, new StartGameFlag());

            ecb.DestroyEntity(rpcEntity);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}