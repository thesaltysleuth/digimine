using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum NodeType
{
    Junction,
    SensorNode,
    FanNode,
    SurfaceOutlet
}

[ExecuteAlways]
[SelectionBase]
public class Node : MonoBehaviour
{
    public string nodeID;
    public NodeType nodeType = NodeType.Junction;
    public string sensorID = "";

    [Header("Pressure State")]
    public float pressure = 0f;
    public bool isFixedPressure = false;

    [Header("Gas Telemetry")]
    [Range(0f, 10f)]
    public float methaneConcentration = 0.2f; // % CH4
    [Range(0f, 5f)]
    public float co2Concentration = 0.04f;   // % CO2
    public float airflowVelocity = 2.5f;      // m/s

    [Header("UI Reference")]
    public TextMeshPro labelText;

    [Header("Graph Connections")]
    public List<Node> connectedNodes = new List<Node>();

    private void Awake()
    {
        EnsureCapsuleMesh();
        UpdateLabel();
    }

    private void Update()
    {
        EnsureCapsuleMesh();
        UpdateLabel();
        OrientLabelToCamera();
    }

    private void OnValidate()
    {
        EnsureCapsuleMesh();
        UpdateLabel();
    }

    public void EnsureCapsuleMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();

        if (mf == null || mf.sharedMesh == null)
        {
            GameObject tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh capsuleMesh = tempCapsule.GetComponent<MeshFilter>().sharedMesh;
            
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = capsuleMesh;

            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

            if (Application.isPlaying) Destroy(tempCapsule);
            else DestroyImmediate(tempCapsule);
        }

        // Apply clean semi-transparent node styling
        if (mr != null && (mr.sharedMaterial == null || mr.sharedMaterial.name == "Default-Material"))
        {
            Material nodeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            nodeMat.name = "TransparentNodeMaterial";
            nodeMat.color = new Color(0.2f, 0.8f, 1.0f, 0.35f); // Soft cyan transparent
            
            // Set surface to transparent if using URP Lit shader
            if (nodeMat.HasProperty("_Surface"))
            {
                nodeMat.SetFloat("_Surface", 1); // 1 = Transparent
                nodeMat.SetFloat("_Blend", 0);   // 0 = Alpha blend
                nodeMat.SetOverrideTag("RenderType", "Transparent");
                nodeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                nodeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                nodeMat.SetInt("_ZWrite", 0);
                nodeMat.DisableKeyword("_ALPHATEST_ON");
                nodeMat.EnableKeyword("_ALPHABLEND_ON");
                nodeMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                nodeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            mr.sharedMaterial = nodeMat;
        }
    }

    public void UpdateLabel()
    {
        if (string.IsNullOrEmpty(nodeID))
        {
            nodeID = gameObject.name;
        }

        if (labelText != null)
        {
            string gasColorHex = GetGasColorHex(methaneConcentration);
            string statusStr = !string.IsNullOrEmpty(sensorID) ? $"[{sensorID}] " : "";

            labelText.text = $"<color=#00E5FF>{statusStr}{nodeID}</color>\n" +
                             $"<size=75%>{pressure:F0} Pa | <color={gasColorHex}>{methaneConcentration:F2}% CH4</color></size>";
        }
    }

    private string GetGasColorHex(float ch4)
    {
        if (ch4 >= 1.5f) return "#FF3D00"; // Alarm Red
        if (ch4 >= 1.0f) return "#FFD600"; // Warning Yellow
        return "#00E676";                  // Normal Green
    }

    public Color GetGasColor(float ch4)
    {
        if (ch4 >= 1.5f) return new Color(1.0f, 0.24f, 0.0f, 0.9f); // Alarm Red
        if (ch4 >= 1.0f) return new Color(1.0f, 0.84f, 0.0f, 0.8f); // Warning Yellow
        return new Color(0.0f, 0.9f, 0.46f, 0.6f);                  // Normal Green
    }

    private void OrientLabelToCamera()
    {
        if (labelText == null) return;
        Camera cam = Camera.main;
        if (cam == null && Camera.current != null) cam = Camera.current;

        if (cam != null)
        {
            labelText.transform.rotation = cam.transform.rotation;
        }
    }
}