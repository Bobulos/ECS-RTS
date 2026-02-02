using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject inputPlayback;
    [SerializeField] private GameObject inputLogger;

    [Header("Local Playerdata")]
    public int localTeam = 1;
    public InputBridge inputBridge;
    public ConstructionBridge constructionBridge;
    public UnitActionManager unitActionManager;

    private EntityManager entityManager;

    private bool initialized = false;
    private void Start()
    {
        inputBridge = GameObject.FindFirstObjectByType<InputBridge>();
        constructionBridge = GameObject.FindFirstObjectByType<ConstructionBridge>();
        unitActionManager = GameObject.FindFirstObjectByType<UnitActionManager>();

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        localData = entityManager.CreateEntityQuery(typeof(LocalPlayerData));
        
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
    private void Update()
    {
        if (!initialized)
        {
            OnChangeTeam(localTeam);
        }
    }
    public void OnChangeTeam(int newTeam)
    {
        localTeam = newTeam;
        if (localData.TryGetSingleton(out LocalPlayerData data))
        {
            initialized = true;
            data.TeamID = newTeam;
            //write to it
            if (localData.TryGetSingletonEntity<LocalPlayerData>(out var e)) entityManager.SetComponentData(e, data);
            inputBridge.team = newTeam;
            constructionBridge.team = newTeam;
            unitActionManager.team = newTeam;

        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private EntityQuery localData;

    public void EndGame()
    {
        GameSettings.InReplayMode = false;
        GameSettings.ReplayPath = "";
        SceneManager.LoadScene("MainMenue");
    }
}
