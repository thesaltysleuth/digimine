using UnityEngine;

/// <summary>
/// Controls fan rotation speed around the local X-axis based on ventilation airflow speed.
/// Automatically calculates the geometric center/pivot of the blades so downloaded models
/// with off-center origin pivots spin perfectly in place.
/// </summary>
public class FanController : MonoBehaviour
{
    [Header("Rotor Reference")]
    [Tooltip("Drag the child GameObject containing the spinning blades/rotor here (auto-detected if empty).")]
    public Transform rotorTransform;

    [Header("Pivot / Centering")]
    [Tooltip("Automatically calculate and rotate around the visual/mesh center of the blades.")]
    public bool autoCenterPivot = true;

    [Tooltip("Manual local pivot offset if autoCenterPivot is disabled or needs fine-tuning.")]
    public Vector3 localPivotOffset = Vector3.zero;

    [Header("Speed & Rotation Settings")]
    [Tooltip("Degrees per second of rotation per unit of fan speed.")]
    public float speedMultiplier = 4f;

    [Tooltip("Maximum rotation speed in degrees per second.")]
    public float maxRotationDegreesPerSec = 3600f;

    [Tooltip("How smoothly the fan accelerates/decelerates.")]
    public float acceleration = 3f;

    [Tooltip("Reverse rotation direction if spinning backwards.")]
    public bool reverseRotation = false;

    [Header("Airflow Integration")]
    [Tooltip("Current fan speed / pressure value. Automatically updated by slider.")]
    public float fanSpeed = 0f;

    [Tooltip("Automatically sync fan speed with AirflowPlacementManager slider.")]
    public bool autoSyncWithAirflow = true;

    [Header("Audio (Optional)")]
    public AudioSource fanAudioSource;
    public float maxAudioPitch = 1.5f;
    public float minAudioPitch = 0.5f;

    // Internal velocity tracking for smooth inertia
    private float currentVelocity = 0f;
    private bool isPivotInitialized = false;

    private void Start()
    {
        InitializeRotor();

        if (fanAudioSource == null)
        {
            fanAudioSource = GetComponent<AudioSource>();
        }

        // Initialize speed from manager if present
        if (autoSyncWithAirflow && AirflowPlacementManager.Instance != null && AirflowPlacementManager.Instance.fanSlider != null)
        {
            fanSpeed = AirflowPlacementManager.Instance.fanSlider.value;
        }
    }

    /// <summary>
    /// Finds rotor child and calculates its mesh center offset so it rotates in-place.
    /// </summary>
    public void InitializeRotor()
    {
        // Auto-detect rotor child if not manually assigned
        if (rotorTransform == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>();
            foreach (var t in children)
            {
                if (t == transform) continue;
                string lower = t.name.ToLower();
                if (lower.Contains("rotor") || lower.Contains("blade") || lower.Contains("fan") || lower.Contains("propeller") || lower.Contains("spin"))
                {
                    rotorTransform = t;
                    break;
                }
            }
        }

        if (rotorTransform != null && autoCenterPivot)
        {
            CalculateLocalCenterOffset();
        }

        isPivotInitialized = true;
    }

    /// <summary>
    /// Computes the combined bounds of all mesh renderers under rotorTransform
    /// to determine the exact geometric center point in local space.
    /// </summary>
    private void CalculateLocalCenterOffset()
    {
        if (rotorTransform == null) return;

        Renderer[] renderers = rotorTransform.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }
            localPivotOffset = rotorTransform.InverseTransformPoint(combined.center);
        }
        else
        {
            localPivotOffset = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!isPivotInitialized && rotorTransform != null)
        {
            InitializeRotor();
        }

        // Auto sync with Airflow slider
        if (autoSyncWithAirflow && AirflowPlacementManager.Instance != null)
        {
            if (AirflowPlacementManager.Instance.fanSlider != null)
            {
                fanSpeed = AirflowPlacementManager.Instance.fanSlider.value;
            }
            else if (AirflowPlacementManager.Instance.inletNode != null)
            {
                fanSpeed = AirflowPlacementManager.Instance.inletNode.pressure;
            }
        }

        // Calculate target rotational velocity (degrees / sec)
        float targetVelocity = Mathf.Clamp(fanSpeed * speedMultiplier, 0f, maxRotationDegreesPerSec);
        if (reverseRotation) targetVelocity = -targetVelocity;

        // Smooth acceleration / deceleration
        currentVelocity = Mathf.MoveTowards(currentVelocity, targetVelocity, (maxRotationDegreesPerSec / Mathf.Max(0.1f, acceleration)) * Time.deltaTime);

        // Rotate around the calculated center on the local X-axis
        if (rotorTransform != null && Mathf.Abs(currentVelocity) > 0.001f)
        {
            float deltaAngle = currentVelocity * Time.deltaTime;
            Vector3 worldCenter = rotorTransform.TransformPoint(localPivotOffset);
            Vector3 worldAxis = rotorTransform.TransformDirection(Vector3.right);

            rotorTransform.RotateAround(worldCenter, worldAxis, deltaAngle);
        }

        // Audio modulation if attached
        if (fanAudioSource != null)
        {
            float ratio = Mathf.Clamp01(Mathf.Abs(currentVelocity) / Mathf.Max(1f, maxRotationDegreesPerSec));
            if (ratio > 0.01f)
            {
                if (!fanAudioSource.isPlaying) fanAudioSource.Play();
                fanAudioSource.volume = ratio;
                fanAudioSource.pitch = Mathf.Lerp(minAudioPitch, maxAudioPitch, ratio);
            }
            else
            {
                if (fanAudioSource.isPlaying && ratio <= 0.001f)
                    fanAudioSource.Stop();
            }
        }
    }

    /// <summary>
    /// Set fan speed value directly from UI or external script.
    /// </summary>
    public void SetFanSpeed(float speed)
    {
        fanSpeed = Mathf.Max(0f, speed);
    }

    private void OnDrawGizmosSelected()
    {
        if (rotorTransform != null)
        {
            Vector3 center = rotorTransform.TransformPoint(localPivotOffset);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(center, rotorTransform.TransformDirection(Vector3.right) * 0.5f);
        }
    }
}
