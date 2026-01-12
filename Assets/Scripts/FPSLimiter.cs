using UnityEngine;
public class FPSLimiter : MonoBehaviour
{
    public int targetFPS = 60; // Set your desired FPS here

    void Awake()
    {
        // Disable VSync to allow Application.targetFrameRate to work
        QualitySettings.vSyncCount = 0;

        // Set the target frame rate
        Application.targetFrameRate = targetFPS;
    }
}
