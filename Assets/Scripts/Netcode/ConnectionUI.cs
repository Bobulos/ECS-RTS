using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private string _mapSceneName = "BattlesMultiplayered";
    [SerializeField] private TMP_InputField _ipField;
    [SerializeField] private TMP_InputField _portField;

    [SerializeField] private string _loadingSceneName = "LoadingScene";

    private void Start()
    {
        _ipField.text = "127.0.0.1";
        _portField.text = "7979";
    }

    public void OnHostPressed()
    {
        StartHost("127.0.0.1", ushort.Parse(_portField.text));
    }

    public void OnJoinPressed()
    {
        StartClient(_ipField.text, ushort.Parse(_portField.text));
    }

    // ---------------- HOST ----------------
    void StartHost(string ip, ushort port)
    {
        //DestroyExistingWorlds();

        var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        Listen(serverWorld, port);
        Connect(clientWorld, ip, port);

        GameLoadConfig.ServerIp = ip;
        GameLoadConfig.IsClient = false;
        GameLoadConfig.IsHost = true;
        GameLoadConfig.MapSceneName = _mapSceneName;
        SceneManager.LoadScene(_loadingSceneName);
    }

    // ---------------- CLIENT ----------------
    void StartClient(string ip, ushort port)
    {
        //DestroyExistingWorlds();

        var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");
        Connect(clientWorld, ip, port);

        GameLoadConfig.ServerIp = ip;
        GameLoadConfig.IsClient = true;
        GameLoadConfig.IsHost = false;
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

