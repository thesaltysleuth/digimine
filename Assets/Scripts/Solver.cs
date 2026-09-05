using System.Collections.Generic;
using UnityEngine;

public class Solver : MonoBehaviour
{
    public void SolveNetwork(List<Node> nodes, List<Edge> edges, int maxIterations = 100, float tolerance = 0.001f)
    {
        // 1. Solve Nodal Pressures (Gauss-Seidel Relaxation)
        for (int iter = 0; iter < maxIterations; iter++)
        {
            float maxChange = 0f;
            foreach (var node in nodes)
            {
                if (node.isFixedPressure) continue;

                float weightSum = 0f;
                float pressureSum = 0f;

                foreach (var edge in edges)
                {
                    if (edge.nodeA != node && edge.nodeB != node) continue;

                    Node neighbor = edge.GetOtherNode(node);
                    if (neighbor.pressure < 0) continue; // Skip unsolved neighbors

                    float weight = 1.0f / Mathf.Max(0.0001f, edge.resistance);
                    pressureSum += neighbor.pressure * weight;
                    weightSum += weight;
                }

                if (weightSum > 0)
                {
                    float newP = pressureSum / weightSum;
                    maxChange = Mathf.Max(maxChange, Mathf.Abs(newP - node.pressure));
                    node.pressure = newP;
                }
            }
            if (maxChange < tolerance) break;
        }

        // 2. Solve Flow Rates and Directions
        foreach (var edge in edges)
        {
            if (edge.nodeA == null || edge.nodeB == null) continue;

            float deltaP = edge.nodeA.pressure - edge.nodeB.pressure;
            edge.calculatedFlowRate = Mathf.Abs(deltaP) / Mathf.Max(0.0001f, edge.resistance);

            if (deltaP > 0.001f) edge.flowDirection = FlowDir.A_To_B;
            else if (deltaP < -0.001f) edge.flowDirection = FlowDir.B_To_A;
            else edge.flowDirection = FlowDir.Static;
        }

        // 3. Advect Methane Concentrations Downstream
        List<Node> sortedNodes = new List<Node>(nodes);
        sortedNodes.Sort((a, b) => b.pressure.CompareTo(a.pressure));

        foreach (var node in sortedNodes)
        {
            if (node.ch4 >= 0) continue; // Skip manually set sources

            float totalInflow = 0f;
            float weightedCh4Sum = 0f;

            foreach (var edge in edges)
            {
                if (edge.calculatedFlowRate <= 0) continue;

                bool isInflow = (edge.flowDirection == FlowDir.A_To_B && edge.nodeB == node) ||
                                (edge.flowDirection == FlowDir.B_To_A && edge.nodeA == node);

                if (isInflow) 
                {
                    Node sourceNode = edge.GetOtherNode(node);
                    if (sourceNode.ch4 >= 0)
                    {
                        weightedCh4Sum += edge.calculatedFlowRate * sourceNode.ch4;
                        totalInflow += edge.calculatedFlowRate;
                    }
                }
            }

            if (totalInflow > 0)
            {
                node.ch4 = weightedCh4Sum / totalInflow;
            }
            else
            {
                node.ch4 = 0f; // Default baseline if no incoming flow
            }
        }
    }
}