using System.Collections.Generic;
using UnityEngine;

public enum WhatIfScenario
{
    NormalOperation,
    MethaneLeak,
    FanFailure,
    DebrisBlockage,
    DoorShortCircuit
}

public class ScenarioManager : MonoBehaviour
{
    [Header("Current Active Scenario")]
    public WhatIfScenario currentScenario = WhatIfScenario.NormalOperation;

    [Header("References")]
    public VentilationNetworkSolver networkSolver;
    public GraphManager graphManager;
    public MineDataReplayer dataReplayer;

    [Header("Scenario Target Nodes & Edges")]
    public Node targetLeakNode;
    public Node fanNode;
    public Node doorNodeA;
    public Node doorNodeB;

    private float originalFanPressure = 1500f;

    private void Start()
    {
        if (networkSolver == null) networkSolver = FindAnyObjectByType<VentilationNetworkSolver>();
        if (graphManager == null) graphManager = FindAnyObjectByType<GraphManager>();
        if (dataReplayer == null) dataReplayer = FindAnyObjectByType<MineDataReplayer>();

        FindDefaultNodes();
    }

    private void FindDefaultNodes()
    {
        if (graphManager == null || graphManager.allNodes == null) return;

        foreach (Node n in graphManager.allNodes)
        {
            if (n == null) continue;
            if (n.nodeID.Contains("1") && fanNode == null)
            {
                fanNode = n;
                fanNode.isFixedPressure = true;
                fanNode.pressure = originalFanPressure;
                fanNode.nodeType = NodeType.FanNode;
            }
            if (n.nodeID.Contains("3") && targetLeakNode == null)
            {
                targetLeakNode = n;
            }
            if (n.nodeID.Contains("5") && doorNodeA == null) doorNodeA = n;
            if (n.nodeID.Contains("8") && doorNodeB == null) doorNodeB = n;
        }
    }

    public void TriggerMethaneLeakScenario()
    {
        currentScenario = WhatIfScenario.MethaneLeak;
        if (targetLeakNode != null)
        {
            targetLeakNode.methaneConcentration = 4.2f; // Spikes to dangerous 4.2% CH4
        }
        Debug.Log("[ScenarioManager] 🚨 Scenario 1 Activated: Methane Leak at Node 3!");
    }

    public void TriggerFanFailureScenario()
    {
        currentScenario = WhatIfScenario.FanFailure;
        if (fanNode != null)
        {
            fanNode.pressure = 0f; // Fan stops spinning
        }
        if (networkSolver != null) networkSolver.SolveNetwork();
        Debug.Log("[ScenarioManager] 🛑 Scenario 2 Activated: Ventilation Main Fan Failure!");
    }

    public void TriggerDebrisBlockageScenario()
    {
        currentScenario = WhatIfScenario.DebrisBlockage;
        FlowEdge[] edges = FindObjectsByType<FlowEdge>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (edges.Length > 0)
        {
            edges[0].isBlocked = true; // Obstruction blocks tunnel
        }
        if (networkSolver != null) networkSolver.SolveNetwork();
        Debug.Log("[ScenarioManager] 🪨 Scenario 3 Activated: Tunnel Debris Blockage!");
    }

    public void TriggerDoorShortCircuitScenario()
    {
        currentScenario = WhatIfScenario.DoorShortCircuit;
        if (doorNodeA != null && doorNodeB != null)
        {
            doorNodeA.pressure = 1200f;
            doorNodeB.pressure = 100f; // Short circuit pressure drop
        }
        if (networkSolver != null) networkSolver.SolveNetwork();
        Debug.Log("[ScenarioManager] 🚪 Scenario 4 Activated: Ventilation Door Opened (Short-Circuit)!");
    }

    public void ResetToNormalOperation()
    {
        currentScenario = WhatIfScenario.NormalOperation;
        if (fanNode != null)
        {
            fanNode.pressure = originalFanPressure;
        }
        FlowEdge[] edges = FindObjectsByType<FlowEdge>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (FlowEdge edge in edges)
        {
            if (edge != null) edge.isBlocked = false;
        }
        if (networkSolver != null) networkSolver.SolveNetwork();
        Debug.Log("[ScenarioManager] ✅ Reset to Normal Digital Twin Operation.");
    }
}
