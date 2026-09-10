using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class AirflowPlacementManager : MonoBehaviour
{
    public static AirflowPlacementManager Instance { get; private set; }

    [Header("UI & Controller Roots")]
    [Tooltip("UI Button on screen space Canvas (auto-detected if null).")]
    public Button airflowButton;

    [Tooltip("Root GameObject containing Fan controller and Door switches (auto-detected if null).")]
    public GameObject airflowControllersRoot;

    [Header("Fan Controller References")]
    [Tooltip("World space UI Slider controlling fan pressure/speed.")]
    public Slider fanSlider;

    [Tooltip("3D TextMeshPro displaying current fan speed / pressure.")]
    public TextMeshPro fanSpeedText;

    [Tooltip("Target inlet Node whose pressure is controlled by the fan slider (auto-detects Node_start if null).")]
    public Node inletNode;

    [Header("State")]
    public bool isControllersActive = false;

    private GraphManager graphManager;
    private int lastToggleFrame = -1;
    private int lastClickFrame = -1;   // Prevent double-dispatch on the same frame

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        graphManager = FindFirstObjectByType<GraphManager>();

        AutoFindAirflowButton();
        AutoFindAirflowControllersRoot();
        AutoFindFanComponents();
        SetupWorldCanvasCamera();

        if (fanSlider != null)
        {
            if (inletNode != null && inletNode.pressure >= 0)
                fanSlider.value = inletNode.pressure;

            fanSlider.onValueChanged.RemoveListener(OnFanSliderValueChanged);
            fanSlider.onValueChanged.AddListener(OnFanSliderValueChanged);
            UpdateSpeedText(fanSlider.value);
        }

        SetControllersActive(isControllersActive);
    }

    private void AutoFindAirflowButton()
    {
        if (airflowButton != null) return;

        string[] names = { "airflow", "Airflow", "airflowButton", "AirflowButton" };
        foreach (var n in names)
        {
            GameObject found = GameObject.Find(n);
            if (found != null && found.GetComponent<RectTransform>() != null)
            {
                airflowButton = found.GetComponent<Button>() ?? found.AddComponent<Button>();
                Graphic g = found.GetComponent<Graphic>();
                if (g != null) g.raycastTarget = true;
                break;
            }
        }

        if (airflowButton != null)
        {
            airflowButton.onClick.RemoveListener(ToggleAirflowMode);
            airflowButton.onClick.AddListener(ToggleAirflowMode);
            Debug.Log("[AirflowPlacementManager] Bound to UI Button: " + airflowButton.name);
        }
    }

    private void AutoFindAirflowControllersRoot()
    {
        if (airflowControllersRoot != null) return;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in all)
        {
            if (obj != null && obj.scene.isLoaded &&
                (obj.name == "Airflow controllers (1)" || obj.name == "Airflow controllers" || obj.name == "AirflowControllers"))
            {
                airflowControllersRoot = obj;
                break;
            }
        }
    }

    private void AutoFindFanComponents()
    {
        if (inletNode == null)
        {
            if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
            if (graphManager != null)
            {
                foreach (var n in graphManager.nodes)
                {
                    if (n != null && n.name.ToLower().Contains("start")) { inletNode = n; break; }
                }
                if (inletNode == null)
                {
                    foreach (var n in graphManager.nodes)
                    {
                        if (n != null && n.isFixedPressure && n.pressure > 0) { inletNode = n; break; }
                    }
                }
            }
        }

        if (airflowControllersRoot != null)
        {
            if (fanSlider == null)
                fanSlider = airflowControllersRoot.GetComponentInChildren<Slider>(true);

            if (fanSpeedText == null)
            {
                foreach (var tmp in airflowControllersRoot.GetComponentsInChildren<TextMeshPro>(true))
                {
                    if (tmp != null && tmp.gameObject.name.Equals("speed", System.StringComparison.OrdinalIgnoreCase))
                    {
                        fanSpeedText = tmp;
                        break;
                    }
                }
            }
        }
    }

    private void SetupWorldCanvasCamera()
    {
        if (airflowControllersRoot == null) return;
        Canvas wc = airflowControllersRoot.GetComponentInChildren<Canvas>(true);
        if (wc != null && wc.renderMode == RenderMode.WorldSpace && wc.worldCamera == null)
            wc.worldCamera = Camera.main;
    }

    public void ToggleAirflowMode()
    {
        if (Time.frameCount == lastToggleFrame) return;
        lastToggleFrame = Time.frameCount;
        SetControllersActive(!isControllersActive);
    }

    public void SetControllersActive(bool active)
    {
        isControllersActive = active;
        if (airflowControllersRoot != null)
            airflowControllersRoot.SetActive(isControllersActive);

        if (isControllersActive)
        {
            if (DebrisPlacementManager.Instance != null && DebrisPlacementManager.Instance.isPlacing)
                DebrisPlacementManager.Instance.SetPlacementMode(false);
            if (LeakPlacementManager.Instance != null && LeakPlacementManager.Instance.isPlacing)
                LeakPlacementManager.Instance.SetPlacementMode(false);
            if (NavigationPlacementManager.Instance != null && NavigationPlacementManager.Instance.isPlacing)
                NavigationPlacementManager.Instance.SetPlacementMode(false);

            SetupWorldCanvasCamera();
        }
    }

    private void Update()
    {
        HandleDoorClicks();
    }

    // Single, consolidated click handler — both capsule switches AND door meshes
    private void HandleDoorClicks()
    {
        if (Mouse.current == null || Camera.main == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // Prevent double-dispatch on same frame
        if (Time.frameCount == lastClickFrame) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        float maxDist = Camera.main.farClipPlane > 0 ? Camera.main.farClipPlane : 5000f;

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDist);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[AirflowPlacementManager] Click: no physics hits");
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            GameObject hitGO = hit.collider.gameObject;
            Debug.Log($"[AirflowPlacementManager] Hit: '{hitGO.name}' at dist {hit.distance:F1}");

            // 1. Direct DoorSwitch component
            DoorSwitch sw = hitGO.GetComponent<DoorSwitch>() ?? hitGO.GetComponentInParent<DoorSwitch>();
            if (sw != null)
            {
                Debug.Log($"[AirflowPlacementManager] Toggling via DoorSwitch on '{hitGO.name}'");
                lastClickFrame = Time.frameCount;
                sw.OnClick();
                return;
            }

            // 2. Direct Door component (clicking the door mesh itself)
            Door door = hitGO.GetComponent<Door>() ?? hitGO.GetComponentInParent<Door>();
            if (door != null)
            {
                Debug.Log($"[AirflowPlacementManager] Toggling Door '{door.name}' directly");
                lastClickFrame = Time.frameCount;
                door.Toggle();
                return;
            }

            // 3. Hit object is a Door's toggleSwitch GameObject
            Door[] allDoors = FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in allDoors)
            {
                if (d != null && d.toggleSwitch != null &&
                    (hitGO == d.toggleSwitch || hit.collider.transform.IsChildOf(d.toggleSwitch.transform)))
                {
                    Debug.Log($"[AirflowPlacementManager] Toggling Door '{d.name}' via toggleSwitch match '{hitGO.name}'");
                    lastClickFrame = Time.frameCount;
                    d.Toggle();
                    return;
                }
            }
        }
    }

    public void OnFanSliderValueChanged(float value)
    {
        if (inletNode != null)
        {
            inletNode.pressure = value;
            inletNode.UpdateLabel();
        }

        UpdateSpeedText(value);

        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
        if (graphManager != null) graphManager.RunSolverAndRender();
    }

    private void UpdateSpeedText(float value)
    {
        if (fanSpeedText != null)
            fanSpeedText.text = $"{value:F1} m³/s";
    }
}
