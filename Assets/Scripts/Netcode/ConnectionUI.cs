using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Entities;
using UnityEngine;
using TMPro;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _ipField;
    [SerializeField] private TMP_InputField _portField;

    [SerializeField] private string _loadingSceneName = "LoadingScene";
    [SerializeField] private string _mapSceneName = "BattlesMultiplayer";

    PlayerSpawner _spawner;

    private void Start()
    {
        _ipField.text = "127.0.0.1";
        _portField.text = "7979";

        // var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        // var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerSpawner>());
        // _spawner = query.GetSingleton<PlayerSpawner>();
    }
    public void OnHostPressed()
    {
        GameLoadConfig.ServerIp = "127.0.0.1";
        GameLoadConfig.IsHost = true;
        GameLoadConfig.IsClient = false;
        LoadMap();
    }
    

    public void OnJoinPressed()
    {
        GameLoadConfig.ServerIp = _ipField.text;
        GameLoadConfig.IsHost = false;
        GameLoadConfig.IsClient = true;
        LoadMap();
    }




    private void LoadMap()
    {
        GameLoadConfig.InReplayMode = false;
        GameLoadConfig.MapSceneName = _mapSceneName;
        //Invoke(nameof(EnterLoadingScene), 10f); // Delay to ensure worlds are set up before loading scene
        EnterLoadingScene();
    }
    private void EnterLoadingScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(_loadingSceneName);
    }

}