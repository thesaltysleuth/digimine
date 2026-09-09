using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DebrisPlacementManager : MonoBehaviour
{
    public static DebrisPlacementManager Instance { get; private set; }

    [Header("Debris Settings")]

    [Tooltip("Assign your Debris Prefab here (with or without Debris component attached).")]
    public GameObject debrisPrefab;

    [Header("Placement State & Snapping")]
    public bool isPlacing = false;
    [Tooltip("Maximum ground/world distance to snap to an edge line.")]
    public float maxSnapDistance = 25f;

    [Header("Invalid Cursor Settings")]
    [Tooltip("Texture2D for invalid placement cursor.")]
    public Texture2D invalidCursorTexture;
    public Vector2 cursorHotspot = new Vector2(16f, 16f);

    [Header("Indicator Sizing & Tuning")]
    public float indicatorRadius = 4.0f;
    public float verticalOffset = 0.5f;
    public bool renderAlwaysOnTop = true;

    [Header("Visual Colors")]
    public Color validSnapColor = new Color(1f, 0.6f, 0.1f, 0.65f); // Orange transparent sphere

    private GameObject previewObject;
    private GameObject snapIndicator;
    private GraphManager graphManager;

    // Snapping state
    private Vector3 currentSnappedPos;
    private Edge currentTargetEdge;
    private bool currentPlacementValid;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        graphManager = FindFirstObjectByType<GraphManager>();

        AutoFindDebrisPrefab();
        AutoFindProhibitionTexture();
        BindUIButton();
    }

    private void AutoFindDebrisPrefab()
    {
        if (debrisPrefab != null) return;

#if UNITY_EDITOR
        debrisPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/debris.prefab");
        if (debrisPrefab == null)
        {
            debrisPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JC_StylizedRocks_Lite/Prefabs/SM_Rocks_01.prefab");
        }
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

    private int lastToggleFrame = -1;
    private int activatedFrame = -1;

    private void BindUIButton()
    {
        string[] possibleNames = { "debris", "Debris", "debrisButton", "DebrisButton", "debris_button", "Debris_Button" };
        GameObject debrisButtonObj = null;

        foreach (var name in possibleNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null && found.GetComponent<RectTransform>() != null)
            {
                debrisButtonObj = found;
                break;
            }
        }

        if (debrisButtonObj != null)
        {
            Button btn = debrisButtonObj.GetComponent<Button>();
            if (btn == null) btn = debrisButtonObj.AddComponent<Button>();

            Graphic graphic = debrisButtonObj.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;

            btn.onClick.RemoveListener(TogglePlacementMode);
            btn.onClick.AddListener(TogglePlacementMode);
            Debug.Log("[DebrisPlacementManager] Successfully bound to UI Button: " + debrisButtonObj.name);
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

            // If leak placement is active, deactivate it
            if (LeakPlacementManager.Instance != null && LeakPlacementManager.Instance.isPlacing)
            {
                LeakPlacementManager.Instance.SetPlacementMode(false);
            }

            CreateIndicators();
            Debug.Log("[DebrisPlacementManager] Placement Mode ACTIVE - Hover over tunnels/edges to place debris.");
        }
        else
        {
            DestroyIndicators();
            ResetCursor();
            Debug.Log("[DebrisPlacementManager] Placement Mode CANCELLED.");
        }
    }

    private void Update()
    {
        if (isPlacing)
        {
            // Ignore input on the frame placement mode was activated
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

        currentPlacementValid = GetClosestPointOnEdge(ray, out Vector3 snappedPosition, out Edge targetEdge);
        currentSnappedPos = snappedPosition;
        currentTargetEdge = targetEdge;

        if (currentPlacementValid && targetEdge != null)
        {
            ResetCursor();

            // Calculate edge orientation vector
            Vector3 edgeDir = (targetEdge.nodeB.transform.position - targetEdge.nodeA.transform.position).normalized;
            if (edgeDir.sqrMagnitude < 0.001f) edgeDir = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(edgeDir, Vector3.up);

            // Position & show snap indicator
            Vector3 indicatorPos = snappedPosition + Vector3.up * verticalOffset;
            if (snapIndicator != null)
            {
                snapIndicator.SetActive(true);
                snapIndicator.transform.position = indicatorPos;
                snapIndicator.transform.localScale = Vector3.one * indicatorRadius;
            }

            // Position & show preview object
            if (previewObject != null)
            {
                previewObject.SetActive(true);
                previewObject.transform.position = snappedPosition;
                previewObject.transform.rotation = rotation;
            }

            // Place debris on left click
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                PlaceDebris(snappedPosition, targetEdge, rotation);
            }
        }
        else
        {
            SetInvalidCursor();

            if (previewObject != null) previewObject.SetActive(false);
            if (snapIndicator != null) snapIndicator.SetActive(false);
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

    private bool GetClosestPointOnEdge(Ray ray, out Vector3 snappedPoint, out Edge targetEdge)
    {
        snappedPoint = Vector3.zero;
        targetEdge = null;

        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
        if (graphManager == null || graphManager.edges == null || graphManager.edges.Count == 0)
        {
            return false;
        }

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

    private void PlaceDebris(Vector3 position, Edge edge, Quaternion rotation)
    {
        if (edge == null) return;

        // If edge is already blocked, destroy existing debris instance before placing new one
        if (edge.isBlocked && edge.debrisInstance != null)
        {
            Destroy(edge.debrisInstance);
        }

        GameObject debrisObj;

        if (debrisPrefab != null)
        {
            debrisObj = Instantiate(debrisPrefab, position, rotation);
        }
        else
        {
            // Fallback object if prefab not found
            debrisObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debrisObj.name = "Debris_Placeholder";
            debrisObj.transform.position = position;
            debrisObj.transform.rotation = rotation;
            debrisObj.transform.localScale = new Vector3(2f, 1.5f, 2f);

            Renderer r = debrisObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = CreateTransparentMaterial(new Color(0.4f, 0.25f, 0.1f, 1f), false);
            }
        }

        debrisObj.transform.SetParent(graphManager.transform);

        Debris debrisComp = debrisObj.GetComponent<Debris>();
        if (debrisComp == null)
        {
            debrisComp = debrisObj.AddComponent<Debris>();
        }

        debrisComp.Initialize(edge, position);

        Debug.Log($"[DebrisPlacementManager] Placed Debris on Edge ({edge.nodeA.name} <-> {edge.nodeB.name}) at {position}.");

        SetPlacementMode(false);
    }

    private void CreateIndicators()
    {
        DestroyIndicators();

        if (debrisPrefab != null)
        {
            previewObject = Instantiate(debrisPrefab);
            foreach (var col in previewObject.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }

        snapIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        snapIndicator.name = "DebrisPlacement_SnapIndicator";
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
            mat.SetFloat("_Mode", 3);
        }

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);

        if (alwaysOnTop)
        {
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;
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
}
