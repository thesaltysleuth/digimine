using System.Collections.Generic;
using MineVent.Domain;

namespace MineVent.Network
{
    public class MineNetwork
    {
        public Dictionary<int, Node> Nodes { get; private set; } = new Dictionary<int, Node>();
        public Dictionary<int, Airway> Airways { get; private set; } = new Dictionary<int, Airway>();
        public List<Loop> Loops { get; private set; } = new List<Loop>();

        public void AddNode(Node node)
        {
            if (!Nodes.ContainsKey(node.Id))
            {
                Nodes[node.Id] = node;
            }
        }

        public void AddAirway(Airway airway)
        {
            if (Airways.ContainsKey(airway.Id))
                return;

            Airways[airway.Id] = airway;

            if (Nodes.TryGetValue(airway.StartNodeId, out var startNode))
                startNode.RegisterAirway(airway.Id);

            if (Nodes.TryGetValue(airway.EndNodeId, out var endNode))
                endNode.RegisterAirway(airway.Id);
        }

        public void AddLoop(Loop loop)
        {
            Loops.Add(loop);
        }

        public Node GetNode(int id)
        {
            return Nodes.TryGetValue(id, out var node) ? node : null;
        }

        public Airway GetAirway(int id)
        {
            return Airways.TryGetValue(id, out var airway) ? airway : null;
        }
    }
}