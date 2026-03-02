using UnityEngine;
using TMPro;
public class LobbyPlayer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _textPlayerName;
    [SerializeField] private TextMeshProUGUI _textTeam;
    public void SetPlayerInfo(string playerName, int teamID)
    {
        _textPlayerName.text = playerName;
        _textTeam.text = $"Team {teamID}";
    }
}
