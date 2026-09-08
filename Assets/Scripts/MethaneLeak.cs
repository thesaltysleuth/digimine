using UnityEngine;

public class MethaneLeak : MonoBehaviour
{
    [Header("Gas Leak Properties")]
    public float emissionRate = 0.5f; // m³/s Methane gas generation rate

    [Header("Affected Node")]
    public Node targetNode;

    private bool isInitialized = false;

    /// <summary>
    /// Binds this methane leak to a specific node and updates the network.
    /// </summary>
    public void Initialize(Node node, float rate)
    {
        targetNode = node;
        emissionRate = rate;

        if (targetNode != null)
        {
            targetNode.methaneGenerationRate += emissionRate;
            isInitialized = true;

            // Recalculate network solver
            GraphManager manager = FindFirstObjectByType<GraphManager>();
            if (manager != null)
            {
                manager.RunSolverAndRender();
            }
        }
    }

    private void OnDestroy()
    {
        if (isInitialized && targetNode != null)
        {
            targetNode.methaneGenerationRate = Mathf.Max(0f, targetNode.methaneGenerationRate - emissionRate);

            GraphManager manager = FindFirstObjectByType<GraphManager>();
            if (manager != null)
            {
                manager.RunSolverAndRender();
            }
        }
    }
}
