using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Unity.NetCode;
public class GameManager : MapLoadedAccess
{
    //Events
    public static Action OnEndGame;


    [SerializeField] private GameObject inputPlayback;
    [SerializeField] private GameObject inputLogger;

    [Header("Local Playerdata OVERIDE")]
    public int localTeamOv = 1;
    private int localTeam = 0;
    [HideInInspector]public InputBridge inputBridge;
    [HideInInspector]public ConstructionBridge constructionBridge;
    [HideInInspector]public UnitActionManager unitActionManager;

    private EntityManager entityManager;

    private bool initialized = false;
    public override void OnLoad()
    {
        //OnChangeTeam()
        inputBridge = GameObject.FindFirstObjectByType<InputBridge>();
        constructionBridge = GameObject.FindFirstObjectByType<ConstructionBridge>();
        unitActionManager = GameObject.FindFirstObjectByType<UnitActionManager>();

        entityManager = ClientServerBootstrap.ClientWorld.EntityManager;

        localData = entityManager.CreateEntityQuery(typeof(LocalPlayerData));
        
        if (GameLoadConfig.InReplayMode)
        {
            var p = Instantiate(inputPlayback).GetComponent<InputPlayback>();
            p.StartReplay(GameLoadConfig.ReplayPath);
        }
        else
        {
            //Instantiate(inputLogger);
        }
    }
    private void Update()
    {
        if (!_ready)
        {
            return;
        }
        //Set player systems to local team data
        if (localData.TryGetSingleton<LocalPlayerData>(out var data))
        {
            if (data.TeamID != localTeam)
            {
                OnChangeTeam(data.TeamID);
            }
        }
        
        // if (!_ready)
        // {
        //     return;
        // }
        // if (!initialized)
        // {
        //     OnChangeTeam(localTeam);
        // }
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
            UnityEngine.Debug.Log($"Change player team to {newTeam}");

        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private EntityQuery localData;

    public void EndGame()
    {
        OnEndGame?.Invoke();

        GameLoadConfig.InReplayMode = false;
        GameLoadConfig.ReplayPath = "";
        SceneManager.LoadScene("MenueLAN");
    }
}
