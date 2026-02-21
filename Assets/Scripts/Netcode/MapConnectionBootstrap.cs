using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using System;
using Unity.Scenes;
using UnityEngine.SceneManagement;

public class MapConnectionBootstrap : MonoBehaviour
{
    public SubScene _sharedScene;
    public static Action OnMapLoaded;

    private bool _initialized;
    private int _framesWaited;
    private Entity _serverSceneEntity;
    private Entity _clientSceneEntity;
    private void Update()
    {
        if (_initialized) return;

        // 1. First, we must ensure the Default World is gone so systems don't clash
        if (World.DefaultGameObjectInjectionWorld != null && 
            World.DefaultGameObjectInjectionWorld.Name == "Default World")
        {
            DestroyDefaultWorld();
            SetupWorlds();
            return;
        }

        // 2. Wait until SubScenes are fully loaded in their respective worlds
        // This is crucial to prevent "Ghost Prefab Not Found" errors
        if (IsMapReady())
        {
            StartConnection();
            _initialized = true;
        }
    }

    private void SetupWorlds()
    {
        if (GameLoadConfig.IsHost)
        {
            var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            _serverSceneEntity = SceneSystem.LoadSceneAsync(serverWorld.Unmanaged, _sharedScene.SceneGUID);
            _clientSceneEntity = SceneSystem.LoadSceneAsync(clientWorld.Unmanaged, _sharedScene.SceneGUID);
        }
        else if (GameLoadConfig.IsClient)
        {
            var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            _clientSceneEntity = SceneSystem.LoadSceneAsync(clientWorld.Unmanaged, _sharedScene.SceneGUID);
        }
    }

    private bool IsMapReady()
    {
        bool serverReady = true;
        bool clientReady = true;

        if (GameLoadConfig.IsHost)
        {
            var sw = GetWorld("ServerWorld");
            serverReady = sw != null && SceneSystem.IsSceneLoaded(sw.Unmanaged, _serverSceneEntity);
        }

        var cw = GetWorld("ClientWorld");
        clientReady = cw != null && SceneSystem.IsSceneLoaded(cw.Unmanaged, _clientSceneEntity);

        return serverReady && clientReady;
    }
    private World GetWorld(string name)
    {
        foreach (var world in World.All)
        {
            if (world.Name == name) return world;
        }
        return null;
    }
    private void StartConnection()
    {
        if (GameLoadConfig.IsHost)
        {
            Host();
        }
        else if (GameLoadConfig.IsClient)
        {
            Join(GameLoadConfig.ServerIp, 7979);
        }
        
        OnMapLoaded?.Invoke();
    }

    private void Host()
    {
        
        DestroyDefaultWorld();
        var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        

        LoadSubSceneIntoWorld(serverWorld);
        LoadSubSceneIntoWorld(clientWorld);

        RequestServerListen(serverWorld);
        ConnectClient(clientWorld, "127.0.0.1", 7979);

        Debug.Log("[MapBootstrap] Host setup complete");
        OnMapLoaded?.Invoke();
    }

    private void Join(string ip, ushort port)
    {

        DestroyDefaultWorld();
        UnityEngine.Debug.Log($"[MapBootstrap] Joining server at {ip}:{port}");
        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        

        LoadSubSceneIntoWorld(clientWorld);

        ConnectClient(clientWorld, ip, port);

        Debug.Log("[MapBootstrap] Join setup complete");
        OnMapLoaded?.Invoke();
    }
    private void LoadSubSceneIntoWorld(World world)
    {
        var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, _sharedScene.SceneGUID);
        Debug.Log($"[MapBootstrap] Loading subscene into {world.Name}");
    }
    private void RequestServerListen(World serverWorld)
    {
        using var query = serverWorld.EntityManager.CreateEntityQuery(
            ComponentType.ReadWrite<NetworkStreamDriver>());

        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7979);

        query.GetSingletonRW<NetworkStreamDriver>().ValueRW
            .Listen(endpoint);
        Debug.Log("<color=blue>[Server] Listening on port 7979</color>");
    }
    private void ConnectClient(World clientWorld, string serverIp, ushort serverPort)
    {
        using var existingQuery = clientWorld.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<NetworkStreamConnection>());
        
        if (existingQuery.CalculateEntityCount() > 0)
        {
            Debug.LogWarning("[Client] Connection already exists, skipping Connect()");
            return;
        }
        Debug.Log("<color=green>[Client] Connecting to server on port 7979</color>");
        using var query = clientWorld.EntityManager.CreateEntityQuery(
            ComponentType.ReadWrite<NetworkStreamDriver>());
        query.GetSingletonRW<NetworkStreamDriver>().ValueRW
            .Connect(clientWorld.EntityManager, NetworkEndpoint.Parse(serverIp, serverPort));
    }

    private void DestroyDefaultWorld()
    {
        foreach (var world in World.All)
        {
            if (world.Flags == WorldFlags.Game)
            {
                world.Dispose();
                break;
            }
        }
    }
}
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DebugConnectionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (conn, state) in SystemAPI.Query<RefRO<NetworkStreamConnection>, RefRO<NetworkSnapshotAck>>())
        {
            UnityEngine.Debug.Log($"[Client] Connection state: {conn.ValueRO.CurrentState}");
        }

        // Log if no connection entity exists at all
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
        if (q.CalculateEntityCount() == 0)
            UnityEngine.Debug.LogWarning("<color=green>[Client] No NetworkStreamConnection entity found</color>");
    }
}