using System.Collections.Generic;
using UnityEngine;

public class Solver : MonoBehaviour
{
    public void SolveNetwork(List<Node> nodes, List<Edge> edges, int maxIterations = 2000, float tolerance = 0.0001f)
    {
        if (nodes == null || edges == null) return;

        // 0. Initialize negative pressures on non-fixed nodes with an initial guess
        float sumFixedP = 0f;
        int countFixedP = 0;
        foreach (var node in nodes)
        {
            if (node != null && node.isFixedPressure && node.pressure >= 0)
            {
                sumFixedP += node.pressure;
                countFixedP++;
            }
        }
        float initialGuessP = countFixedP > 0 ? sumFixedP / countFixedP : 0f;

        foreach (var node in nodes)
        {
            if (node != null && !node.isFixedPressure && node.pressure < 0)
            {
                node.pressure = initialGuessP;
            }
        }

        // 1. Solve Nodal Pressures (Successive Over-Relaxation / SOR)
        float omega = 1.4f; // Over-relaxation factor for fast convergence
        for (int iter = 0; iter < maxIterations; iter++)
        {
            float maxChange = 0f;
            foreach (var node in nodes)
            {
                if (node == null || node.isFixedPressure) continue;

                float weightSum = 0f;
                float pressureSum = 0f;

                foreach (var edge in edges)
                {
                    if (edge == null || edge.isBlocked || edge.nodeA == null || edge.nodeB == null) continue;
                    if (edge.nodeA != node && edge.nodeB != node) continue;

                    Node neighbor = edge.GetOtherNode(node);
                    if (neighbor == null || neighbor.pressure < 0) continue;

                    float weight = 1.0f / Mathf.Max(0.0001f, edge.resistance);
                    pressureSum += neighbor.pressure * weight;
                    weightSum += weight;
                }

                if (weightSum > 0)
                {
                    float targetP = pressureSum / weightSum;
                    float newP = Mathf.Lerp(node.pressure, targetP, omega);
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

            if (edge.isBlocked)
            {
                edge.calculatedFlowRate = 0f;
                edge.flowDirection = FlowDir.Static;
                continue;
            }

            float deltaP = edge.nodeA.pressure - edge.nodeB.pressure;
            edge.calculatedFlowRate = Mathf.Abs(deltaP) / Mathf.Max(0.0001f, edge.resistance);

            if (deltaP > 0.001f) edge.flowDirection = FlowDir.A_To_B;
            else if (deltaP < -0.001f) edge.flowDirection = FlowDir.B_To_A;
            else edge.flowDirection = FlowDir.Static;
        }

        // 3. Calculate Nodal Airflow Rates
        foreach (var node in nodes)
        {
            if (node == null) continue;

            float totalInflow = 0f;
            float totalOutflow = 0f;

            foreach (var edge in edges)
            {
                if (edge == null || edge.nodeA == null || edge.nodeB == null) continue;

                if (edge.nodeA == node)
                {
                    if (edge.flowDirection == FlowDir.B_To_A) totalInflow += edge.calculatedFlowRate;
                    else if (edge.flowDirection == FlowDir.A_To_B) totalOutflow += edge.calculatedFlowRate;
                }
                else if (edge.nodeB == node)
                {
                    if (edge.flowDirection == FlowDir.A_To_B) totalInflow += edge.calculatedFlowRate;
                    else if (edge.flowDirection == FlowDir.B_To_A) totalOutflow += edge.calculatedFlowRate;
                }
            }

            node.totalInflow = totalInflow;
            node.totalOutflow = totalOutflow;
            node.airflow = Mathf.Max(totalInflow, totalOutflow);
        }

        // 4. Advect Methane Concentrations Downstream
        foreach (var node in nodes)
        {
            if (node != null && !node.isFixedCh4)
            {
                node.ch4 = -1f;
            }
        }

        List<Node> sortedNodes = new List<Node>(nodes);
        sortedNodes.Sort((a, b) => b.pressure.CompareTo(a.pressure));

        foreach (var node in sortedNodes)
        {
            if (node == null) continue;
            if (node.isFixedCh4) continue; // Skip manually set CH4 sources

            float totalInflow = 0f;
            float weightedCh4Sum = 0f;

            foreach (var edge in edges)
            {
                if (edge == null || edge.calculatedFlowRate <= 0) continue;

                bool isInflow = (edge.flowDirection == FlowDir.A_To_B && edge.nodeB == node) ||
                                (edge.flowDirection == FlowDir.B_To_A && edge.nodeA == node);

                if (isInflow) 
                {
                    Node sourceNode = edge.GetOtherNode(node);
                    if (sourceNode != null && sourceNode.ch4 >= 0)
                    {
                        weightedCh4Sum += edge.calculatedFlowRate * sourceNode.ch4;
                        totalInflow += edge.calculatedFlowRate;
                    }
                }
            }

            float localGen = Mathf.Max(0f, node.methaneGenerationRate);
            float totalGasAirflow = totalInflow + localGen;

            if (totalGasAirflow > 0)
            {
                // localGen is pure CH4 (100% concentration) added to the node's air volume
                node.ch4 = (weightedCh4Sum + (localGen * 100f)) / totalGasAirflow;
            }
            else
            {
                node.ch4 = 0f; // Default baseline if no incoming flow and no generation
            }
        }
    }
}