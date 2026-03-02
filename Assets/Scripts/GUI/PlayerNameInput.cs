using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class PlayerNameInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputFieldName;
    private string _previousName;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Only update the player name in GameLoadConfig if it has changed, to avoid unnecessary updates
        if (_inputFieldName.text.CompareTo(_previousName) != 0)
        {
            _previousName = _inputFieldName.text;
            GameLoadConfig.LocalPlayerName = _inputFieldName.text;
        }
        //GameLoadConfig.LocalPlayerName = _inputFieldName.text;
    }
}
