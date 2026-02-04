using System;
using TMPro;
using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    [SerializeField] private float readRate = 0.3f;
    [SerializeField] private TextMeshProUGUI text;
    // Update is called once per frame
    private float fps;

    private void Start()
    {
        InvokeRepeating(nameof(UpdateFPS), 0f, readRate);
    }
    private void UpdateFPS()
    {
        text.text = $"{fps}";
    }
    private void Update()
    {
        fps = Mathf.Round(1f / Time.smoothDeltaTime);
    }
}
