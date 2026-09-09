using UnityEngine;

public class Debris : MonoBehaviour
{
    [Header("Blocked Edge")]
    public Edge targetEdge;
    public Vector3 worldPosition;

    private bool isInitialized = false;

    /// <summary>
    /// Binds this debris object to an edge, blocks airflow through it, and updates network visual state.
    /// </summary>
    public void Initialize(Edge edge, Vector3 pos)
    {
        targetEdge = edge;
        worldPosition = pos;

        if (targetEdge != null)
        {
            targetEdge.isBlocked = true;
            targetEdge.debrisInstance = gameObject;
            targetEdge.debrisWorldPosition = pos;
            isInitialized = true;

            // Recalculate network solver and update view
            GraphManager manager = FindFirstObjectByType<GraphManager>();
            if (manager != null)
            {
                manager.RunSolverAndRender();
            }
        }
    }

    private void OnDestroy()
    {
        if (isInitialized && targetEdge != null)
        {
            targetEdge.isBlocked = false;
            targetEdge.debrisInstance = null;

            if (targetEdge.downstreamLineRenderer != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(targetEdge.downstreamLineRenderer.gameObject);
                }
                else
                {
                    DestroyImmediate(targetEdge.downstreamLineRenderer.gameObject);
                }
                targetEdge.downstreamLineRenderer = null;
            }

            GraphManager manager = FindFirstObjectByType<GraphManager>();
            if (manager != null)
            {
                manager.RunSolverAndRender();
            }
        }
    }
}
