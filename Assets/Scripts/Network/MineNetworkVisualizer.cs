using System.Collections.Generic;
using MineVent.Domain;
using MineVent.Network;
using UnityEngine;

namespace MineVent.Visualization
{
    public class MineNetworkVisualizer : MonoBehaviour
    {
        [SerializeField] private float nodeRadius = 0.22f;
        [SerializeField] private float airwayRadius = 0.08f;
        [SerializeField] private bool drawLoops = true;

        private MineNetwork network;

        public void Build(MineNetwork mineNetwork)
        {
            network = mineNetwork;
            Build();
        }

        public void Build()
        {
            Clear();

            if (network == null)
            {
                Debug.LogWarning("MineNetworkVisualizer: network is null.");
                return;
            }

            var root = new GameObject("MineNetworkRoot");
            root.transform.SetParent(transform, false);

            // Draw nodes
            foreach (var node in network.Nodes.Values)
            {
                var nodeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodeObject.name = $"Node_{node.Id}";
                nodeObject.transform.SetParent(root.transform, false);
                nodeObject.transform.position = node.Position;
                nodeObject.transform.localScale = Vector3.one * nodeRadius * 2f;

                var renderer = nodeObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.color = node.IsBoundary ? Color.yellow : Color.cyan;
                }
            }

            // Draw airways as cylinders
            foreach (var airway in network.Airways.Values)
            {
                var startNode = network.GetNode(airway.StartNodeId);
                var endNode = network.GetNode(airway.EndNodeId);

                if (startNode == null || endNode == null)
                    continue;

                var start = startNode.Position;
                var end = endNode.Position;
                var direction = end - start;

                if (direction.sqrMagnitude < 0.0001f)
                    continue;

                var airwayObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                airwayObject.name = $"Airway_{airway.Id}";
                airwayObject.transform.SetParent(root.transform, false);

                var midpoint = (start + end) * 0.5f;
                airwayObject.transform.position = midpoint;
                airwayObject.transform.up = direction.normalized;

                var length = direction.magnitude;
                airwayObject.transform.localScale = new Vector3(
                    airwayRadius * 2f,
                    Mathf.Max(length * 0.5f, 0.001f),
                    airwayRadius * 2f
                );

                var renderer = airwayObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Standard"));

                    // Map airflow value to a color range
                    float normalizedFlow = Mathf.InverseLerp(-10f, 10f, airway.Q);
                    renderer.material.color = Color.Lerp(Color.blue, Color.red, normalizedFlow);
                }
            }

            // Draw loops as a simple polyline
            if (drawLoops)
            {
                foreach (var loop in network.Loops)
                {
                    var loopObject = new GameObject($"Loop_{loop.Id}");
                    loopObject.transform.SetParent(root.transform, false);

                    var line = loopObject.AddComponent<LineRenderer>();
                    line.material = new Material(Shader.Find("Sprites/Default"));
                    line.startWidth = 0.04f;
                    line.endWidth = 0.04f;
                    line.startColor = new Color(1f, 0.5f, 1f, 1f);
                    line.endColor = new Color(1f, 0.5f, 1f, 1f);
                    line.useWorldSpace = false;

                    var points = new List<Vector3>();

                    foreach (var airwayEntry in loop.AirwaySigns)
                    {
                        var airway = network.GetAirway(airwayEntry.AirwayId);
                        if (airway == null)
                            continue;

                        var a = network.GetNode(airway.StartNodeId);
                        var b = network.GetNode(airway.EndNodeId);

                        if (a == null || b == null)
                            continue;

                        var point = airwayEntry.Sign > 0 ? b.Position : a.Position;
                        points.Add(point);
                    }

                    if (points.Count > 0)
                    {
                        // Close the loop visually if it is not already closed
                        if ((points[0] - points[points.Count - 1]).sqrMagnitude > 0.0001f)
                            points.Add(points[0]);

                        line.positionCount = points.Count;
                        line.SetPositions(points.ToArray());
                    }
                }
            }
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}