using System.Collections.Generic;
using UnityEngine;

public class GraphManager : MonoBehaviour
{
    [Header("Nodes")]
    public List<Node> allNodes = new List<Node>();

    [Header("Visual Settings")]
    public Material flowArrowMaterial;
    public float lineWidth = 1.0f;
    public float arrowSpacing = 35.0f;
    public float speedMultiplier = 0.1f;

    [Header("Auto Sync")]
    public bool autoFindNodesInScene = true;

    private void OnEnable()
    {
        SyncAndGenerate();
    }

    private void Start()
    {
        SyncAndGenerate();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= DelaySync;
        UnityEditor.EditorApplication.delayCall += DelaySync;
#endif
    }

    private void DelaySync()
    {
        if (this != null && gameObject != null)
        {
            SyncAndGenerate();
        }
    }

    public void SyncAndGenerate()
    {
        if (autoFindNodesInScene || allNodes == null || allNodes.Count == 0)
        {
            FindAllNodesInScene();
        }

        GenerateFlowLines();
    }

    public void FindAllNodesInScene()
    {
        Node[] foundNodes = Object.FindObjectsByType<Node>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        allNodes = new List<Node>(foundNodes);
    }

    public void GenerateFlowLines()
    {
        // Clean up previous edge children safely
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("Edge_"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        if (allNodes == null || allNodes.Count == 0) return;

        HashSet<string> processedPairs = new HashSet<string>();

        foreach (Node fromNode in allNodes)
        {
            if (fromNode == null) continue;

            foreach (Node toNode in fromNode.connectedNodes)
            {
                if (toNode == null) continue;

                int idA = fromNode.GetHashCode();
                int idB = toNode.GetHashCode();
                string pairKey = idA < idB ? $"{idA}_{idB}" : $"{idB}_{idA}";

                if (processedPairs.Contains(pairKey)) continue;
                processedPairs.Add(pairKey);

                // Create edge GameObject
                GameObject edgeObj = new GameObject($"Edge_{fromNode.nodeID}_{toNode.nodeID}");
                edgeObj.transform.SetParent(this.transform);

                LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
                if (flowArrowMaterial != null)
                {
                    lr.material = flowArrowMaterial;
                }
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.useWorldSpace = true;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;

                FlowEdge flowEdge = edgeObj.AddComponent<FlowEdge>();
                flowEdge.nodeA = fromNode;
                flowEdge.nodeB = toNode;
                flowEdge.arrowSpacing = arrowSpacing;
                flowEdge.speedMultiplier = speedMultiplier;
            }
        }
    }
}