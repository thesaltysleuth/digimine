using UnityEngine;

public enum FlowDir { A_To_B, B_To_A, Static }

[System.Serializable]
public class Edge
{
    public Node nodeA;
    public Node nodeB;
    public float resistance = 1.0f;

    public float calculatedFlowRate = 0f;
    public FlowDir flowDirection = FlowDir.Static;
    [HideInInspector] public LineRenderer lineRenderer;

    public Edge(Node a, Node b)
    {
        nodeA = a;
        nodeB = b;
    }

    public Node GetOtherNode(Node current)
    {
        return current == nodeA ? nodeB : nodeA;
    }
}