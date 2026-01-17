using UnityEngine;
using UnityEngine.SceneManagement;

public class ReplayManifestUI : MonoBehaviour
{
    [SerializeField] private GameObject replayElement;
    [SerializeField] private Transform content;

    private ReplayManifest manifest;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /*    private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }*/

    [SerializeField] private string map;
    void Start()
    {
        manifest = ReplayFileManager.LoadManifest();

        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        for (int i = manifest.replays.Length - 1; i >= 0; i--)
        {
            string replay = manifest.replays[i];
            GameObject e = Instantiate(replayElement, content, false);

            if (e.TryGetComponent<ReplayUIElement>(out var ui))
            {
                ui.OnCreate(replay, this);
            }
        }

        Canvas.ForceUpdateCanvases();
    }
    public void ClearReplays()
    {
        ReplayFileManager.ClearManifest();
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        Canvas.ForceUpdateCanvases();
    }
    // Update is called once per frame
    public void ElementClicked(string path)
    {
        GameSettings.InReplayMode = true;
        GameSettings.ReplayPath = path;
        GameSettings.MapSceneName = "Battles";
        // 1. Start loading the scene asynchronously
        SceneManager.LoadScene("LoadingScene");

    }
}
