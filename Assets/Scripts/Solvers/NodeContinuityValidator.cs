using System;
using System.Collections.Generic;
using MineVent.Network;

namespace MineVent.Solvers
{
    public class NodeContinuityValidator
    {
        public List<string> Validate(MineNetwork network, float tolerance = 0.0001f)
        {
            var errors = new List<string>();

            foreach (var node in network.Nodes.Values)
            {
                if (node.IsBoundary)
                    continue;

                float netFlow = 0f;

                foreach (var airwayId in node.ConnectedAirwayIds)
                {
                    var airway = network.GetAirway(airwayId);
                    if (airway == null)
                        continue;

                    if (airway.StartNodeId == node.Id)
                        netFlow += airway.Q;
                    else if (airway.EndNodeId == node.Id)
                        netFlow -= airway.Q;
                }

                if (MathF.Abs(netFlow) > tolerance)
                {
                    errors.Add($"Node {node.Id} continuity error: {netFlow}");
                }
            }

            return errors;
        }
    }
}