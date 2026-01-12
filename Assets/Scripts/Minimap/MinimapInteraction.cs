using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems; // Required for event trigger functionality

public class MinimapInteraction : MonoBehaviour, IPointerClickHandler
{
    public Vector2 worldMin = new Vector2(-200, -200);
    public Vector2 worldMax = new Vector2(200, 200);
    public RectTransform mapTransform;

    // Event triggered when a valid click position is found
    public static event Action<Vector3, int> OnClickEvent;

    public void OnPointerClick(PointerEventData d)
    {
        Debug.Log("Minimap clicked");
        if (mapTransform == null)
        {
            Debug.LogError("MinimapTransform or Rig not assigned.", this);
            return;
        }

        // 1. Get the local position of the mouse click within the RectTransform.
        // The last argument (null) means we are using the default screen space camera.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapTransform, Input.mousePosition, null, out Vector2 localPoint))
            return;

        Vector2 size = mapTransform.rect.size;

        // 2. Normalize the local point to a 0-1 range (bottom-left to top-right).
        // localPoint is relative to the pivot (often the center of the minimap).
        // If pivot is 0.5, 0.5: range is -size/2 to +size/2. 
        // We shift the center (0) to 0.5, then scale.
        Vector2 normalizedPos = new Vector2(
            (localPoint.x / size.x) + 0.5f,
            (localPoint.y / size.y) + 0.5f
        );

        // Clamp to ensure we are exactly within the 0 to 1 boundaries
        normalizedPos = math.clamp(normalizedPos, float2.zero, new float2(1, 1));

        // 3. Convert normalized 0-1 position to actual World XZ coordinates
        float worldRangeX = worldMax.x - worldMin.x;
        float worldRangeZ = worldMax.y - worldMin.y; // Y is used for World Z axis

        Vector3 clickedWorldPos = new Vector3(
            // X-coordinate
            worldMin.x + normalizedPos.x * worldRangeX,

            // Y-coordinate (Height)
            0,

            // Z-coordinate (Uses the normalized Y from the minimap)
            worldMin.y + normalizedPos.y * worldRangeZ
        );
        int button = 0;
        if (d.button == PointerEventData.InputButton.Right) button = 1;
        OnClickEvent?.Invoke(clickedWorldPos, button);

        //Debug.Log($"Minimap Click - Normalized: {normalizedPos} | World XZ: ({clickedWorldPos.x:F2}, {clickedWorldPos.z:F2})");
    }
}