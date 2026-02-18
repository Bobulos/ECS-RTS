using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

public class ServerStarter : MonoBehaviour
{
    public ushort Port = 7979;

    void Start()
    {
        foreach (var world in World.All)
        {
            if (!world.IsServer()) continue;

            var em = world.EntityManager;

            var driverQuery = em.CreateEntityQuery(typeof(NetworkStreamDriver));
            var driver = driverQuery.GetSingletonRW<NetworkStreamDriver>();

            var endpoint = NetworkEndpoint.AnyIpv4;
            endpoint.Port = Port;

            driver.ValueRW.Listen(endpoint);

            Debug.Log($"SERVER LISTENING ON {Port}");
        }
    }
}
