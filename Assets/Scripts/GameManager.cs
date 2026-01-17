using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject inputPlayback;
    [SerializeField] private GameObject inputLogger;

    [Header("Local Playerdata")]
    public uint localTeam = 1;
    public InputBridge inputBridge;
    public ConstructionBridge constructionBridge;

    private EntityManager entityManager;
    private void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }
    public void OnChangeTeam(uint newTeam)
    {
        localTeam = newTeam;
        if (localData.TryGetSingleton(out LocalPlayerData data))
        {
            data.TeamID = (int)newTeam;
            //write to it
            if (localData.TryGetSingletonEntity<LocalPlayerData>(out var e)) entityManager.SetComponentData(e, data);
            inputBridge.team = newTeam;
            constructionBridge.team = newTeam;

        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private EntityQuery localData;
    void Start()
    {
        localData = entityManager.CreateEntityQuery(typeof(LocalPlayerData));
        OnChangeTeam(localTeam);
        if (GameSettings.InReplayMode)
        {
            var p = Instantiate(inputPlayback).GetComponent<InputPlayback>();
            p.StartReplay(GameSettings.ReplayPath);
        }
        else
        {
            Instantiate(inputLogger);
        }
    }
    public void EndGame()
    {
        GameSettings.InReplayMode = false;
        GameSettings.ReplayPath = "";
        SceneManager.LoadScene("MainMenue");
    }
}
