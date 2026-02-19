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

    private void Update()
    {
        if (_initialized) return;

        // Wait 2 frames after scene load for subscenes to bake
        _framesWaited++;
        if (_framesWaited < 10) return;

        _initialized = true;

        if (GameLoadConfig.IsHost)
            Host();
        else if (GameLoadConfig.IsClient)
            Join(GameLoadConfig.ServerIp, 7979);
        //SceneManager.LoadScene(_sharedScene.SceneGUID, LoadSceneMode.Additive);
    }

    private void Host()
    {
        var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        DestroyDefaultWorld();

        LoadSubSceneIntoWorld(serverWorld);
        LoadSubSceneIntoWorld(clientWorld);

        RequestServerListen(serverWorld);
        ConnectClient(clientWorld, "127.0.0.1", 7979);

        Debug.Log("[MapBootstrap] Host setup complete");
        OnMapLoaded?.Invoke();
    }

    private void Join(string ip, ushort port)
    {
        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        DestroyDefaultWorld();

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
        query.GetSingletonRW<NetworkStreamDriver>().ValueRW
            .Listen(ClientServerBootstrap.DefaultListenAddress.WithPort(7979));
        Debug.Log("<color=blue>[Server] Listening on port 7979</color>");
    }

    private void ConnectClient(World clientWorld, string serverIp, ushort serverPort)
    {
        using var query = clientWorld.EntityManager.CreateEntityQuery(
            ComponentType.ReadWrite<NetworkStreamDriver>());
        query.GetSingletonRW<NetworkStreamDriver>().ValueRW
            .Connect(clientWorld.EntityManager, NetworkEndpoint.Parse(serverIp, serverPort));
        Debug.Log("<color=green>[Client] Connecting to server on port 7979</color>");
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