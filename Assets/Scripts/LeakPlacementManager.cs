using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LeakPlacementManager : MonoBehaviour
{
    public static LeakPlacementManager Instance { get; private set; }

    [Header("Methane Leak Settings")]
    [Tooltip("Assign your Methane Leak Prefab here (with or without MethaneLeak component attached).")]
    public GameObject leakPrefab;
    public float defaultEmissionRate = 0.5f; // m³/s

    [Header("Rotation & Transform Offset Settings")]
    [Tooltip("Rotation offset applied when spawning the leak. Default (-90, 0, 0) points local Z straight UP into world Y.")]
    public Vector3 placementRotationEuler = new Vector3(-90f, 0f, 0f);
    [Tooltip("Position transform offset applied to the spawned/preview leak.")]
    public Vector3 placementPositionOffset = Vector3.zero;
    [Tooltip("If true, position offset is calculated in local object space (rotated with object) to fix off-center prefab pivots. If false, applied in world space.")]
    public bool isLocalOffset = true;

    [Header("Placement State & Snapping")]
    public bool isPlacing = false;
    [Tooltip("Maximum ground/world distance to snap to a node or edge line.")]
    public float maxSnapDistance = 25f;

    [Header("Invalid Cursor Settings")]
    [Tooltip("Texture2D for invalid placement cursor. Drag prohibition image here (auto-found from Assets/images/prohibition.png if unassigned).")]
    public Texture2D invalidCursorTexture;
    [Tooltip("Cursor hotspot offset in pixels (usually center of cursor icon).")]
    public Vector2 cursorHotspot = new Vector2(16f, 16f);

    [Header("Indicator Sizing & Tuning")]
    [Tooltip("Diameter of the yellow snap indicator sphere (in world units). Adjust live in Inspector!")]
    public float indicatorRadius = 4.0f;

    [Tooltip("Vertical height offset above ground/pipe surface.")]
    public float verticalOffset = 0.5f;

    [Tooltip("Render yellow snap indicator on top of scene geometry (x-ray / no depth occlusion).")]
    public bool renderAlwaysOnTop = true;

    [Header("Visual Colors")]
    public Color validSnapColor = new Color(1f, 0.85f, 0.1f, 0.65f); // Yellow transparent sphere

    private GameObject previewObject;
    private GameObject snapIndicator;
    private GraphManager graphManager;

    // Gizmo state cache
    private Vector3 currentSnappedPos;
    private Vector3 currentRawHitPos;
    private bool currentPlacementValid;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        graphManager = FindFirstObjectByType<GraphManager>();

        AutoFindProhibitionTexture();

        // Auto-find and bind UI button named "leak" if not manually wired
        GameObject leakButtonObj = GameObject.Find("leak");
        if (leakButtonObj == null) leakButtonObj = GameObject.Find("Leak");

        if (leakButtonObj != null && leakButtonObj.GetComponent<RectTransform>() != null)
        {
            Button btn = leakButtonObj.GetComponent<Button>();
            if (btn == null)
            {
                btn = leakButtonObj.AddComponent<Button>();
            }

            Graphic graphic = leakButtonObj.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            btn.onClick.RemoveListener(TogglePlacementMode);
            btn.onClick.AddListener(TogglePlacementMode);
            Debug.Log("[LeakPlacementManager] Successfully bound to UI Button: " + leakButtonObj.name);
        }
    }

    private int lastToggleFrame = -1;
    private int activatedFrame = -1;

    private void AutoFindProhibitionTexture()
    {
        if (invalidCursorTexture != null) return;

#if UNITY_EDITOR
        invalidCursorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/images/prohibition.png");
#endif

        if (invalidCursorTexture == null)
        {
            Texture2D[] texs = Resources.FindObjectsOfTypeAll<Texture2D>();
            foreach (var t in texs)
            {
                if (t != null && t.name.Equals("prohibition", System.StringComparison.OrdinalIgnoreCase))
                {
                    invalidCursorTexture = t;
                    break;
                }
            }
        }

        if (invalidCursorTexture != null)
        {
            Debug.Log("[LeakPlacementManager] Successfully loaded invalid cursor texture: " + invalidCursorTexture.name);
        }
    }

    public void TogglePlacementMode()
    {
        if (Time.frameCount == lastToggleFrame) return;
        lastToggleFrame = Time.frameCount;

        SetPlacementMode(!isPlacing);
    }

    public void SetPlacementMode(bool active)
    {
        if (isPlacing == active) return;

        isPlacing = active;

        if (isPlacing)
        {
            activatedFrame = Time.frameCount;

            // If debris placement is active, deactivate it
            if (DebrisPlacementManager.Instance != null && DebrisPlacementManager.Instance.isPlacing)
            {
                DebrisPlacementManager.Instance.SetPlacementMode(false);
            }

            CreateIndicators();
            Debug.Log("[LeakPlacementManager] Placement Mode ACTIVE - Hover over nodes/edges to place methane leak.");
        }
        else
        {
            DestroyIndicators();
            ResetCursor();
            Debug.Log("[LeakPlacementManager] Placement Mode CANCELLED.");
        }
    }

    private void Update()
    {
        if (isPlacing)
        {
            // Ignore input on the frame placement mode was activated
            if (Time.frameCount == activatedFrame) return;

            // Cancel placement on Escape or Right-Click
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            {
                SetPlacementMode(false);
                return;
            }

            UpdatePlacementCursor();
        }
    }

    private void UpdatePlacementCursor()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        currentPlacementValid = GetClosestPointOnNetwork(ray, out Vector3 snappedPosition, out Node nearestNode, out Vector3 rawHitPosition);
        currentSnappedPos = snappedPosition;
        currentRawHitPos = rawHitPosition;

        if (currentPlacementValid)
        {
            // --- VALID PLACEMENT LOCATION ---
            // 1. Reset mouse cursor to normal
            ResetCursor();

            // 2. Position & show yellow transparent snap indicator
            Vector3 indicatorPos = snappedPosition + Vector3.up * verticalOffset;
            if (snapIndicator != null)
            {
                snapIndicator.SetActive(true);
                snapIndicator.transform.position = indicatorPos;
                snapIndicator.transform.localScale = Vector3.one * indicatorRadius;
            }

            // 3. Position & show ghost preview pointing UP
            Quaternion rotation = Quaternion.Euler(placementRotationEuler);
            Vector3 offset = isLocalOffset ? (rotation * placementPositionOffset) : placementPositionOffset;
            Vector3 targetPosition = snappedPosition + offset;

            if (previewObject != null)
            {
                previewObject.SetActive(true);
                previewObject.transform.position = targetPosition;
                previewObject.transform.rotation = rotation;
            }

            // Handle left click to place
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Ensure we are not clicking over UI buttons
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                PlaceLeak(targetPosition, nearestNode);
            }
        }
        else
        {
            // --- INVALID PLACEMENT LOCATION ---
            // 1. Change cursor to invalid / prohibition sprite
            SetInvalidCursor();

            // 2. Hide ghost preview object and yellow snap indicator
            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }

            if (snapIndicator != null)
            {
                snapIndicator.SetActive(false);
            }
        }
    }

    private void SetInvalidCursor()
    {
        if (invalidCursorTexture != null)
        {
            Cursor.SetCursor(invalidCursorTexture, cursorHotspot, CursorMode.Auto);
        }
    }

    private void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDisable()
    {
        ResetCursor();
    }

    private void OnDestroy()
    {
        ResetCursor();
    }

     private bool GetClosestPointOnNetwork(Ray ray, out Vector3 snappedPoint, out Node nearestNode, out Vector3 rawHitPoint)
    {
        snappedPoint = Vector3.zero;
        nearestNode = null;
        rawHitPoint = Vector3.zero;

        if (graphManager == null) graphManager = FindAnyObjectByType<GraphManager>();
        if (graphManager == null || graphManager.nodes == null || graphManager.nodes.Count == 0)
        {
            return false;
        }

        // Raycast against scene colliders first, then ground plane (Y=0)
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            rawHitPoint = hit.point;
        }
        else if (groundPlane.Raycast(ray, out float enter))
        {
            rawHitPoint = ray.GetPoint(enter);
        }
        else
        {
            return false;
        }

        float minDistanceSq = maxSnapDistance * maxSnapDistance;
        Vector3 bestPoint = rawHitPoint;
        bool snappedToEdge = false;

        // 1. Check distance to Node centers
        foreach (var node in graphManager.nodes)
        {
            if (node == null) continue;
            Vector3 nodePos = node.transform.position;
            float distSq = (nodePos - rawHitPoint).sqrMagnitude;
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                bestPoint = nodePos;
                nearestNode = node;
            }
        }

        // 2. Check distance to Edge line segments
        foreach (var edge in graphManager.edges)
        {
            if (edge == null || edge.nodeA == null || edge.nodeB == null) continue;

            Vector3 posA = edge.nodeA.transform.position;
            Vector3 posB = edge.nodeB.transform.position;

            Vector3 closestOnSeg = ClosestPointOnSegment(rawHitPoint, posA, posB);
            float distSq = (closestOnSeg - rawHitPoint).sqrMagnitude;

            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                bestPoint = closestOnSeg;
                snappedToEdge = true;

                // The first node to detect a leak is the downstream endpoint.
                switch (edge.flowDirection)
                {
                    case FlowDir.A_To_B:
                        nearestNode = edge.nodeB;
                        break;

                    case FlowDir.B_To_A:
                        nearestNode = edge.nodeA;
                        break;

                    case FlowDir.Static:
                    default:
                        nearestNode = null;
                        break;
                }
            }
        }
        //Debug.Log($"[LeakPlacementManager] Closest point found at {bestPoint}, distance squared: {minDistanceSq}, snappedToEdge: {snappedToEdge}, nearestNode: {(nearestNode != null ? nearestNode.gameObject.name : "null")}");

        if (minDistanceSq < maxSnapDistance * maxSnapDistance)
        {
            snappedPoint = bestPoint;

            if (nearestNode == null && !snappedToEdge)
            {
                nearestNode = GetNearestNodeToPosition(bestPoint);
            }

            return true;
        }

        return false;
    }
       private Node GetNearestNodeToPosition(Vector3 pos)
    {
        Node closest = null;
        float minDistSq = float.MaxValue;
        foreach (var n in graphManager.nodes)
        {
            if (n == null) continue;
            float dSq = (n.transform.position - pos).sqrMagnitude;
            if (dSq < minDistSq)
            {
                minDistSq = dSq;
                closest = n;
            }
        }
        return closest;
    }

    private Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen == 0f) return a;

        float t = Vector3.Dot(p - a, ab) / sqrLen;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    private void PlaceLeak(Vector3 position, Node node)
    {
        if (node == null) return;

        Quaternion rotation = Quaternion.Euler(placementRotationEuler);
        GameObject leakObj;

        if (leakPrefab != null)
        {
            leakObj = Instantiate(leakPrefab, position, rotation);
        }
        else
        {
            // Fallback placeholder sphere if prefab not yet assigned
            leakObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leakObj.name = "MethaneLeak_Placeholder";
            leakObj.transform.position = position;
            leakObj.transform.rotation = rotation;
            leakObj.transform.localScale = Vector3.one * 1.5f;

            Renderer r = leakObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = CreateTransparentMaterial(Color.yellow, false);
            }
        }

        MethaneLeak leakComp = leakObj.GetComponent<MethaneLeak>();
        if (leakComp == null)
        {
            leakComp = leakObj.AddComponent<MethaneLeak>();
        }

        leakComp.Initialize(node, defaultEmissionRate);

        Debug.Log($"[LeakPlacementManager] Placed Methane Leak at {position}, affecting Node '{node.gameObject.name}' with +{defaultEmissionRate} m³/s methane rate.");

        // Finish placement mode
        SetPlacementMode(false);
    }

    private void CreateIndicators()
    {
        DestroyIndicators();

        // 1. Create Ghost Preview Object (using leakPrefab or fallback)
        if (leakPrefab != null)
        {
            previewObject = Instantiate(leakPrefab);
            // Disable colliders on ghost preview
            foreach (var col in previewObject.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }

        // 2. Create Yellow Transparent Snap Indicator Sphere
        snapIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        snapIndicator.name = "LeakPlacement_SnapIndicator";
        snapIndicator.transform.localScale = Vector3.one * indicatorRadius;

        Collider indicatorCol = snapIndicator.GetComponent<Collider>();
        if (indicatorCol != null) Destroy(indicatorCol);

        Renderer indicatorRenderer = snapIndicator.GetComponent<Renderer>();
        if (indicatorRenderer != null)
        {
            indicatorRenderer.material = CreateTransparentMaterial(validSnapColor, renderAlwaysOnTop);
        }
    }

    private Material CreateTransparentMaterial(Color color, bool alwaysOnTop)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3); // Transparent mode
        }

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);

        if (alwaysOnTop)
        {
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000; // Overlay render queue
        }

        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        return mat;
    }

    private void DestroyIndicators()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
        if (snapIndicator != null)
        {
            Destroy(snapIndicator);
            snapIndicator = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (!isPlacing) return;

        if (currentPlacementValid)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentSnappedPos + Vector3.up * verticalOffset, indicatorRadius * 0.5f);
        }
    }
}
