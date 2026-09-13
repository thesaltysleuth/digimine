using UnityEngine;
using TMPro;

public class Node : MonoBehaviour
{
    [Header("Input Parameters (Leave -1 for Unknown)")]
    public float pressure = -1f; // Pa (-1 = Undefined/To Solve)
    public float ch4 = -1f;      // % Concentration (-1 = Undefined/To Solve)

    [Header("Gas Generation")]
    public float methaneGenerationRate = 0f; // m³/s (Methane gas emission rate at this node)

    [Header("Calculated Results")]
    public float totalInflow = 0f;   // m³/s (Sum of incoming edge flows)
    public float totalOutflow = 0f;  // m³/s (Sum of outgoing edge flows)
    public float airflow = 0f;       // m³/s (Junction throughput = Max(In, Out))

    [Header("Boundary Flags")]
    public bool isFixedPressure = false;
    public bool isFixedCh4 = false;
    public bool isExit = false;

    [Header("Visual & Icon Settings")]
    [Tooltip("Icon prefab displayed above the node initially. Changeable in Inspector.")]
    public GameObject iconPrefab;
    public Vector3 iconOffset = new Vector3(0f, 1.2f, 0f);
    public Vector3 iconModelScale = new Vector3(0.018f, 0.018f, 0.018f);

    [Header("UI Panel Settings")]
    [Tooltip("Toggle showing node name in the minimal panel (default false).")]
    public bool showNodeName = false;
    [Tooltip("Whether the info panel is currently open/visible.")]
    public bool isInfoVisible = false;
    public Vector3 panelOffset = new Vector3(0f, 2.0f, -1.2f);
    public Vector3 panelRotation = new Vector3(60f, 180f, 0f);

    [Header("Internal UI References")]
    public GameObject iconInstance;
    public GameObject infoPanelInstance;
    public TextMeshPro labelText;

    private static Material sharedPanelMaterial;

    private void Awake()
    {
        EnsureIcon();
        EnsureInfoPanel();
    }

    private void Start()
    {
        SetInfoVisible(isInfoVisible);
        UpdateLabel();
    }

    public void ToggleInfo()
    {
        SetInfoVisible(!isInfoVisible);
    }

    public void SetInfoVisible(bool visible)
    {
        isInfoVisible = visible;

        if (infoPanelInstance == null)
            EnsureInfoPanel();

        if (infoPanelInstance != null)
        {
            infoPanelInstance.SetActive(visible);
        }

        if (visible)
        {
            UpdateLabel();
        }
    }

    public void EnsureIcon()
    {
        // Load default icon prefab if unassigned
        if (iconPrefab == null)
        {
#if UNITY_EDITOR
            iconPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/info.prefab");
#endif
        }

        // Find or create container
        Transform iconTransform = transform.Find("NodeIcon");
        if (iconTransform != null)
        {
            iconInstance = iconTransform.gameObject;
        }
        else
        {
            iconInstance = new GameObject("NodeIcon");
            iconInstance.transform.SetParent(transform, false);
        }

        iconInstance.transform.localPosition = iconOffset;
        iconInstance.transform.localRotation = Quaternion.identity;
        iconInstance.transform.localScale = Vector3.one;

        // Ensure collider on icon for raycast clicking
        SphereCollider col = iconInstance.GetComponent<SphereCollider>();
        if (col == null)
        {
            col = iconInstance.AddComponent<SphereCollider>();
        }
        col.radius = 1.0f;
        col.center = Vector3.zero;

        // Check model instance
        Transform modelTransform = iconInstance.transform.Find("IconModel");
        if (iconPrefab != null)
        {
            if (modelTransform == null)
            {
                GameObject model = Instantiate(iconPrefab, iconInstance.transform);
                model.name = "IconModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = iconPrefab.transform.localRotation;
                model.transform.localScale = Vector3.Scale(iconPrefab.transform.localScale, iconModelScale);
            }
        }
    }

    public void EnsureInfoPanel()
    {
        // Hide/clean up legacy Text (TMP) child if directly on node
        Transform legacyTMP = transform.Find("Text (TMP)");
        if (legacyTMP != null && legacyTMP.parent == transform)
        {
            legacyTMP.gameObject.SetActive(false);
        }

        // Find or create panel root
        Transform panelTransform = transform.Find("InfoPanel");
        if (panelTransform != null)
        {
            infoPanelInstance = panelTransform.gameObject;
        }
        else
        {
            infoPanelInstance = new GameObject("InfoPanel");
            infoPanelInstance.transform.SetParent(transform, false);
        }

        infoPanelInstance.transform.localPosition = panelOffset;
        infoPanelInstance.transform.localRotation = Quaternion.Euler(panelRotation);
        infoPanelInstance.transform.localScale = Vector3.one;

        // Ensure background plate
        Transform bgTransform = infoPanelInstance.transform.Find("BackgroundPlate");
        GameObject bgObj;
        if (bgTransform == null)
        {
            bgObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bgObj.name = "BackgroundPlate";
            bgObj.transform.SetParent(infoPanelInstance.transform, false);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localRotation = Quaternion.identity;

            Collider bgCol = bgObj.GetComponent<Collider>();
            if (bgCol != null)
            {
                if (Application.isPlaying) Destroy(bgCol);
                else DestroyImmediate(bgCol);
            }
        }
        else
        {
            bgObj = bgTransform.gameObject;
        }

        bgObj.transform.localScale = showNodeName ? new Vector3(4.2f, 2.2f, 0.08f) : new Vector3(4.2f, 1.6f, 0.08f);

        if (sharedPanelMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            sharedPanelMaterial = new Material(shader);
            sharedPanelMaterial.color = new Color(0.07f, 0.09f, 0.13f, 1f);
            if (sharedPanelMaterial.HasProperty("_BaseColor"))
                sharedPanelMaterial.SetColor("_BaseColor", sharedPanelMaterial.color);
            if (sharedPanelMaterial.HasProperty("_Smoothness"))
                sharedPanelMaterial.SetFloat("_Smoothness", 0.6f);
        }

        MeshRenderer bgRenderer = bgObj.GetComponent<MeshRenderer>();
        if (bgRenderer != null)
        {
            bgRenderer.sharedMaterial = sharedPanelMaterial;
        }

        // Ensure label TextMeshPro
        Transform textTransform = infoPanelInstance.transform.Find("LabelText");
        if (textTransform == null)
        {
            GameObject textObj = new GameObject("LabelText");
            textObj.transform.SetParent(infoPanelInstance.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            labelText = textObj.AddComponent<TextMeshPro>();
        }
        else
        {
            labelText = textTransform.GetComponent<TextMeshPro>();
        }

        if (labelText != null)
        {
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 24f;
            labelText.rectTransform.sizeDelta = new Vector2(26f, 12f);
            labelText.enableWordWrapping = false;
        }

        infoPanelInstance.SetActive(isInfoVisible);
    }

    // Call this method whenever pressure, airflow, or CH4 updates
    public void UpdateLabel()
    {
        if (labelText == null)
        {
            EnsureInfoPanel();
        }

        if (labelText == null) return;

        // Color coding CH4 text based on hazard level
        string ch4ColorHex = "#00E676"; // Green (Safe)
        if (ch4 > 1.25f) ch4ColorHex = "#FF1744"; // Red (Danger)
        else if (ch4 >= 0.75f) ch4ColorHex = "#FFEA00"; // Yellow (Warning)

        // Formatted display values
        string displayPressure = pressure >= 0 ? $"{pressure:F1} Pa" : "Calculating...";
        string displayAirflow = $"{airflow:F1} m³/s";
        string displayCh4 = ch4 >= 0 ? $"{ch4:F2}% CH4" : "Calculating...";

        if (showNodeName)
        {
            labelText.text = $"<size=85%><color=#00E5FF><b>{gameObject.name}</b></color></size>\n" +
                             $"<color=#ECEFF1>{displayPressure}</color>  |  <color=#81D4FA>{displayAirflow}</color>\n" +
                             $"<color={ch4ColorHex}><b>{displayCh4}</b></color>";
        }
        else
        {
            labelText.text = $"<color=#ECEFF1>{displayPressure}</color>  |  <color=#81D4FA>{displayAirflow}</color>\n" +
                             $"<color={ch4ColorHex}><b>{displayCh4}</b></color>";
        }

        Transform bgTransform = infoPanelInstance != null ? infoPanelInstance.transform.Find("BackgroundPlate") : null;
        if (bgTransform != null)
        {
            bgTransform.localScale = showNodeName ? new Vector3(4.2f, 2.2f, 0.08f) : new Vector3(4.2f, 1.6f, 0.08f);
        }
    }

    private void OnMouseDown()
    {
        // Support direct clicking on 3D node / icon
        ToggleInfo();
    }

    private void OnValidate()
    {
        EnsureIcon();
        EnsureInfoPanel();
        UpdateLabel();

        if (Application.isPlaying)
        {
            GraphManager manager = FindFirstObjectByType<GraphManager>();
            if (manager != null)
            {
                manager.RunSolverAndRender();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (isExit) Gizmos.color = Color.yellow;
        else Gizmos.color = isFixedPressure ? Color.green : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}