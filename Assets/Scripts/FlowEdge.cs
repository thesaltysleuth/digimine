using UnityEngine;


[RequireComponent(typeof(LineRenderer))]
public class FlowEdge : MonoBehaviour
{
    [Header("Connected Node References")]
    public Node nodeA;
    public Node nodeB;

    [Header("Flow Settings")]
    public float speedMultiplier = 0.1f;
    public float arrowSpacing = 35.0f;

    private LineRenderer lineRenderer;
    private Material instanceMaterial;
    private float textureOffset = 0.0f;
    private double lastEditorTime = 0.0;

    private static readonly int BaseMapST_ID = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexST_ID = Shader.PropertyToID("_MainTex_ST");

    private void Awake()
    {
        SetupLineRenderer();
    }

    private void OnEnable()
    {
        SetupLineRenderer();
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.useWorldSpace = true;

            if (lineRenderer.sharedMaterial != null && (instanceMaterial == null || instanceMaterial.shader != lineRenderer.sharedMaterial.shader))
            {
                instanceMaterial = new Material(lineRenderer.sharedMaterial);
                instanceMaterial.name = $"{gameObject.name}_InstanceMaterial";
                lineRenderer.material = instanceMaterial;
            }
        }
    }

    private void Update()
    {
        if (nodeA == null || nodeB == null) return;
        if (lineRenderer == null) SetupLineRenderer();
        if (lineRenderer == null) return;

        // Position endpoints at node centers
        Vector3 posA = nodeA.transform.position;
        Vector3 posB = nodeB.transform.position;
        lineRenderer.SetPosition(0, posA);
        lineRenderer.SetPosition(1, posB);

        float distance = Vector3.Distance(posA, posB);
        if (distance < 0.01f) return;

        // Calculate pressure difference
        float pressureDelta = nodeA.pressure - nodeB.pressure;

        // Frame delta time for Play Mode & Edit Mode
        float deltaTime = Time.deltaTime;
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
            deltaTime = (lastEditorTime > 0.0) ? (float)(currentTime - lastEditorTime) : 0.016f;
            lastEditorTime = currentTime;
#else
            deltaTime = 0.016f;
#endif
        }

        // Discrete arrow count along edge distance
        float arrowCount = Mathf.Max(1.0f, distance / Mathf.Max(1.0f, arrowSpacing));

        // Direction flipping:
        // pressureDelta >= 0 -> flow A to B (arrows point towards B: +scaleX)
        // pressureDelta < 0  -> flow B to A (arrows flip to point towards A: -scaleX)
        float directionSign = (pressureDelta >= 0f) ? 1.0f : -1.0f;
        float scaleX = directionSign * arrowCount;

        // Animate scrolling towards destination if pressure difference exists
        if (Mathf.Abs(pressureDelta) >= 0.01f)
        {
            float speed = Mathf.Abs(pressureDelta) * speedMultiplier;
            textureOffset -= speed * deltaTime;
        }

        // Apply ST vector to material instance
        Vector4 stVector = new Vector4(scaleX, 1.0f, textureOffset, 0.0f);

        if (lineRenderer.material != null)
        {
            if (lineRenderer.material.HasProperty(BaseMapST_ID))
                lineRenderer.material.SetVector(BaseMapST_ID, stVector);
            
            if (lineRenderer.material.HasProperty(MainTexST_ID))
                lineRenderer.material.SetVector(MainTexST_ID, stVector);
        }
    }

    private void OnDestroy()
    {
        if (instanceMaterial != null)
        {
            if (Application.isPlaying) Destroy(instanceMaterial);
            else DestroyImmediate(instanceMaterial);
        }
    }
}