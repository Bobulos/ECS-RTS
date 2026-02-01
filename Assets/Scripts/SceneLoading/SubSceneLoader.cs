using UnityEngine;
using Unity.Entities;
using Unity.Scenes;
using System.Collections;

public class SubSceneLoader : MonoBehaviour
{
    // Assign the SubScene in the Inspector to get its GUID
    public SubScene subSceneToLoad;
    private Entity loadedSceneEntity;

    private void Start()
    {
        LoadSubScene();
    }

    private void LoadSubScene()
    {
        // Define load parameters if needed (e.g., SceneLoadFlags.NewInstance)
        var loadParameters = new SceneSystem.LoadParameters { Flags = SceneLoadFlags.NewInstance };

        // Load the scene asynchronously using its GUID
        loadedSceneEntity = SceneSystem.LoadSceneAsync(World.DefaultGameObjectInjectionWorld.Unmanaged, subSceneToLoad.SceneGUID, loadParameters);

        // Optional: Monitor the loading status
        StartCoroutine(CheckSceneLoaded());
    }

    private void UnloadSubScene()
    {
        if (loadedSceneEntity != Entity.Null)
        {
            // Unload the scene by destroying its meta entities
            var unloadParameters = SceneSystem.UnloadParameters.DestroyMetaEntities;
            SceneSystem.UnloadScene(World.DefaultGameObjectInjectionWorld.Unmanaged, loadedSceneEntity, unloadParameters);
            loadedSceneEntity = Entity.Null;
        }
    }

    IEnumerator CheckSceneLoaded()
    {
        // Wait until the scene is fully loaded
        while (!SceneSystem.IsSceneLoaded(World.DefaultGameObjectInjectionWorld.Unmanaged, loadedSceneEntity))
        {
            yield return null;
        }
        Debug.Log("SubScene fully loaded!");
    }
}
