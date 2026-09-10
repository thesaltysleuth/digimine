using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NavigationPlacementManager : MonoBehaviour
{
    public static NavigationPlacementManager Instance { get; private set; }

    [Header("UI Component References (Assign in Inspector)")]
    [Tooltip("Button used to enter/exit miner placement mode.")]
    public Button navigationButton;

    [Tooltip("Button used to calculate and render the safest escape route after placing a miner.")]
    public Button startNavigationButton;

    [Tooltip("Panel or Text object displayed when no safe route is available.")]
    public GameObject noSafeRouteWarningPanel;

    [Header("Miner Prefab & Settings")]
    [Tooltip("Human prefab to spawn. Auto-detects Assets/objects/human.prefab if null.")]
    public GameObject minerPrefab;
    public float maxSnapDistance = 25f;

    [Header("Rotation & Transform Offset Settings")]
    [Tooltip("Rotation offset applied when spawning/previewing miner.")]
    public Vector3 placementRotationEuler = Vector3.zero;
    [Tooltip("Position transform offset applied to the spawned/preview miner.")]
    public Vector3 placementPositionOffset = Vector3.zero;
    [Tooltip("If true, position offset is calculated in local object space (rotated with object) to fix off-center prefab pivots. If false, applied in world space.")]
    public bool isLocalOffset = true;

    [Header("Cursor & Indicators")]
    public Texture2D invalidCursorTexture;
    public Vector2 cursorHotspot = new Vector2(16f, 16f);
    public float indicatorRadius = 3.0f;
    public float verticalOffset = 0.5f;
    public Color validSnapColor = new Color(0f, 0.8f, 1f, 0.65f); // Cyan transparent

    [Header("Path Renderer Settings")]
    public Color pathColor = new Color(0f, 0.9f, 1f, 1f); // Vibrant Cyan/Blue
    public float pathWidth = 2.0f;

    [Header("Placement State")]
    public bool isPlacing = false;

    // Active Instances
    private GameObject previewObject;
    private GameObject snapIndicator;
    private GameObject minerInstance;
    private Node minerStartNode;
    private Vector3 minerWorldPosition;

    private LineRenderer pathLineRenderer;
    private GraphManager graphManager;

    private int lastToggleFrame = -1;
    private int activatedFrame = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        graphManager = FindFirstObjectByType<GraphManager>();

        AutoFindMinerPrefab();
        AutoFindProhibitionTexture();
        SetupUIListeners();
        SetupPathRenderer();

        if (noSafeRouteWarningPanel != null)
        {
            noSafeRouteWarningPanel.SetActive(false);
        }

        if (startNavigationButton != null)
        {
            startNavigationButton.gameObject.SetActive(false);
        }
    }

    public void SetupUIListeners()
    {
        if (navigationButton != null)
        {
            navigationButton.onClick.RemoveListener(TogglePlacementMode);
            navigationButton.onClick.AddListener(TogglePlacementMode);
        }

        if (startNavigationButton != null)
        {
            startNavigationButton.onClick.RemoveListener(CalculateAndRenderPath);
            startNavigationButton.onClick.AddListener(CalculateAndRenderPath);
        }
    }

    private void AutoFindMinerPrefab()
    {
        if (minerPrefab != null) return;

#if UNITY_EDITOR
        minerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/human.prefab");
#endif
    }

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
    }

    private void SetupPathRenderer()
    {
        GameObject pathObj = new GameObject("Navigation_PathRenderer");
        pathObj.transform.SetParent(transform);
        pathLineRenderer = pathObj.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.color = pathColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", pathColor);

        pathLineRenderer.sharedMaterial = mat;
        pathLineRenderer.startWidth = pathWidth;
        pathLineRenderer.endWidth = pathWidth;
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.enabled = false;
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

            // Deactivate other placement modes
            if (DebrisPlacementManager.Instance != null && DebrisPlacementManager.Instance.isPlacing)
            {
                DebrisPlacementManager.Instance.SetPlacementMode(false);
            }
            if (LeakPlacementManager.Instance != null && LeakPlacementManager.Instance.isPlacing)
            {
                LeakPlacementManager.Instance.SetPlacementMode(false);
            }

            CreateIndicators();
            Debug.Log("[NavigationPlacementManager] Placement Mode ACTIVE - Hover over tunnels to place miner.");
        }
        else
        {
            DestroyIndicators();
            ResetCursor();
            Debug.Log("[NavigationPlacementManager] Placement Mode CANCELLED.");
        }
    }

    private void Update()
    {
        if (isPlacing)
        {
            if (Time.frameCount == activatedFrame) return;

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

        bool isValid = GetClosestPointOnEdge(ray, out Vector3 snappedPosition, out Edge targetEdge, out Node nearestNode);

        if (isValid && targetEdge != null)
        {
            ResetCursor();

            Vector3 edgeDir = (targetEdge.nodeB.transform.position - targetEdge.nodeA.transform.position).normalized;
            if (edgeDir.sqrMagnitude < 0.001f) edgeDir = Vector3.forward;
            Quaternion baseRotation = Quaternion.LookRotation(edgeDir, Vector3.up);
            Quaternion rotation = baseRotation * Quaternion.Euler(placementRotationEuler);
            Vector3 offset = isLocalOffset ? (rotation * placementPositionOffset) : placementPositionOffset;
            Vector3 targetPosition = snappedPosition + offset;

            Vector3 indicatorPos = snappedPosition + Vector3.up * verticalOffset;
            if (snapIndicator != null)
            {
                snapIndicator.SetActive(true);
                snapIndicator.transform.position = indicatorPos;
            }

            if (previewObject != null)
            {
                previewObject.SetActive(true);
                previewObject.transform.position = targetPosition;
                previewObject.transform.rotation = rotation;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                PlaceMiner(targetPosition, rotation, nearestNode);
            }
        }
        else
        {
            SetInvalidCursor();

            if (previewObject != null) previewObject.SetActive(false);
            if (snapIndicator != null) snapIndicator.SetActive(false);
        }
    }

    private void PlaceMiner(Vector3 position, Quaternion rotation, Node startNode)
    {
        if (minerInstance != null)
        {
            Destroy(minerInstance);
        }

        if (minerPrefab != null)
        {
            minerInstance = Instantiate(minerPrefab, position, rotation);
        }
        else
        {
            // Fallback human primitive capsule
            minerInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            minerInstance.name = "Miner_Placeholder";
            minerInstance.transform.position = position + Vector3.up * 1f;
            minerInstance.transform.rotation = rotation;
            minerInstance.transform.localScale = new Vector3(1f, 1.8f, 1f);

            Renderer r = minerInstance.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = Color.cyan;
            }
        }

        minerInstance.transform.SetParent(graphManager != null ? graphManager.transform : transform);
        minerStartNode = startNode;
        minerWorldPosition = position;

        Debug.Log($"[NavigationPlacementManager] Placed Miner near Node '{startNode?.name}' at position {position}.");

        // Clear previous navigation line/warnings
        ClearPath();

        // Show start navigation button
        if (startNavigationButton != null)
        {
            startNavigationButton.gameObject.SetActive(true);
        }

        SetPlacementMode(false);
    }

    public void CalculateAndRenderPath()
    {
        if (minerStartNode == null)
        {
            Debug.LogWarning("[NavigationPlacementManager] No miner has been placed yet!");
            return;
        }

        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();

        List<Node> safePath = Pathfinder.FindSafestPath(minerStartNode, graphManager.nodes, graphManager.edges);

        if (safePath != null && safePath.Count > 0)
        {
            Debug.Log($"[NavigationPlacementManager] Safe route calculated! Node count: {safePath.Count}");

            if (noSafeRouteWarningPanel != null)
            {
                noSafeRouteWarningPanel.SetActive(false);
            }

            RenderPath(safePath);
        }
        else
        {
            Debug.LogWarning("[NavigationPlacementManager] No safe escape route available!");

            ClearPath();

            if (noSafeRouteWarningPanel != null)
            {
                noSafeRouteWarningPanel.SetActive(true);
            }
        }
    }

    private void RenderPath(List<Node> pathNodes)
    {
        if (pathLineRenderer == null) SetupPathRenderer();

        List<Vector3> points = new List<Vector3>();

        // Start from miner's exact position
        Vector3 startPos = minerInstance != null ? minerInstance.transform.position : minerWorldPosition;
        points.Add(startPos + Vector3.up * verticalOffset);

        foreach (var node in pathNodes)
        {
            if (node != null)
            {
                points.Add(node.transform.position + Vector3.up * verticalOffset);
            }
        }

        pathLineRenderer.enabled = true;
        pathLineRenderer.positionCount = points.Count;
        pathLineRenderer.SetPositions(points.ToArray());
    }

    public void ClearPath()
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.positionCount = 0;
            pathLineRenderer.enabled = false;
        }

        if (noSafeRouteWarningPanel != null)
        {
            noSafeRouteWarningPanel.SetActive(false);
        }
    }

    private bool GetClosestPointOnEdge(Ray ray, out Vector3 snappedPoint, out Edge targetEdge, out Node nearestNode)
    {
        snappedPoint = Vector3.zero;
        targetEdge = null;
        nearestNode = null;

        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
        if (graphManager == null || graphManager.edges == null || graphManager.edges.Count == 0) return false;

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Vector3 rawHitPoint;

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
                targetEdge = edge;
            }
        }

        if (targetEdge != null && minDistanceSq < maxSnapDistance * maxSnapDistance)
        {
            snappedPoint = bestPoint;

            // Pick nearest node of the edge to bestPoint
            float distA = Vector3.Distance(bestPoint, targetEdge.nodeA.transform.position);
            float distB = Vector3.Distance(bestPoint, targetEdge.nodeB.transform.position);
            nearestNode = distA <= distB ? targetEdge.nodeA : targetEdge.nodeB;

            return true;
        }

        return false;
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

    private void CreateIndicators()
    {
        DestroyIndicators();

        if (minerPrefab != null)
        {
            previewObject = Instantiate(minerPrefab);
            foreach (var col in previewObject.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }

        snapIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        snapIndicator.name = "NavigationPlacement_SnapIndicator";
        snapIndicator.transform.localScale = Vector3.one * indicatorRadius;

        Collider indicatorCol = snapIndicator.GetComponent<Collider>();
        if (indicatorCol != null) Destroy(indicatorCol);

        Renderer indicatorRenderer = snapIndicator.GetComponent<Renderer>();
        if (indicatorRenderer != null)
        {
            indicatorRenderer.material = CreateTransparentMaterial(validSnapColor, true);
        }
    }

    private Material CreateTransparentMaterial(Color color, bool alwaysOnTop)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

        if (alwaysOnTop)
        {
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;
        }

        return mat;
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

    private void DestroyIndicators()
    {
        if (previewObject != null) { Destroy(previewObject); previewObject = null; }
        if (snapIndicator != null) { Destroy(snapIndicator); snapIndicator = null; }
    }

    private void OnDisable() { ResetCursor(); }
    private void OnDestroy() { ResetCursor(); }
}
