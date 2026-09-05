using UnityEngine;
using TMPro;

public class Node : MonoBehaviour
{
    [Header("Input Parameters (Leave -1 for Unknown)")]
    public float pressure = -1f; // Pa (-1 = Undefined/To Solve)
    public float ch4 = -1f;      // % Concentration (-1 = Undefined/To Solve)

    [Header("Boundary Flags")]
    public bool isFixedPressure = false;

    [Header("UI Reference")]
    public TextMeshPro labelText; // Drag child TextMeshPro component here

    // Call this method whenever pressure or CH4 updates
    public void UpdateLabel()
    {
        if (labelText == null)
            labelText = GetComponentInChildren<TextMeshPro>();

        if (labelText == null) return;

        // Color coding CH4 text based on hazard level
        string ch4ColorHex = "#00E676"; // Green (Safe)
        if (ch4 > 1.25f) ch4ColorHex = "#FF1744"; // Red (Danger)
        else if (ch4 >= 0.75f) ch4ColorHex = "#FFEA00"; // Yellow (Warning)

        // Formatted RichText output to match your UI mockup
        string displayPressure = pressure >= 0 ? $"{pressure:F1} Pa" : "Calculating...";
        string displayCh4 = ch4 >= 0 ? $"{ch4:F2}% CH4" : "Calculating...";

        labelText.text = $"<color=#00E5FF>{gameObject.name}</color>\n" +
                         $"<size=75%>{displayPressure} | <color={ch4ColorHex}>{displayCh4}</color></size>";
    }

    private void OnValidate()
    {
        if (pressure >= 0)
        {
            isFixedPressure = true;
        }

        // Auto-find child TextMeshPro component if not assigned
        if (labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshPro>();
        }

        // Live-update label text in Inspector
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
        Gizmos.color = isFixedPressure ? Color.green : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}