using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using System;
using Unity.Scenes;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class MapConnectionBootstrap : MonoBehaviour
{
    [Serialize] private float _loadTime = 1f;
    public static Action OnMapLoaded;
    private void Start()
    {
        Invoke(nameof(OnMapLoadedInternal), _loadTime);
    }
    void OnMapLoadedInternal()
    {
        World.DefaultGameObjectInjectionWorld.Dispose();
        OnMapLoaded?.Invoke();
    }
}
// [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
// public partial class DebugConnectionSystem : SystemBase
// {
//     protected override void OnUpdate()
//     {
//         foreach (var (conn, state) in SystemAPI.Query<RefRO<NetworkStreamConnection>, RefRO<NetworkSnapshotAck>>())
//         {
//             UnityEngine.Debug.Log($"[Client] Connection state: {conn.ValueRO.CurrentState}");
//         }

//         // Log if no connection entity exists at all
//         var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
//         if (q.CalculateEntityCount() == 0)
//             UnityEngine.Debug.LogWarning("<color=green>[Client] No NetworkStreamConnection entity found</color>");
//     }
// }