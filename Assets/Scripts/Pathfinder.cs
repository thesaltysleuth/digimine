using System.Collections.Generic;
using UnityEngine;

public static class Pathfinder
{
    public const float CH4_LOW_THRESHOLD = 0.75f;
    public const float CH4_MED_THRESHOLD = 1.25f;
    public const float CH4_CRITICAL_THRESHOLD = 2.0f;

    /// <summary>
    /// Calculates the safest path from startNode to an exit node.
    /// Returns an ordered list of nodes starting with startNode and ending with the reached exit node.
    /// Returns null if no valid path exists.
    /// </summary>
    public static List<Node> FindSafestPath(Node startNode, List<Node> allNodes, List<Edge> allEdges)
    {
        if (startNode == null || allNodes == null || allNodes.Count == 0) return null;

        // 1. Identify exit nodes
        List<Node> exitNodes = GetExitNodes(allNodes);
        if (exitNodes.Count == 0)
        {
            Debug.LogWarning("[Pathfinder] No exit nodes found in graph.");
            return null;
        }

        HashSet<Node> exitSet = new HashSet<Node>(exitNodes);

        // If start node itself is an exit node
        if (exitSet.Contains(startNode))
        {
            return new List<Node> { startNode };
        }

        // Build adjacency map for fast lookup: Node -> List<(Node neighbor, Edge edge)>
        Dictionary<Node, List<(Node neighbor, Edge edge)>> adjacency = new Dictionary<Node, List<(Node neighbor, Edge edge)>>();
        foreach (var node in allNodes)
        {
            if (node != null) adjacency[node] = new List<(Node, Edge)>();
        }

        foreach (var edge in allEdges)
        {
            if (edge == null || edge.nodeA == null || edge.nodeB == null) continue;
            if (!adjacency.ContainsKey(edge.nodeA)) adjacency[edge.nodeA] = new List<(Node, Edge)>();
            if (!adjacency.ContainsKey(edge.nodeB)) adjacency[edge.nodeB] = new List<(Node, Edge)>();

            adjacency[edge.nodeA].Add((edge.nodeB, edge));
            adjacency[edge.nodeB].Add((edge.nodeA, edge));
        }

        // Dijkstra algorithm structures
        Dictionary<Node, float> dist = new Dictionary<Node, float>();
        Dictionary<Node, Node> prev = new Dictionary<Node, Node>();
        HashSet<Node> unvisited = new HashSet<Node>();

        foreach (var node in allNodes)
        {
            if (node == null) continue;
            dist[node] = float.PositiveInfinity;
            unvisited.Add(node);
        }

        dist[startNode] = 0f;

        Node targetExitNode = null;

        while (unvisited.Count > 0)
        {
            // Find unvisited node with smallest distance
            Node current = null;
            float minDistance = float.PositiveInfinity;

            foreach (var node in unvisited)
            {
                if (dist[node] < minDistance)
                {
                    minDistance = dist[node];
                    current = node;
                }
            }

            if (current == null || float.IsPositiveInfinity(minDistance))
            {
                // Remaining nodes are unreachable
                break;
            }

            // If we reached an exit node, we found the optimal route!
            if (exitSet.Contains(current))
            {
                targetExitNode = current;
                break;
            }

            unvisited.Remove(current);

            // Relax edges from current node
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;

            foreach (var (neighbor, edge) in neighbors)
            {
                if (!unvisited.Contains(neighbor)) continue;

                float edgeCost = CalculateEdgeCost(edge);
                if (float.IsPositiveInfinity(edgeCost)) continue; // Blocked or unsafe

                float alt = dist[current] + edgeCost;
                if (alt < dist[neighbor])
                {
                    dist[neighbor] = alt;
                    prev[neighbor] = current;
                }
            }
        }

        if (targetExitNode == null)
        {
            return null; // No safe path to any exit node
        }

        // Reconstruct path
        List<Node> path = new List<Node>();
        Node currNode = targetExitNode;
        while (currNode != null)
        {
            path.Add(currNode);
            if (currNode == startNode) break;
            prev.TryGetValue(currNode, out currNode);
        }

        path.Reverse();

        if (path.Count == 0 || path[0] != startNode)
        {
            return null;
        }

        return path;
    }

    /// <summary>
    /// Calculates dynamic traversal cost for an edge based on length, debris, and CH4 levels.
    /// </summary>
    public static float CalculateEdgeCost(Edge edge)
    {
        if (edge == null || edge.nodeA == null || edge.nodeB == null)
            return float.PositiveInfinity;

        // Blocked by debris or closed door -> impassable
        if (edge.isBlocked || (edge.door != null && edge.door.doorState == Door.DoorState.Closed))
            return float.PositiveInfinity;

        float distance = Vector3.Distance(edge.nodeA.transform.position, edge.nodeB.transform.position);
        float maxCh4 = Mathf.Max(edge.nodeA.ch4, edge.nodeB.ch4);

        // Methane cost multiplier
        float penaltyMultiplier = 1.0f;

        if (maxCh4 > CH4_CRITICAL_THRESHOLD)
        {
            // Above critical threshold: avoid completely
            return float.PositiveInfinity;
        }
        else if (maxCh4 > CH4_MED_THRESHOLD)
        {
            // High CH4 -> very high penalty
            penaltyMultiplier = 15.0f;
        }
        else if (maxCh4 >= CH4_LOW_THRESHOLD)
        {
            // Medium CH4 -> moderate penalty
            penaltyMultiplier = 3.0f;
        }
        else
        {
            // Low CH4 -> standard cost
            penaltyMultiplier = 1.0f;
        }

        return distance * penaltyMultiplier;
    }

    /// <summary>
    /// Finds exit nodes in graph. Uses isExit flag, or fallback by node name / fixed pressure node.
    /// </summary>
    public static List<Node> GetExitNodes(List<Node> allNodes)
    {
        List<Node> exits = new List<Node>();
        if (allNodes == null) return exits;

        // First pass: explicit isExit flag
        foreach (var node in allNodes)
        {
            if (node != null && node.isExit)
            {
                exits.Add(node);
            }
        }

        if (exits.Count > 0) return exits;

        // Fallback pass: check names like "Node_end", "Exit", or isFixedPressure
        foreach (var node in allNodes)
        {
            if (node == null) continue;
            string nameLower = node.name.ToLower();
            if (nameLower.Contains("exit") || nameLower.Contains("end") || node.isFixedPressure)
            {
                exits.Add(node);
            }
        }

        return exits;
    }
}
