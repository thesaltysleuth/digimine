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

    [Header("3D Navigation Arrow Prefabs")]
    [Tooltip("Start of the arrow navigation object. Auto-detects Assets/objects/arrowstart.prefab if null.")]
    public GameObject arrowStartPrefab;
    [Tooltip("Body/corridor segment of the arrow navigation object. Auto-detects Assets/objects/arrowbody.prefab if null.")]
    public GameObject arrowBodyPrefab;
    [Tooltip("Head/tip of the arrow navigation object. Auto-detects Assets/objects/arrowhead.prefab if null.")]
    public GameObject arrowHeadPrefab;

    [Header("3D Navigation Arrow Controls & Transform")]
    [Tooltip("If true, renders the 3D arrow object instead of the 2D LineRenderer.")]
    public bool use3DArrowNavigation = true;
    [Tooltip("Vertical floating height above the tunnel floor/nodes.")]
    public float arrowFloatingHeight = 2.0f;
    [Tooltip("Overall uniform scale multiplier for the arrow objects.")]
    public float arrowUniformScale = 1.0f;
    [Tooltip("Width scale multiplier (lateral thickness across the tunnel).")]
    public float arrowWidthScale = 1.0f;
    [Tooltip("Height scale multiplier (vertical thickness).")]
    public float arrowHeightScale = 1.0f;
    [Tooltip("Length scale multiplier for individual body segments.")]
    public float arrowLengthScale = 1.0f;
    [Tooltip("Additional 3D position offset in world coordinates.")]
    public Vector3 arrowPositionOffset = Vector3.zero;
    [Tooltip("Corner overlap / miter multiplier (1.0 = exact miter extension to prevent corner gaps and misalignments).")]
    public float cornerOverlap = 1.0f;
    [Tooltip("Optional custom material override for the navigation arrow pieces. If null, keeps prefab material.")]
    public Material arrowMaterialOverride;
    [Tooltip("Optional custom color tint applied to the arrow pieces.")]
    public Color arrowTint = Color.white;
    [Tooltip("If true, disables colliders on spawned arrow pieces so they do not block mouse clicks or raycasts.")]
    public bool disableArrowColliders = true;

    [Header("Legacy Path Line Renderer Settings")]
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
    private GameObject navigationArrowInstance;
    private GraphManager graphManager;

    private const float BASE_START_LENGTH = 58.48f;
    private const float BASE_BODY_LENGTH = 58.48f;
    private const float BASE_HEAD_LENGTH = 136.25f;
    private const float BASE_WIDTH = 53.94f;
    private const float MESH_OFFSET_START = 112.05f;
    private const float MESH_OFFSET_BODY = 112.05f;
    private const float MESH_OFFSET_HEAD = 82.68f;

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
        AutoFindArrowPrefabs();
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

    private void AutoFindArrowPrefabs()
    {
#if UNITY_EDITOR
        if (arrowStartPrefab == null)
            arrowStartPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/arrowstart.prefab");
        if (arrowBodyPrefab == null)
            arrowBodyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/arrowbody.prefab");
        if (arrowHeadPrefab == null)
            arrowHeadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/arrowhead.prefab");
#endif
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
        List<Vector3> points = new List<Vector3>();

        // Start from miner's exact position
        Vector3 startPos = minerInstance != null ? minerInstance.transform.position : minerWorldPosition;
        float heightOffset = use3DArrowNavigation ? arrowFloatingHeight : verticalOffset;
        points.Add(startPos + Vector3.up * heightOffset + arrowPositionOffset);

        foreach (var node in pathNodes)
        {
            if (node != null)
            {
                points.Add(node.transform.position + Vector3.up * heightOffset + arrowPositionOffset);
            }
        }

        if (use3DArrowNavigation)
        {
            if (pathLineRenderer != null)
            {
                pathLineRenderer.positionCount = 0;
                pathLineRenderer.enabled = false;
            }
            Render3DArrow(points);
        }
        else
        {
            Clear3DArrow();
            if (pathLineRenderer == null) SetupPathRenderer();
            pathLineRenderer.enabled = true;
            pathLineRenderer.positionCount = points.Count;
            pathLineRenderer.SetPositions(points.ToArray());
        }
    }

    private void Render3DArrow(List<Vector3> waypoints)
    {
        Clear3DArrow();

        if (waypoints == null || waypoints.Count < 2) return;

        AutoFindArrowPrefabs();

        if (arrowStartPrefab == null || arrowBodyPrefab == null || arrowHeadPrefab == null)
        {
            Debug.LogWarning("[NavigationPlacementManager] One or more 3D arrow prefabs could not be found! Falling back to line renderer.");
            if (pathLineRenderer == null) SetupPathRenderer();
            pathLineRenderer.enabled = true;
            pathLineRenderer.positionCount = waypoints.Count;
            pathLineRenderer.SetPositions(waypoints.ToArray());
            return;
        }

        navigationArrowInstance = new GameObject("Navigation_3DArrow_Root");
        navigationArrowInstance.transform.SetParent(graphManager != null ? graphManager.transform : transform);

        float uniform = Mathf.Max(0.01f, arrowUniformScale);
        float scaleX = uniform * Mathf.Max(0.01f, arrowLengthScale);
        float scaleY = uniform * Mathf.Max(0.01f, arrowHeightScale);
        float scaleZ = uniform * Mathf.Max(0.01f, arrowWidthScale);

        float nominalStartLen = BASE_START_LENGTH * scaleX;
        float nominalBodyLen = BASE_BODY_LENGTH * scaleX;
        float nominalHeadLen = BASE_HEAD_LENGTH * scaleX;
        float halfWidth = BASE_WIDTH * 0.5f * scaleZ;

        int segmentCount = waypoints.Count - 1;

        for (int seg = 0; seg < segmentCount; seg++)
        {
            Vector3 A = waypoints[seg];
            Vector3 B = waypoints[seg + 1];

            Vector3 dir = (B - A).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            Vector3 upVector = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            // Mesh local +X is forward; Quaternion.Euler(0, -90, 0) correctly aligns local +X with dir
            Quaternion segRot = Quaternion.LookRotation(dir, upVector) * Quaternion.Euler(0, -90, 0);

            // Miter corner extension at start (A)
            float startOffset = 0f;
            if (seg > 0 && cornerOverlap > 0f)
            {
                Vector3 prevDir = (A - waypoints[seg - 1]).normalized;
                float dot = Mathf.Clamp(Vector3.Dot(prevDir, dir), -1f, 1f);
                float angleRad = Mathf.Acos(dot);
                float cornerMiter = halfWidth * Mathf.Tan(angleRad * 0.5f) * cornerOverlap;
                startOffset = -Mathf.Clamp(cornerMiter, 0f, halfWidth * 2f);
            }

            // Miter corner extension at end (B)
            float endOffset = 0f;
            if (seg < segmentCount - 1 && cornerOverlap > 0f)
            {
                Vector3 nextDir = (waypoints[seg + 2] - B).normalized;
                float dot = Mathf.Clamp(Vector3.Dot(dir, nextDir), -1f, 1f);
                float angleRad = Mathf.Acos(dot);
                float cornerMiter = halfWidth * Mathf.Tan(angleRad * 0.5f) * cornerOverlap;
                endOffset = Mathf.Clamp(cornerMiter, 0f, halfWidth * 2f);
            }

            Vector3 effectiveStart = A + dir * startOffset;
            Vector3 effectiveEnd = B + dir * endOffset;
            float segDist = Vector3.Distance(effectiveStart, effectiveEnd);
            if (segDist < 0.001f) continue;

            float currentDist = 0f;

            // 1. If very first segment, place the Arrow Start prefab
            if (seg == 0)
            {
                float actualStartLen = Mathf.Min(nominalStartLen, segDist * 0.4f);
                float thisScaleX = (actualStartLen / BASE_START_LENGTH);

                SpawnArrowPiece(arrowStartPrefab, effectiveStart + dir * currentDist, segRot,
                    new Vector3(thisScaleX, scaleY, scaleZ), MESH_OFFSET_START, "ArrowStart_0");

                currentDist += actualStartLen;
            }

            // Determine how much length is reserved for arrowhead if this is the final segment
            float headReservation = (seg == segmentCount - 1) ? Mathf.Min(nominalHeadLen, segDist * 0.4f) : 0f;
            float availableDist = segDist - headReservation;

            // 2. Place Arrow Body prefabs along this corridor segment with zero gaps
            while (currentDist < availableDist - 0.001f)
            {
                float remainingDist = availableDist - currentDist;
                float thisBodyLen = Mathf.Min(nominalBodyLen, remainingDist);
                float thisScaleX = (thisBodyLen / BASE_BODY_LENGTH);

                SpawnArrowPiece(arrowBodyPrefab, effectiveStart + dir * currentDist, segRot,
                    new Vector3(thisScaleX, scaleY, scaleZ), MESH_OFFSET_BODY, $"ArrowBody_{seg}_{currentDist:F1}");

                currentDist += thisBodyLen;
            }

            // 3. If final segment, place the Arrow Head prefab at the exit pointing towards the exit
            if (seg == segmentCount - 1)
            {
                float actualHeadLen = headReservation > 0f ? headReservation : Mathf.Min(nominalHeadLen, segDist);
                float thisScaleX = (actualHeadLen / BASE_HEAD_LENGTH);
                Vector3 headStartPos = effectiveEnd - dir * actualHeadLen;

                SpawnArrowPiece(arrowHeadPrefab, headStartPos, segRot,
                    new Vector3(thisScaleX, scaleY, scaleZ), MESH_OFFSET_HEAD, "ArrowHead_Exit");
            }
        }
    }

    private void SpawnArrowPiece(GameObject prefab, Vector3 worldPos, Quaternion worldRot, Vector3 localScale, float meshOffsetX, string pieceName)
    {
        if (prefab == null || navigationArrowInstance == null) return;

        GameObject anchor = new GameObject(pieceName);
        anchor.transform.SetParent(navigationArrowInstance.transform);
        anchor.transform.position = worldPos;
        anchor.transform.rotation = worldRot;

        GameObject piece = Instantiate(prefab, anchor.transform);
        piece.transform.localPosition = new Vector3(meshOffsetX * localScale.x, 0, 0);
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;

        if (disableArrowColliders)
        {
            foreach (var col in piece.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }

        if (arrowMaterialOverride != null || arrowTint != Color.white)
        {
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                if (arrowMaterialOverride != null)
                {
                    r.material = arrowMaterialOverride;
                }
                if (arrowTint != Color.white && r.material != null)
                {
                    if (r.material.HasProperty("_BaseColor"))
                        r.material.SetColor("_BaseColor", arrowTint);
                    else if (r.material.HasProperty("_Color"))
                        r.material.color = arrowTint;
                }
            }
        }
    }

    private void Clear3DArrow()
    {
        if (navigationArrowInstance != null)
        {
            Destroy(navigationArrowInstance);
            navigationArrowInstance = null;
        }
    }

    public void ClearPath()
    {
        Clear3DArrow();

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
