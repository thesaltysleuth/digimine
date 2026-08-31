using NUnit.Framework;
using UnityEngine;
using MineVent.Network;

public class GraphJsonImporterTests
{
    [Test]
    public void Import_ConvertsJsonToMineNetwork()
    {
        const string json = @"
        {
            ""nodes"": [
                {
                    ""id"": 1,
                    ""position"": { ""x"": 10, ""y"": 20, ""z"": 30 },
                    ""isBoundary"": true,
                    ""fixedPressure"": 125.5
                },
                {
                    ""id"": 2,
                    ""position"": { ""x"": 40, ""y"": 50, ""z"": 60 },
                    ""isBoundary"": false,
                    ""fixedPressure"": 0
                }
            ],
            ""airways"": [
                {
                    ""id"": 101,
                    ""startNodeId"": 1,
                    ""endNodeId"": 2,
                    ""resistance"": 2.5,
                    ""initialQ"": 4.25,
                    ""fanPressure"": 12
                }
            ],
            ""loops"": [
                {
                    ""id"": 201,
                    ""airwaySigns"": [
                        { ""airwayId"": 101, ""sign"": 1 }
                    ]
                }
            ]
        }";

        MineNetwork network = GraphJsonImporter.Import(json);

        Assert.That(network, Is.Not.Null);
        Assert.That(network.Nodes, Has.Count.EqualTo(2));
        Assert.That(network.Airways, Has.Count.EqualTo(1));
        Assert.That(network.Loops, Has.Count.EqualTo(1));

        var startNode = network.GetNode(1);
        Assert.That(startNode.Position, Is.EqualTo(new Vector3(10, 20, 30)));
        Assert.That(startNode.IsBoundary, Is.True);
        Assert.That(startNode.FixedPressure, Is.EqualTo(125.5f).Within(0.001f));
        Assert.Contains(101, startNode.ConnectedAirwayIds);

        var endNode = network.GetNode(2);
        Assert.That(endNode.Position, Is.EqualTo(new Vector3(40, 50, 60)));
        Assert.That(endNode.IsBoundary, Is.False);
        Assert.Contains(101, endNode.ConnectedAirwayIds);

        var airway = network.GetAirway(101);
        Assert.That(airway.StartNodeId, Is.EqualTo(1));
        Assert.That(airway.EndNodeId, Is.EqualTo(2));
        Assert.That(airway.R, Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(airway.Q, Is.EqualTo(4.25f).Within(0.001f));
        Assert.That(airway.FanPressure, Is.EqualTo(12f).Within(0.001f));

        var loop = network.Loops[0];
        Assert.That(loop.Id, Is.EqualTo(201));
        Assert.That(loop.AirwaySigns, Has.Count.EqualTo(1));
        Assert.That(loop.AirwaySigns[0].AirwayId, Is.EqualTo(101));
        Assert.That(loop.AirwaySigns[0].Sign, Is.EqualTo(1));
    }
}