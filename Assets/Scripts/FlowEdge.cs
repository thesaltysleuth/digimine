using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class FlowEdge : MonoBehaviour
{
    [Header("Connected Node References")]
    public Node nodeA;
    public Node nodeB;

    [Header("Edge Properties")]
    public float resistance = 1.0f;
    public bool isBlocked = false;

    [Header("Flow Settings")]
    public float speedMultiplier = 0.1f;
    public float arrowSpacing = 35.0f;
    public float calculatedFlowRate = 0.0f;

    [Header("Gas Particle Visuals")]
    public ParticleSystem gasParticleSystem;

    private LineRenderer lineRenderer;
    private Material instanceMaterial;
    private float textureOffset = 0.0f;
    private double lastEditorTime = 0.0;

    private static readonly int BaseMapST_ID = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexST_ID = Shader.PropertyToID("_MainTex_ST");
    private static readonly int BaseColor_ID = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        SetupLineRenderer();
        SetupGasParticles();
    }

    private void OnEnable()
    {
        SetupLineRenderer();
        SetupGasParticles();
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

    private void SetupGasParticles()
    {
        if (gasParticleSystem == null)
        {
            Transform pTransform = transform.Find("GasParticles");
            if (pTransform != null) gasParticleSystem = pTransform.GetComponent<ParticleSystem>();
        }

        if (gasParticleSystem == null)
        {
            GameObject pObj = new GameObject("GasParticles");
            pObj.transform.SetParent(transform);
            pObj.transform.localPosition = Vector3.zero;

            gasParticleSystem = pObj.AddComponent<ParticleSystem>();
            var main = gasParticleSystem.main;
            main.startSize = 1.2f;
            main.startLifetime = 2.0f;
            main.startSpeed = 1.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = gasParticleSystem.emission;
            emission.rateOverTime = 12f;

            var shape = gasParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
        }
    }

    public void UpdateFlowVelocity()
    {
        if (nodeA == null || nodeB == null) return;
        if (isBlocked)
        {
            calculatedFlowRate = 0f;
            return;
        }

        float deltaP = nodeA.pressure - nodeB.pressure;
        float safeResistance = Mathf.Max(0.01f, resistance);
        // Flow rate Q = deltaP / R
        calculatedFlowRate = deltaP / safeResistance;
    }

    private void Update()
    {
        if (nodeA == null || nodeB == null) return;
        if (lineRenderer == null) SetupLineRenderer();
        if (lineRenderer == null) return;

        Vector3 posA = nodeA.transform.position;
        Vector3 posB = nodeB.transform.position;
        lineRenderer.SetPosition(0, posA);
        lineRenderer.SetPosition(1, posB);

        float distance = Vector3.Distance(posA, posB);
        if (distance < 0.01f) return;

        float pressureDelta = isBlocked ? 0f : (nodeA.pressure - nodeB.pressure);

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

        float arrowCount = Mathf.Max(1.0f, distance / Mathf.Max(1.0f, arrowSpacing));
        float directionSign = (pressureDelta >= 0f) ? 1.0f : -1.0f;
        float scaleX = directionSign * arrowCount;

        if (Mathf.Abs(pressureDelta) >= 0.01f && !isBlocked)
        {
            float speed = Mathf.Abs(pressureDelta) * speedMultiplier;
            textureOffset -= speed * deltaTime;
        }

        Vector4 stVector = new Vector4(scaleX, 1.0f, textureOffset, 0.0f);

        if (lineRenderer.material != null)
        {
            if (lineRenderer.material.HasProperty(BaseMapST_ID))
                lineRenderer.material.SetVector(BaseMapST_ID, stVector);
            
            if (lineRenderer.material.HasProperty(MainTexST_ID))
                lineRenderer.material.SetVector(MainTexST_ID, stVector);

            // Dynamically tint flow edge arrows based on max gas concentration between nodeA and nodeB
            float maxGas = Mathf.Max(nodeA.methaneConcentration, nodeB.methaneConcentration);
            Color gasColor = nodeA.GetGasColor(maxGas);
            if (lineRenderer.material.HasProperty(BaseColor_ID))
                lineRenderer.material.SetColor(BaseColor_ID, gasColor);
        }

        // Update Gas Particle System position and flow vector
        if (gasParticleSystem != null)
        {
            var main = gasParticleSystem.main;
            float maxGas = Mathf.Max(nodeA.methaneConcentration, nodeB.methaneConcentration);
            main.startColor = nodeA.GetGasColor(maxGas);

            var shape = gasParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.position = (posA + posB) * 0.5f;

            var velocityOverLifetime = gasParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;

            Vector3 dir = (posB - posA).normalized * (directionSign * Mathf.Clamp(Mathf.Abs(pressureDelta) * 0.05f, 0.2f, 5f));
            velocityOverLifetime.x = dir.x;
            velocityOverLifetime.y = dir.y;
            velocityOverLifetime.z = dir.z;
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