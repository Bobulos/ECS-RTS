using TMPro;
using UnityEngine;

public class ReplayUIElement : MonoBehaviour
{
    [SerializeField]private TextMeshProUGUI textUI;
    string filePath = string.Empty;
    ReplayManifestUI sys;
    public void OnCreate(string path, ReplayManifestUI system)
    {
        filePath = path;
        textUI.text = path.Remove(path.Length-4);
        sys = system;
    }
    public void OnClick()
    {
        sys.ElementClicked(filePath);
    }
}
