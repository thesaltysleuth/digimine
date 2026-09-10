using UnityEngine;
using TMPro;

public class Door : MonoBehaviour
{
    public enum DoorState { Open, Closed }

    [Header("Door State")]
    public DoorState doorState = DoorState.Closed;

    [Header("Switch Visuals (Optional - auto-finds if unassigned)")]
    [Tooltip("3D toggle switch GameObject for this door.")]
    public GameObject toggleSwitch;
    [Tooltip("Renderer of the switch capsule to change color.")]
    public Renderer switchRenderer;
    [Tooltip("Optional TextMeshPro label on the switch.")]
    public TextMeshPro switchText;

    [Header("Switch Colors")]
    public Color openColor = new Color(0f, 0.9f, 0.2f, 1f);   // Green
    public Color closedColor = new Color(1f, 0.08f, 0f, 1f);  // Red

    [Header("Switch Materials (Optional)")]
    public Material openMaterial;
    public Material closedMaterial;

    private Material runtimeSwitchMat;
    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        InitializeSwitch();
    }

    private void Start()
    {
        InitializeSwitch();
        UpdateSwitchVisual();
    }

    public void InitializeSwitch()
    {
        if (toggleSwitch != null)
        {
            if (switchRenderer == null)
                switchRenderer = toggleSwitch.GetComponent<Renderer>();

            if (switchText == null)
                switchText = toggleSwitch.GetComponentInChildren<TextMeshPro>();

            DoorSwitch ds = toggleSwitch.GetComponent<DoorSwitch>();
            if (ds == null) ds = toggleSwitch.AddComponent<DoorSwitch>();
            if (ds.targetDoor == null) ds.targetDoor = this;
        }

        if (Application.isPlaying && switchRenderer != null && runtimeSwitchMat == null)
            runtimeSwitchMat = switchRenderer.material;
    }

    public void Open()
    {
        doorState = DoorState.Open;
        UpdateSwitchVisual();
        NotifyGraphManager();
    }

    public void Close()
    {
        doorState = DoorState.Closed;
        UpdateSwitchVisual();
        NotifyGraphManager();
    }

    public void Toggle()
    {
        if (doorState == DoorState.Open) Close();
        else Open();
    }

    public void UpdateSwitchVisual()
    {
        if (switchRenderer == null && toggleSwitch != null)
            switchRenderer = toggleSwitch.GetComponent<Renderer>();

        if (switchRenderer == null) return;

        bool isOpen = (doorState == DoorState.Open);

        if (isOpen && openMaterial != null) { switchRenderer.sharedMaterial = openMaterial; return; }
        if (!isOpen && closedMaterial != null) { switchRenderer.sharedMaterial = closedMaterial; return; }

        Color targetColor = isOpen ? openColor : closedColor;

        if (Application.isPlaying)
        {
            if (runtimeSwitchMat == null) runtimeSwitchMat = switchRenderer.material;
            if (runtimeSwitchMat != null)
            {
                runtimeSwitchMat.color = targetColor;
                if (runtimeSwitchMat.HasProperty("_BaseColor"))
                    runtimeSwitchMat.SetColor("_BaseColor", targetColor);
            }
        }
        else
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            switchRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", targetColor);
            propBlock.SetColor("_Color", targetColor);
            switchRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void NotifyGraphManager()
    {
        GraphManager gm = FindFirstObjectByType<GraphManager>();
        if (gm != null) gm.RunSolverAndRender();
    }

    private void OnValidate()
    {
        UpdateSwitchVisual();
    }
}