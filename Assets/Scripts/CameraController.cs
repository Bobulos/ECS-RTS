using UnityEngine;

/// <summary>
/// RTS-style camera controller:
/// - WASD / Arrow keys move camera
/// - Screen-edge movement
/// - Middle mouse drag to pan
/// - Scroll wheel to zoom (moves the camera along its local Z or changes height)
/// - Optional rotation (Q/E)
/// - Smooth damping and bounds clamping
/// Attach to an empty GameObject (Camera rig) and make the Camera a child with a local offset.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float panSpeed = 30f;              // base pan speed
    public float edgeScrollThickness = 12f;   // pixels from screen edge to trigger edge scroll
    public bool enableEdgeScroll = true;
    public bool enableKeyboard = true;
    public bool enableMouseDrag = true;
    public float dragPanSpeed = 1f;           // multiplier for middle mouse drag
    public float keyboardSpeedMultiplier = 1f;

    [Header("Zoom")]
    public float zoomSpeed = 200f;
    public float zoomDampTime = 0.15f;
    public float minZoom = 10f;   // minimum height or distance
    public float maxZoom = 80f;   // maximum height or distance
    public bool zoomAlongLocalZ = false; // if true, zoom moves camera along its local forward; otherwise modifies height (y)

    [Header("Rotation")]
    public bool allowRotation = true;
    public float rotationSpeed = 100f; // degrees per second
    public float rotationDampTime = 0.12f;

    [Header("Bounds (world space)")]
    public bool useBounds = true;
    public Vector2 minBounds = new Vector2(-50f, -50f); // x = minX, y = minZ
    public Vector2 maxBounds = new Vector2(50f, 50f);   // x = maxX, y = maxZ

    [Header("Smoothing")]
    public bool smoothMovement = true;
    public float smoothTime = 0.12f;

    // Internals
    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;

    private float targetZoom; // desired camera height or local z offset
    private float zoomVelocity = 0f;

    private float targetRotation; // desired Y rotation
    private float rotationVelocity = 0f;

    private Camera cam;
    private Transform camTransform;

    // For mouse drag
    private Vector3 dragOrigin;
    private bool isDragging = false;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("RTSCameraController: No child Camera found. Functionality will still work if camera is elsewhere, but zoom along local Z requires a camera.");
        }
        else
        {
            camTransform = cam.transform;
        }

        targetPosition = transform.position;
        targetRotation = transform.eulerAngles.y;

        // If zoom modifies height, initialize to current y; else use local z distance from parent to camera
        if (!zoomAlongLocalZ)
            targetZoom = transform.position.y;
        else if (camTransform != null)
            targetZoom = camTransform.localPosition.z;
    }

    void Update()
    {
        HandleInput(Time.deltaTime);
    }

    void LateUpdate()
    {
        ApplyMovement(Time.deltaTime);
    }

    void HandleInput(float dt)
    {
        Vector3 inputMove = Vector3.zero;

        // Keyboard movement
        if (enableKeyboard)
        {
            float hor = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
            float ver = Input.GetAxisRaw("Vertical");   // W/S or Up/Down
            Vector3 forward = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.right;

            inputMove += (right * hor + forward * ver) * panSpeed * keyboardSpeedMultiplier;
        }

        // Edge scrolling
        if (enableEdgeScroll)
        {
            Vector3 mousePos = Input.mousePosition;
            if (mousePos.x >= 0 && mousePos.y >= 0 && mousePos.x <= Screen.width && mousePos.y <= Screen.height)
            {
                if (mousePos.x >= Screen.width - edgeScrollThickness) inputMove += (Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.right) * panSpeed;
                if (mousePos.x <= edgeScrollThickness) inputMove += (Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.left) * panSpeed;
                if (mousePos.y >= Screen.height - edgeScrollThickness) inputMove += (Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward) * panSpeed;
                if (mousePos.y <= edgeScrollThickness) inputMove += (Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.back) * panSpeed;
            }
        }

        // Middle mouse drag panning
        if (enableMouseDrag)
        {
            if (Input.GetMouseButtonDown(2))
            {
                isDragging = true;
                dragOrigin = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(2))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 currentMouse = Input.mousePosition;
                Vector3 diff = currentMouse - dragOrigin;
                // convert screen delta to world move: move opposite the drag direction
                // scale by camera height for consistent feel
                float scale = (zoomAlongLocalZ ? Mathf.Abs(GetCameraHeightOrDistance()) : transform.position.y) * 0.0025f;
                Vector3 dragMove = (-transform.right * diff.x + -transform.forward * diff.y) * dragPanSpeed * scale;
                inputMove += dragMove;
                dragOrigin = currentMouse; // update origin so movement is incremental
            }
        }

        // Apply input move to target position (frame independent)
        targetPosition += inputMove * dt;

        // Zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float zoomChange = -scroll * zoomSpeed * dt;
            if (!zoomAlongLocalZ)
            {
                targetZoom = Mathf.Clamp(targetZoom + zoomChange, minZoom, maxZoom);
            }
            else if (camTransform != null)
            {
                // move camera along its local z (assuming camera looks at -z or forward; positive/negative depends on your setup)
                float newLocalZ = Mathf.Clamp(camTransform.localPosition.z + zoomChange, -maxZoom, -minZoom);
                targetZoom = newLocalZ;
            }
        }

        // Rotation (Q/E)
        if (allowRotation)
        {
            if (Input.GetKey(KeyCode.Q)) targetRotation += rotationSpeed * dt;
            if (Input.GetKey(KeyCode.E)) targetRotation -= rotationSpeed * dt;
        }

        // Clamp target position to bounds (only X,Z)
        if (useBounds)
        {
            float clampedX = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            float clampedZ = Mathf.Clamp(targetPosition.z, minBounds.y, maxBounds.y);
            // keep current y untouched (zoom handles height)
            targetPosition = new Vector3(clampedX, targetPosition.y, clampedZ);
        }
    }

    void ApplyMovement(float dt)
    {
        // Smooth position
        if (smoothMovement)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime, Mathf.Infinity, dt);
        }
        else
        {
            transform.position = targetPosition;
        }

        // Smooth rotation Y only
        if (allowRotation)
        {
            float currentY = transform.eulerAngles.y;
            float newY;
            if (rotationDampTime > 0f)
                newY = Mathf.SmoothDampAngle(currentY, targetRotation, ref rotationVelocity, rotationDampTime, Mathf.Infinity, dt);
            else
                newY = targetRotation;

            Vector3 e = transform.eulerAngles;
            e.y = newY;
            transform.eulerAngles = e;
        }

        // Zoom
        if (!zoomAlongLocalZ)
        {
            // treat targetZoom as Y height
            Vector3 desired = transform.position;
            desired.y = targetZoom;

            if (zoomDampTime > 0f)
                transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, zoomDampTime)));
            else
                transform.position = desired;
        }
        else if (camTransform != null)
        {
            Vector3 localPos = camTransform.localPosition;
            localPos.z = targetZoom;
            if (zoomDampTime > 0f)
                camTransform.localPosition = Vector3.Lerp(camTransform.localPosition, localPos, 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, zoomDampTime)));
            else
                camTransform.localPosition = localPos;
        }

        // Ensure bounds still honored after smoothing (important if smoothing changed values)
        if (useBounds)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, minBounds.x, maxBounds.x);
            p.z = Mathf.Clamp(p.z, minBounds.y, maxBounds.y);
            transform.position = p;
            targetPosition = p;
        }
    }

    // Utility to get camera height/distance used in drag offset scaling
    private float GetCameraHeightOrDistance()
    {
        if (!zoomAlongLocalZ) return transform.position.y;
        if (camTransform != null) return Mathf.Abs(camTransform.localPosition.z);
        return transform.position.y;
    }

    // Optional: allow programmatic move
    public void MoveTo(Vector3 worldPosition)
    {
        targetPosition = worldPosition;
    }

    // Optional: center on a world point immediately
    public void JumpTo(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        targetPosition = worldPosition;
    }
}
