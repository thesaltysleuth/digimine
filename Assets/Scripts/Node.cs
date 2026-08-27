using System.Collections.Generic;
using UnityEngine;
using TMPro;

[ExecuteAlways]
[SelectionBase]
public class Node : MonoBehaviour
{
    public string nodeID;

    [Header("Pressure State")]
    public float pressure = 0f;

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
            Material defaultMat = tempCapsule.GetComponent<MeshRenderer>().sharedMaterial;
            
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = capsuleMesh;

            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            if (mr.sharedMaterial == null) mr.sharedMaterial = defaultMat;

            if (Application.isPlaying) Destroy(tempCapsule);
            else DestroyImmediate(tempCapsule);
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
            labelText.text = $"{nodeID}\n<size=75%>{pressure:F0} Pa</size>";
        }
    }

    private void OrientLabelToCamera()
    {
        if (labelText == null) return;
        Camera cam = Camera.main;
        if (cam == null && Camera.current != null) cam = Camera.current;

        if (cam != null)
        {
            // Facing camera directly prevents mirrored text
            labelText.transform.rotation = cam.transform.rotation;
        }
    }
}