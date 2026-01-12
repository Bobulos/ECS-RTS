using UnityEngine;
using UnityEngine.EventSystems;

public static class UIUtility
{
    /// <summary>
    /// Returns true if the pointer is currently over a UI element that is NOT ignored.
    /// </summary>
    public static bool IsPointerOverUI()
    {
        if (!EventSystem.current.IsPointerOverGameObject())
            return false;

        // Check if the element has a UIIgnore component
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.GetComponent<UIIgnore>() != null)
                continue; // Ignore this element

            return true; // Found a valid UI element under the pointer
        }

        return false;
    }
}