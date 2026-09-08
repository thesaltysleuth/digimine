using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("WASD Movement")]
    public float keyboardMoveSpeed = 15f;
    public float shiftSpeedMultiplier = 2f;

    [Header("MMB Panning")]
    public float panSpeed = 0.05f;

    [Header("RMB Orbit / Rotation")]
    public float rotateSpeed = 0.2f;
    public float minPitch = 10f;
    public float maxPitch = 85f;

    [Header("Scroll Zoom")]
    public float zoomSpeed = 0.01f;
    public float minZoomDist = 5f;
    public float maxZoomDist = 150f;

    [Header("Ground Limits")]
    public float minGroundHeight = 1.5f;

    [Header("Nearest Node & Focus Controls")]
    public float doubleClickTimeThreshold = 0.35f;
    public float focusDuration = 0.4f;
    public float defaultFocusDistance = 25f; // Comfortable framing distance on double-click
    public float maxSnapRadius = 15f;         // Max ground distance to snap to the nearest node

    // Internal State
    private Transform camTransform;
    private Vector3 targetPivotPosition;
    private float currentDistance = 30f;
    private float yaw = 0f;
    private float pitch = 45f;

    private float lastLmbClickTime = 0f;
    private Node selectedNode = null;

    private bool isFocusing = false;
    private Vector3 focusStartPosition;
    private Vector3 focusTargetPosition;
    private float focusStartDistance;
    private float focusTimer = 0f;

    private void Start()
    {
        camTransform = transform;
        targetPivotPosition = transform.position + transform.forward * currentDistance;
        targetPivotPosition.y = Mathf.Max(targetPivotPosition.y, 0f);

        Vector3 angles = camTransform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        // Prevent camera inputs when hovering over UI elements
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Prevent selection and camera focus while placing a leak
        if (LeakPlacementManager.Instance != null && LeakPlacementManager.Instance.isPlacing)
            return;

        HandleSelectionAndFocus();

        // Interrupt focus if manual navigation controls are touched
        if (isFocusing)
        {
            var kbd = Keyboard.current;
            if (Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed ||
                kbd.wKey.isPressed || kbd.aKey.isPressed || kbd.sKey.isPressed || kbd.dKey.isPressed)
            {
                isFocusing = false;
            }
            else
            {
                ExecuteFocusLerp();
            }
        }
        else
        {
            HandleWASD();
            HandlePanning();
            HandleRotation();
            HandleZoom();
        }

        UpdateCameraTransform();
    }

    private void HandleWASD()
    {
        var kbd = Keyboard.current;
        bool isShift = kbd.leftShiftKey.isPressed || kbd.rightShiftKey.isPressed;
        float speed = keyboardMoveSpeed * (isShift ? shiftSpeedMultiplier : 1f);

        float h = (kbd.dKey.isPressed ? 1f : 0f) - (kbd.aKey.isPressed ? 1f : 0f);
        float v = (kbd.wKey.isPressed ? 1f : 0f) - (kbd.sKey.isPressed ? 1f : 0f);

        if (h != 0 || v != 0)
        {
            Vector3 forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;

            Vector3 moveDir = (forward * v + right * h).normalized;
            targetPivotPosition += moveDir * speed * Time.deltaTime;
        }
    }

    private void HandlePanning()
    {
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            Vector3 panDelta = (-camTransform.right * mouseDelta.x * panSpeed) + (-camTransform.up * mouseDelta.y * panSpeed);
            targetPivotPosition += panDelta * (currentDistance * 0.05f);
        }
    }

    private void HandleRotation()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw += mouseDelta.x * rotateSpeed;
            pitch -= mouseDelta.y * rotateSpeed;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private void HandleZoom()
    {
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            currentDistance -= scrollY * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minZoomDist, maxZoomDist);
        }
    }

    private void HandleSelectionAndFocus()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            Node targetNode = GetNearestNodeFromRay(ray);

            if (targetNode != null)
            {
                selectedNode = targetNode;
                OnNodeSelected(selectedNode);

                float timeSinceLastClick = Time.time - lastLmbClickTime;
                if (timeSinceLastClick <= doubleClickTimeThreshold)
                {
                    StartFocusOnNode(targetNode.transform.position);
                }
            }
            lastLmbClickTime = Time.time;
        }
    }

    private Node GetNearestNodeFromRay(Ray ray)
    {
        // 1. Direct collider hit check
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Node directNode = hit.collider.GetComponentInParent<Node>();
            if (directNode != null) return directNode;
        }

        // 2. Nearest ground-intersection search if direct hit missed
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 groundHitPoint = ray.GetPoint(enter);

            Node nearestNode = null;
            float minDistanceSq = maxSnapRadius * maxSnapRadius;

            Node[] allNodes = Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
            foreach (var node in allNodes)
            {
                float distSq = (node.transform.position - groundHitPoint).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    nearestNode = node;
                }
            }
            return nearestNode;
        }

        return null;
    }

    private void StartFocusOnNode(Vector3 nodeWorldPos)
    {
        isFocusing = true;
        focusTimer = 0f;
        focusStartPosition = targetPivotPosition;
        focusTargetPosition = nodeWorldPos;
        focusStartDistance = currentDistance;
    }

    private void ExecuteFocusLerp()
    {
        focusTimer += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, focusTimer / focusDuration);

        targetPivotPosition = Vector3.Lerp(focusStartPosition, focusTargetPosition, t);
        
        // Smoothly adjust zoom to comfortable framing distance
        currentDistance = Mathf.Lerp(focusStartDistance, defaultFocusDistance, t);

        if (focusTimer >= focusDuration)
        {
            isFocusing = false;
        }
    }

    private void UpdateCameraTransform()
    {
        targetPivotPosition.y = Mathf.Max(targetPivotPosition.y, 0f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = targetPivotPosition - (rotation * Vector3.forward * currentDistance);

        if (position.y < minGroundHeight)
        {
            position.y = minGroundHeight;
        }

        camTransform.rotation = rotation;
        camTransform.position = position;
    }

    private void OnNodeSelected(Node node)
    {
        Debug.Log($"Selected Node: {node.gameObject.name} | Pressure: {node.pressure} Pa | CH4: {node.ch4}%");
    }
}