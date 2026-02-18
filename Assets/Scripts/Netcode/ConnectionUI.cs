using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using TMPro;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _ipField;
    [SerializeField] private TMP_InputField _portField;

    private void Start()
    {
        _ipField.text = "127.0.0.1";
        _portField.text = "7979";
    }

    public void OnConnectPressed()
    {
        var ip = _ipField.text;
        var port = ushort.Parse(_portField.text);
        Connect(ip, port);
    }

    private void Connect(string ip, ushort port)
    {
        foreach (var world in World.All)
        {
            if (!world.IsClient()) continue;

            var ep = NetworkEndpoint.Parse(ip, port);
            using var drvQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<NetworkStreamDriver>());

            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(
                world.EntityManager, ep);

            Debug.Log($"Connecting to {ip}:{port}");
        }
    }
}