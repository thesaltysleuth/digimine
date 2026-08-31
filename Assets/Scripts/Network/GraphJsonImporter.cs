using System;
using System.Collections.Generic;
using MineVent.Domain;
using UnityEngine;

namespace MineVent.Network
{
    public static class GraphJsonImporter
    {
        [Serializable]
        private class GraphData
        {
            public NodeData[] nodes;
            public AirwayData[] airways;
            public LoopData[] loops;
        }

        [Serializable]
        private class NodeData
        {
            public int id;
            public Vector3 position;
            public bool isBoundary;
            public float fixedPressure;
        }

        [Serializable]
        private class AirwayData
        {
            public int id;
            public int startNodeId;
            public int endNodeId;
            public float resistance;
            public float initialQ;
            public float fanPressure;
        }

        [Serializable]
        private class LoopData
        {
            public int id;
            public AirwaySignData[] airwaySigns;
        }

        [Serializable]
        private class AirwaySignData
        {
            public int airwayId;
            public int sign;
        }

        public static MineNetwork Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            GraphData data;

            try
            {
                data = JsonUtility.FromJson<GraphData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("The graph JSON is invalid.", exception);
            }

            if (data == null)
                throw new FormatException("The graph JSON is invalid.");

            var network = new MineNetwork();
            var nodeIds = new HashSet<int>();
            var airwayIds = new HashSet<int>();
            var loopIds = new HashSet<int>();

            foreach (NodeData node in data.nodes ?? Array.Empty<NodeData>())
            {
                if (!nodeIds.Add(node.id))
                    throw new FormatException($"Duplicate node ID: {node.id}");

                network.AddNode(new Node(
                    node.id,
                    node.position,
                    node.isBoundary,
                    node.fixedPressure));
            }

            foreach (AirwayData airway in data.airways ?? Array.Empty<AirwayData>())
            {
                if (!airwayIds.Add(airway.id))
                    throw new FormatException($"Duplicate airway ID: {airway.id}");

                if (!nodeIds.Contains(airway.startNodeId) ||
                    !nodeIds.Contains(airway.endNodeId))
                {
                    throw new FormatException(
                        $"Airway {airway.id} references a missing node.");
                }

                network.AddAirway(new Airway(
                    airway.id,
                    airway.startNodeId,
                    airway.endNodeId,
                    airway.resistance,
                    airway.initialQ,
                    airway.fanPressure));
            }

            foreach (LoopData loop in data.loops ?? Array.Empty<LoopData>())
            {
                if (!loopIds.Add(loop.id))
                    throw new FormatException($"Duplicate loop ID: {loop.id}");

                var signs = new List<(int AirwayId, int Sign)>();

                foreach (AirwaySignData entry in loop.airwaySigns ?? Array.Empty<AirwaySignData>())
                {
                    if (!airwayIds.Contains(entry.airwayId))
                        throw new FormatException(
                            $"Loop {loop.id} references a missing airway.");

                    if (entry.sign != -1 && entry.sign != 1)
                        throw new FormatException(
                            $"Loop {loop.id} has an invalid airway sign.");

                    signs.Add((entry.airwayId, entry.sign));
                }

                network.AddLoop(new Loop(loop.id, signs));
            }

            return network;
        }
    }
}