using UnityEngine;

public class MinimapCam : MonoBehaviour
{
    public Camera camera;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        //camera.enabled = false;
    }

    private void LateUpdate()
    {
        camera.Render();
        //this.enabled = false;
    }

    // Update is called once per frame
    /*    void LateUpdate()
        {
            if (camera.enabled != false)
            {
                Killcam();
            }

        }*/
}
