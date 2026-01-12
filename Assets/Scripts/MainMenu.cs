using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    //public string mapName;
    public void StartGame()
    {
        GameSettings.MapSceneName = "Battles";
        SceneManager.LoadScene("LoadingScene", LoadSceneMode.Single);
    }
}
