using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private string sceneToLoad;

    void Start()
    {
        if (string.IsNullOrEmpty(GameSettings.MapSceneName) || SceneUtility.GetBuildIndexByScenePath(GameSettings.MapSceneName) == -1) { SceneManager.LoadScene("MainMenue"); }
        // Start the background loading process
        StartCoroutine(LoadAsyncOperation());
    }

    IEnumerator LoadAsyncOperation()
    {
        // 1. Start loading the scene in the background
        AsyncOperation gameLevel = SceneManager.LoadSceneAsync(sceneToLoad);

        // 2. Prevent the scene from activating immediately so we can see the bar hit 100%
        gameLevel.allowSceneActivation = false;

        while (!gameLevel.isDone)
        {
            // Unity's progress goes from 0 to 0.9. 1.0 is the activation phase.
            float progress = Mathf.Clamp01(gameLevel.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            // 3. Once progress is 0.9, the scene is ready to activate
            if (gameLevel.progress >= 0.9f)
            {
                // Optional: Wait for a second or a button press before switching
                gameLevel.allowSceneActivation = true;

            }

            yield return null;
        }
    }
}