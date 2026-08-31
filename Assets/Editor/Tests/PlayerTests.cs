using NUnit.Framework;
using UnityEngine;
using MineVent.Domain;
using MineVent.Network;
using MineVent.Solvers;

public class PlayerTests
{
    [Test]
    public void MineNetwork_RegistersConnectedAirways()
    {
        var network = new MineNetwork();

        var node1 = new Node(1, Vector3.zero);
        var node2 = new Node(2, new Vector3(1, 0, 0));
        var node3 = new Node(3, new Vector3(1, 0, 1));

        network.AddNode(node1);
        network.AddNode(node2);
        network.AddNode(node3);

        var airwayA = new Airway(101, 1, 2, 2.0f);
        var airwayB = new Airway(102, 2, 3, 2.5f);

        network.AddAirway(airwayA);
        network.AddAirway(airwayB);

        Assert.That(node1.ConnectedAirwayIds, Does.Contain(101));
        Assert.That(node2.ConnectedAirwayIds, Does.Contain(101));
        Assert.That(node2.ConnectedAirwayIds, Does.Contain(102));
        Assert.That(node3.ConnectedAirwayIds, Does.Contain(102));
    }

    [Test]
    public void HardyCrossSolver_ConvergesOnSimpleClosedLoop()
    {
        var network = new MineNetwork();

        var n1 = new Node(1, Vector3.zero);
        var n2 = new Node(2, new Vector3(1, 0, 0));
        var n3 = new Node(3, new Vector3(1, 0, 1));
        var n4 = new Node(4, new Vector3(0, 0, 1));

        network.AddNode(n1);
        network.AddNode(n2);
        network.AddNode(n3);
        network.AddNode(n4);

        // Square loop: 1 -> 2 -> 3 -> 4 -> 1
        var a1 = new Airway(1, 1, 2, 1.0f, 1.0f);
        var a2 = new Airway(2, 2, 3, 1.0f, 1.0f);
        var a3 = new Airway(3, 3, 4, 1.0f, 1.0f);
        var a4 = new Airway(4, 4, 1, 1.0f, 1.0f);

        network.AddAirway(a1);
        network.AddAirway(a2);
        network.AddAirway(a3);
        network.AddAirway(a4);

        // Loop traversal direction: 1->2->3->4->1
        network.AddLoop(new Loop(1, new System.Collections.Generic.List<(int AirwayId, int Sign)>
        {
            (1, 1),
            (2, 1),
            (3, 1),
            (4, 1)
        }));

        var solver = new HardyCrossSolver();
        var result = solver.Solve(network, tolerance: 0.0001f, maxIterations: 100);

        Assert.That(result.Converged, Is.True, "The loop should converge.");
        Assert.That(Mathf.Abs(network.GetAirway(1).Q), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(network.GetAirway(2).Q), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(network.GetAirway(3).Q), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(network.GetAirway(4).Q), Is.LessThan(0.001f));
    }

    [Test]
    public void NodeContinuityValidator_PassesForBalancedLoop()
    {
        var network = new MineNetwork();

        var n1 = new Node(1, Vector3.zero);
        var n2 = new Node(2, new Vector3(1, 0, 0));
        var n3 = new Node(3, new Vector3(1, 0, 1));
        var n4 = new Node(4, new Vector3(0, 0, 1));

        network.AddNode(n1);
        network.AddNode(n2);
        network.AddNode(n3);
        network.AddNode(n4);

        var a1 = new Airway(1, 1, 2, 1.0f, 1.0f);
        var a2 = new Airway(2, 2, 3, 1.0f, 1.0f);
        var a3 = new Airway(3, 3, 4, 1.0f, 1.0f);
        var a4 = new Airway(4, 4, 1, 1.0f, 1.0f);

        network.AddAirway(a1);
        network.AddAirway(a2);
        network.AddAirway(a3);
        network.AddAirway(a4);

        var validator = new NodeContinuityValidator();
        var errors = validator.Validate(network);

        Assert.That(errors, Is.Empty);
    }
}