using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject inputPlayback;
    [SerializeField] private GameObject inputLogger;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
