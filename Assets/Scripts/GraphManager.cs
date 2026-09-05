using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Solver))]
public class GraphManager : MonoBehaviour
{
    [Header("Master Display Controls")]
    public float lineThickness = 1.5f;   // Controls the width of the arrow line
    public float arrowSpacing = 5.0f;    // World units per arrow repeat
    public bool showEdges = true;

    [Header("Arrow Textures")]
    public Texture greenArrow;
    public Texture yellowArrow;
    public Texture redArrow;

    [Header("Network Structure")]
    public List<Node> nodes = new List<Node>();
    public List<Edge> edges = new List<Edge>();

    private Solver solver;

    private void Awake()
    {
        solver = GetComponent<Solver>();
    }

    private void Start()
    {
        RunSolverAndRender();
    }

    private void Update()
    {
        AnimateArrows();
    }

    [ContextMenu("Run Solver & Update View")]
    public void RunSolverAndRender()
    {
        if (solver == null) solver = GetComponent<Solver>();

        // 1. Calculate pressures & concentrations
        solver.SolveNetwork(nodes, edges);

        // 2. Update node 3D world labels with new calculated values
        foreach (var node in nodes)
        {
            if (node != null)
            {
                node.UpdateLabel();
            }
        }

        // 3. Setup or update LineRenderers
        SetupEdgeRenderers();
    }

    private void SetupEdgeRenderers()
    {
        foreach (var edge in edges)
        {
            if (edge.nodeA == null || edge.nodeB == null) continue;

            if (edge.lineRenderer == null)
            {
                GameObject edgeObj = new GameObject($"Edge_{edge.nodeA.name}_{edge.nodeB.name}");
                edgeObj.transform.SetParent(transform);
                edge.lineRenderer = edgeObj.AddComponent<LineRenderer>();
            }

            LineRenderer lr = edge.lineRenderer;
            lr.enabled = showEdges;
            lr.startWidth = lineThickness;
            lr.endWidth = lineThickness;
            lr.positionCount = 2;

            // Set endpoints
            lr.SetPosition(0, edge.nodeA.transform.position);
            lr.SetPosition(1, edge.nodeB.transform.position);

            // Determine target texture based on methane levels
            float maxCh4 = Mathf.Max(edge.nodeA.ch4, edge.nodeB.ch4);
            Texture targetTex = greenArrow;
            if (maxCh4 > 1.25f) targetTex = redArrow;
            else if (maxCh4 >= 0.75f) targetTex = yellowArrow;

            if (lr.sharedMaterial == null)
            {
                lr.material = new Material(Shader.Find("Unlit/Transparent"));
            }

            lr.material.mainTexture = targetTex;

            // Scale UV tiling based on length and master spacing
            float dist = Vector3.Distance(edge.nodeA.transform.position, edge.nodeB.transform.position);
            float tilingX = dist / Mathf.Max(0.1f, arrowSpacing);

            // Flip UV scale visually if direction flows from B to A
            if (edge.flowDirection == FlowDir.B_To_A)
            {
                tilingX *= -1f;
            }

            lr.material.mainTextureScale = new Vector2(tilingX, 1);
        }
    }

    private void AnimateArrows()
    {
        foreach (var edge in edges)
        {
            if (edge.lineRenderer == null || !edge.lineRenderer.enabled) continue;

            // Speed driven by flow rate
            float speed = edge.calculatedFlowRate * 0.1f;

            if (edge.flowDirection == FlowDir.Static)
            {
                speed = 0f;
            }

            Vector2 currentOffset = edge.lineRenderer.material.mainTextureOffset;
            // Always move offset in one direction relative to the flipped UV mapping
            currentOffset.x -= speed * Time.deltaTime;
            edge.lineRenderer.material.mainTextureOffset = currentOffset;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RunSolverAndRender();
        }
        else
        {
            foreach (var edge in edges)
            {
                if (edge.lineRenderer != null)
                {
                    edge.lineRenderer.enabled = showEdges;
                    edge.lineRenderer.startWidth = lineThickness;
                    edge.lineRenderer.endWidth = lineThickness;

                    float dist = Vector3.Distance(edge.nodeA.transform.position, edge.nodeB.transform.position);
                    edge.lineRenderer.sharedMaterial.mainTextureScale = new Vector2(dist / Mathf.Max(0.1f, arrowSpacing), 1);
                }
            }
        }
    }
}