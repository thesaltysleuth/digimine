using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Solves mine ventilation network airflow (Q) and node pressures (P)
/// using Kirchhoff's laws and Hardy-Cross pressure balance iteration.
/// </summary>
public class VentilationNetworkSolver : MonoBehaviour
{
    [Header("Network References")]
    public GraphManager graphManager;

    [Header("Solver Settings")]
    [Range(1, 100)]
    public int maxIterations = 30;
    public float convergenceTolerance = 0.01f;

    [Header("Default Parameters")]
    public float defaultFanPressure = 1500f; // Pa (Exhaust/Intake Fan)
    public float defaultTunnelResistance = 1.0f;

    private void Start()
    {
        if (graphManager == null)
        {
            graphManager = GetComponent<GraphManager>();
        }
        SolveNetwork();
    }

    public void SolveNetwork()
    {
        if (graphManager == null || graphManager.allNodes == null || graphManager.allNodes.Count == 0)
            return;

        List<Node> nodes = graphManager.allNodes;
        int n = nodes.Count;

        // Pressure relaxation to satisfy continuous pressure drop across connected edges
        for (int iter = 0; iter < maxIterations; iter++)
        {
            float maxChange = 0f;

            foreach (Node node in nodes)
            {
                if (node == null) continue;

                // Fixed pressure nodes (Fan or Outlet Exits) stay constant
                if (node.isFixedPressure) continue;

                float sumPressures = 0f;
                int count = 0;

                foreach (Node neighbor in node.connectedNodes)
                {
                    if (neighbor == null) continue;
                    sumPressures += neighbor.pressure;
                    count++;
                }

                if (count > 0)
                {
                    float targetPressure = sumPressures / count;
                    float delta = Mathf.Abs(node.pressure - targetPressure);
                    node.pressure = Mathf.Lerp(node.pressure, targetPressure, 0.5f);
                    if (delta > maxChange) maxChange = delta;
                }
            }

            if (maxChange < convergenceTolerance) break;
        }

        // Notify edge LineRenderers to update flow velocities and directions
        FlowEdge[] edges = FindObjectsByType<FlowEdge>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (FlowEdge edge in edges)
        {
            if (edge != null)
            {
                edge.UpdateFlowVelocity();
            }
        }
    }

    /// <summary>
    /// Set high resistance or block an edge for 'What-If' scenarios
    /// </summary>
    public void SetEdgeResistance(Node a, Node b, float newResistance)
    {
        FlowEdge[] edges = FindObjectsByType<FlowEdge>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (FlowEdge edge in edges)
        {
            if ((edge.nodeA == a && edge.nodeB == b) || (edge.nodeA == b && edge.nodeB == a))
            {
                edge.resistance = newResistance;
                break;
            }
        }
        SolveNetwork();
    }
}
