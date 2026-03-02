using Unity.Entities;
using UnityEngine;
using Unity.NetCode;
using System.Collections.Generic;

public class LobbyGUISystem : MonoBehaviour
{
    public GameObject _lobbyPlayerPrefab;
    private List<GameObject> _lobbyPlayerInstances = new List<GameObject>();

    private World _clientWorld;
    private EntityQuery _lobbyDataQuery;
    private LobbyData _lobbyDataPrev;

    void OnEnable()
    {
        _clientWorld = ClientServerBootstrap.ClientWorld;
        _lobbyDataQuery = _clientWorld.EntityManager.CreateEntityQuery(typeof(LocalLobbyData));
    }

    void Update()
    {
        if (_lobbyDataQuery.IsEmpty) return;

        var lobbyData = _lobbyDataQuery.GetSingleton<LocalLobbyData>().Data;

        if (lobbyData.PlayerCount == _lobbyDataPrev.PlayerCount) return;

        Debug.Log($"<color=cyan>[LobbyGUI][Client] Player count updated: {lobbyData.PlayerCount}</color>");
        _lobbyDataPrev = lobbyData;

        RefreshPlayerList(lobbyData);
    }

private void RefreshPlayerList(LobbyData lobbyData)
{
    foreach (var instance in _lobbyPlayerInstances)
        Destroy(instance);
    _lobbyPlayerInstances.Clear();

    // Split the semicolon-separated names
    string[] playerNames = lobbyData.PlayerNames.ToString().Split(';');

    for (int i = 0; i < lobbyData.PlayerCount; i++)
    {
        var instance = Instantiate(_lobbyPlayerPrefab, transform);

        string playerName = i < playerNames.Length ? playerNames[i] : $"Player {i + 1}";
        instance.GetComponent<LobbyPlayer>().SetPlayerInfo(playerName, i);

        _lobbyPlayerInstances.Add(instance);
    }
}

    void OnDisable()
    {
        foreach (var instance in _lobbyPlayerInstances)
            Destroy(instance);
        _lobbyPlayerInstances.Clear();
    }
}