using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
public class ECSWorldManager : MonoBehaviour
{
    public static Action OnRestart;

    [SerializeField] private string scene;

    void Awake()
    {

    }
    void OnDestroy()
    {
        
    }

    public void Clean()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (world != null && world.IsCreated)
        {
            world.Dispose();
            World.DefaultGameObjectInjectionWorld = null;
            OnRestart.Invoke();
        }
    }

    public void CleanAndRestartECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (world != null && world.IsCreated)
        {
            world.Dispose();
            World.DefaultGameObjectInjectionWorld = null;
            OnRestart.Invoke();
        }

        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }
}