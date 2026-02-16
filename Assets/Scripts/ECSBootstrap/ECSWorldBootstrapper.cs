using Unity.Entities;
using UnityEngine;
using Unity.NetCode;
public class ECSWorldBootstrapper : MonoBehaviour
{
    void Awake()
    {
        ECSWorldManager.OnRestart += OnRestart;
    }
    void OnDestroy()
    {
        ECSWorldManager.OnRestart -= OnRestart;
    }

    void OnRestart()
    {
        UnityEngine.Debug.Log("Bootstrap activated");

        var world = World.DefaultGameObjectInjectionWorld;

        
        UnityEngine.Debug.Log($"World null {world == null}");
        //If world is gone or disposed, let Unity's default bootstrapping recreate it
        if (world != null)
        {
            UnityEngine.Debug.Log($"World created {world.IsCreated}");
        }
        UnityEngine.Debug.Log("Entity world created");
        DefaultWorldInitialization.Initialize("Default World", false);
    }
}