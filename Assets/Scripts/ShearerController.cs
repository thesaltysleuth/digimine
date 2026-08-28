using UnityEngine;

/// <summary>
/// Controls the Longwall Shearer / Cutter Loader machinery.
/// Moves along the coal seam face according to telemetry speed V [Hz -> m/min]
/// and direction F_SIDE (1 = Left, 0 = Right), with rotating cutter drums,
/// cutting spark particle effects, and coal dust emissions.
/// </summary>
public class ShearerController : MonoBehaviour
{
    [Header("Waypoints / Seam Path")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Machinery Parameters")]
    public float maxSpeedMetersPerMin = 20.0f; // 100Hz = 20 m/min
    public float drumRotationSpeed = 360f;     // deg/sec
    public Transform leftCutterDrum;
    public Transform rightCutterDrum;

    [Header("Particle Systems")]
    public ParticleSystem coalDustParticles;
    public ParticleSystem cuttingSparks;

    [Header("Current Telemetry")]
    public float currentSpeedHz = 0f;
    public float directionFSide = 1f; // 1 = Left, 0 = Right
    public float leftCutterCurrentAmp = 0f;
    public float rightCutterCurrentAmp = 0f;

    private float progress = 0.5f;

    private void Awake()
    {
        EnsureProceduralShearerModel();
    }

    public void UpdateTelemetry(float vHz, float fSide, float amp1, float amp2)
    {
        currentSpeedHz = vHz;
        directionFSide = fSide;
        leftCutterCurrentAmp = amp1;
        rightCutterCurrentAmp = amp2;
    }

    private void Update()
    {
        if (startPoint == null || endPoint == null) return;

        // Calculate speed in normalized progress per second
        float speedFactor = Mathf.Clamp01(currentSpeedHz / 100f);
        float actualMetersPerSec = (speedFactor * maxSpeedMetersPerMin) / 60f;
        float totalDistance = Vector3.Distance(startPoint.position, endPoint.position);

        if (totalDistance > 0.01f)
        {
            float directionSign = (directionFSide > 0.2f) ? -1f : 1f;
            float progressDelta = (actualMetersPerSec / totalDistance) * directionSign * Time.deltaTime;
            progress = Mathf.Clamp01(progress + progressDelta);

            // Move shearer along line between start and end point
            transform.position = Vector3.Lerp(startPoint.position, endPoint.position, progress);
            
            // Orient face direction
            Vector3 travelDir = (endPoint.position - startPoint.position) * directionSign;
            if (travelDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(travelDir, Vector3.up);
            }
        }

        // Rotate cutting drums when cutter motors are energized
        bool isCutting = (leftCutterCurrentAmp > 5f || rightCutterCurrentAmp > 5f || currentSpeedHz > 5f);
        if (isCutting)
        {
            float rotationAmount = drumRotationSpeed * Time.deltaTime;
            if (leftCutterDrum != null) leftCutterDrum.Rotate(Vector3.right, rotationAmount, Space.Self);
            if (rightCutterDrum != null) rightCutterDrum.Rotate(Vector3.right, -rotationAmount, Space.Self);
        }

        // Update particle emissions
        if (coalDustParticles != null)
        {
            var emission = coalDustParticles.emission;
            emission.rateOverTime = isCutting ? 30f : 0f;
        }

        if (cuttingSparks != null)
        {
            var emission = cuttingSparks.emission;
            emission.rateOverTime = (leftCutterCurrentAmp > 40f || rightCutterCurrentAmp > 40f) ? 50f : 0f;
        }
    }

    private void EnsureProceduralShearerModel()
    {
        // If left and right cutter drums aren't set, build a stylized shearer assembly
        if (leftCutterDrum == null || rightCutterDrum == null)
        {
            // Create main body frame
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ShearerBody";
            body.transform.SetParent(transform);
            body.transform.localPosition = new Vector3(0, 0.6f, 0);
            body.transform.localScale = new Vector3(1.2f, 0.8f, 3.0f);
            SetMaterialColor(body, new Color(0.9f, 0.6f, 0.1f)); // High-visibility industrial yellow

            // Create Left Cutting Drum (Spiked Cylinder)
            GameObject leftDrumObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftDrumObj.name = "LeftCutterDrum";
            leftDrumObj.transform.SetParent(transform);
            leftDrumObj.transform.localPosition = new Vector3(-0.9f, 0.6f, 1.4f);
            leftDrumObj.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            leftDrumObj.transform.localRotation = Quaternion.Euler(0, 0, 90);
            SetMaterialColor(leftDrumObj, new Color(0.2f, 0.2f, 0.25f)); // Dark steel
            leftCutterDrum = leftDrumObj.transform;

            // Create Right Cutting Drum
            GameObject rightDrumObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightDrumObj.name = "RightCutterDrum";
            rightDrumObj.transform.SetParent(transform);
            rightDrumObj.transform.localPosition = new Vector3(0.9f, 0.6f, -1.4f);
            rightDrumObj.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            rightDrumObj.transform.localRotation = Quaternion.Euler(0, 0, 90);
            SetMaterialColor(rightDrumObj, new Color(0.2f, 0.2f, 0.25f));
            rightCutterDrum = rightDrumObj.transform;

            // Add LED Headlights
            GameObject lightObj = new GameObject("ShearerHeadlight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, 0.9f, 1.5f);
            Light lightComp = lightObj.AddComponent<Light>();
            lightComp.type = LightType.Spot;
            lightComp.range = 15f;
            lightComp.spotAngle = 60f;
            lightComp.intensity = 3.0f;
            lightComp.color = new Color(1.0f, 0.95f, 0.8f);

            // Add Coal Dust Particle System
            GameObject dustObj = new GameObject("CoalDustParticles");
            dustObj.transform.SetParent(transform);
            dustObj.transform.localPosition = new Vector3(0, 0.5f, 0);
            coalDustParticles = dustObj.AddComponent<ParticleSystem>();
            var dMain = coalDustParticles.main;
            dMain.startSize = 0.8f;
            dMain.startColor = new Color(0.15f, 0.15f, 0.15f, 0.5f); // Dark coal dust
            dMain.startLifetime = 1.5f;

            // Add Cutting Sparks Particle System
            GameObject sparkObj = new GameObject("CuttingSparks");
            sparkObj.transform.SetParent(transform);
            sparkObj.transform.localPosition = new Vector3(0, 0.5f, 1.4f);
            cuttingSparks = sparkObj.AddComponent<ParticleSystem>();
            var sMain = cuttingSparks.main;
            sMain.startSize = 0.15f;
            sMain.startColor = new Color(1.0f, 0.6f, 0.1f, 1.0f); // Glowing orange sparks
            sMain.startSpeed = 6.0f;
        }
    }

    private void SetMaterialColor(GameObject go, Color color)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color;
            mr.sharedMaterial = mat;
        }
    }
}
