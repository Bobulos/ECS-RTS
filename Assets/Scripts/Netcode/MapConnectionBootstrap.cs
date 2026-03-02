using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using System;
using Unity.Scenes;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;
using Unity.Entities.UniversalDelegates;

public class MapConnectionBootstrap : MonoBehaviour
{
    [Serialize] private float _loadTime = 5f;
    public static Action OnMapLoaded;
    private World _clientWorld;
    private EntityQuery _inGameQuery;
    private void Update()
    {
        if (_clientWorld == null && ClientServerBootstrap.ClientWorld != null)
        {
            _clientWorld = ClientServerBootstrap.ClientWorld;
            _inGameQuery = _clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamInGame));
            
        } else if (_clientWorld != null && _inGameQuery.CalculateEntityCount() > 0)
        {
            Invoke(nameof(OnMapLoadedInternal), _loadTime);
            UnityEngine.Debug.Log($"<color=red>[MapConnectionBootstrap] Detected client world, starting map load timer</color>");
            //UnityEngine.Debug.Log($"<color=red>[MapConnectionBootstrap] Map load timer started, will invoke OnMapLoaded in {_loadTime} seconds</color>");
        }
        
    }
    void OnMapLoadedInternal()
    {
        if (World.DefaultGameObjectInjectionWorld != null) World.DefaultGameObjectInjectionWorld.Dispose();
        OnMapLoaded?.Invoke();
        Destroy(this.gameObject);
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