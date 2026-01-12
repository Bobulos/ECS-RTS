using UnityEngine;

public class SelectionBoxVisual : MonoBehaviour
{
    // The texture used to draw the selection box. 
    // You should assign a small (e.g., 1x1 pixel) white texture in the Inspector.
    public Texture2D boxTexture;
    public Color boxColor = new Color(0.8f, 0.8f, 0.9f, 0.2f); // Light blue, semi-transparent
    public Color borderColor = new Color(0.8f, 0.8f, 0.9f, 0.8f); // Light blue, less transparent
    public float borderWidth = 1f;

    // These will be set by your input system
    private Vector2 startScreenPosition;
    private Vector2 currentScreenPosition;
    private bool isDragging = false;

    // Call this method from your InputHandlerSystem or a dedicated InputManager
    public void StartSelection(Vector2 screenPos)
    {
        startScreenPosition = screenPos;
        currentScreenPosition = screenPos;
        isDragging = true;
    }

    // Call this method from your InputHandlerSystem or a dedicated InputManager
    public void UpdateSelection(Vector2 screenPos)
    {
        if (isDragging)
        {
            currentScreenPosition = screenPos;
        }
    }

    // Call this method from your InputHandlerSystem or a dedicated InputManager
    public void EndSelection()
    {
        isDragging = false;
    }

    // Unity's built-in OnGUI is suitable for simple UI overlays like this.
    // It runs after all other rendering.
    void OnGUI()
    {
        if (!isDragging || boxTexture == null)
        {
            return;
        }

        // Calculate the rectangle to draw in screen space
        Rect selectionRect = GetScreenRect(startScreenPosition, currentScreenPosition);

        // Draw the fill of the box
        GUI.color = boxColor;
        GUI.DrawTexture(selectionRect, boxTexture);

        // Draw the borders of the box
        GUI.color = borderColor;
        // Top border
        GUI.DrawTexture(new Rect(selectionRect.x, selectionRect.y, selectionRect.width, borderWidth), boxTexture);
        // Bottom border
        GUI.DrawTexture(new Rect(selectionRect.x, selectionRect.yMax - borderWidth, selectionRect.width, borderWidth), boxTexture);
        // Left border
        GUI.DrawTexture(new Rect(selectionRect.x, selectionRect.y, borderWidth, selectionRect.height), boxTexture);
        // Right border
        GUI.DrawTexture(new Rect(selectionRect.xMax - borderWidth, selectionRect.y, borderWidth, selectionRect.height), boxTexture);
    }

    // Helper to get a proper Rect, handling dragging in any direction
    Rect GetScreenRect(Vector2 p1, Vector2 p2)
    {
        // Invert Y coordinate because GUI is top-left origin, but mouse position is bottom-left
        p1.y = Screen.height - p1.y;
        p2.y = Screen.height - p2.y;

        var min = Vector2.Min(p1, p2);
        var max = Vector2.Max(p1, p2);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // You can call this in Start() to create a default white texture if none is assigned.
    // This is useful for quick setup but generally assigning one manually is better.
    void Awake()
    {
        if (boxTexture == null)
        {
            boxTexture = new Texture2D(1, 1);
            boxTexture.SetPixel(0, 0, Color.white);
            boxTexture.Apply();
        }
    }
}