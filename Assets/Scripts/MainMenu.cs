using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject _mainMenu;
    public GameObject _multiplayerMenu;
    public GameObject _createLobbyMenu;
    public GameObject _joinLobbyMenu;
    public GameObject _lobbyMenuClient;
    public GameObject _lobbyMenuHost;

    private ConnectionManager _connectionManager;
    void Start()
    {
        OnReturn();
        _connectionManager = GameObject.FindFirstObjectByType<ConnectionManager>();
    }
    public void OnReturn()
    {
        _mainMenu.SetActive(true);
        _multiplayerMenu.SetActive(false);
        _createLobbyMenu.SetActive(false);
        _joinLobbyMenu.SetActive(false);
        _lobbyMenuClient.SetActive(false);
        _lobbyMenuHost.SetActive(false);
    }
    public void OnEnterMultiplayerMenu()
    {
        _mainMenu.SetActive(false);
        _multiplayerMenu.SetActive(true);
    }

    public void OnEnterLobbyAsHost()
    {
        _connectionManager.OnHostPressed();
        _createLobbyMenu.SetActive(false);
        _lobbyMenuHost.SetActive(true);
    }
    public void OnEnterLobbyAsClient()
    {
        _connectionManager.OnJoinPressed();
        _joinLobbyMenu.SetActive(false);
        _lobbyMenuClient.SetActive(true);
    }
    public void OnJoinLobbyPressed()
    {
        _multiplayerMenu.SetActive(false);
        _joinLobbyMenu.SetActive(true);
    }
    public void OnCreateLobbyPressed()
    {
        _multiplayerMenu.SetActive(false);
        _createLobbyMenu.SetActive(true);
    }
    public void OnStartGame()
    {
        _connectionManager.HostStartGame();
    }
}
