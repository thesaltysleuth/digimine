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
    private Material staticBlockedMaterial;
    private Material leakGlowMaterial;

    private void Awake()
    {
        solver = GetComponent<Solver>();
        if (GetComponent<LeakPlacementManager>() == null)
        {
            gameObject.AddComponent<LeakPlacementManager>();
        }
        if (GetComponent<DebrisPlacementManager>() == null)
        {
            gameObject.AddComponent<DebrisPlacementManager>();
        }
        if (GetComponent<NavigationPlacementManager>() == null)
        {
            gameObject.AddComponent<NavigationPlacementManager>();
        }
        if (GetComponent<AirflowPlacementManager>() == null)
        {
            gameObject.AddComponent<AirflowPlacementManager>();
        }
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

    private void EnsureStaticBlockedMaterial()
    {
        if (staticBlockedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            staticBlockedMaterial = new Material(shader);
            staticBlockedMaterial.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            if (staticBlockedMaterial.HasProperty("_BaseColor"))
            {
                staticBlockedMaterial.SetColor("_BaseColor", staticBlockedMaterial.color);
            }
        }
    }

    private void EnsureLeakGlowMaterial()
    {
        if (leakGlowMaterial == null)
        {
            leakGlowMaterial = new Material(Shader.Find("Sprites/Default"));
            leakGlowMaterial.color = new Color(1f, 0.5f, 0f, 0.35f);
        }
    }

    private void DestroyLeakGlow(Edge edge)
    {
        if (edge.leakGlowRenderer != null)
        {
            if (Application.isPlaying)
                Destroy(edge.leakGlowRenderer.gameObject);
            else
                DestroyImmediate(edge.leakGlowRenderer.gameObject);
            edge.leakGlowRenderer = null;
        }
    }

    private void SetupEdgeRenderers()
    {
        EnsureStaticBlockedMaterial();
        EnsureLeakGlowMaterial();

        foreach (var edge in edges)
        {
            if (edge.nodeA == null || edge.nodeB == null) continue;

            if (!edge.isBlocked)
            {
                if (edge.downstreamLineRenderer != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(edge.downstreamLineRenderer.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(edge.downstreamLineRenderer.gameObject);
                    }
                    edge.downstreamLineRenderer = null;
                }

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

                bool hasFlow = edge.calculatedFlowRate > 0.0001f && edge.flowDirection != FlowDir.Static;

                if (hasFlow)
                {
                    // Determine target texture based on methane levels
                    Node upstreamNode = edge.flowDirection == FlowDir.A_To_B ? edge.nodeA : edge.nodeB;
                    Node downstreamNode = edge.flowDirection == FlowDir.A_To_B ? edge.nodeB : edge.nodeA;
                    //Debug.Log($"[GraphManager] Edge {edge.nodeA.name} -> {edge.nodeB.name}, FlowDir: {edge.flowDirection}, Upstream Node: {upstreamNode.name}, CH4: {upstreamNode.ch4}");
                    float upstreamCh4 = upstreamNode.ch4;
                    Texture targetTex = greenArrow;
                    bool isleak = false;
                    if (downstreamNode.methaneGenerationRate > 0f)
                    {
                        isleak = true;
                    }
                    edge.isLeak = isleak;
                    if (upstreamCh4 > 1.25f) targetTex = redArrow;
                    else if (upstreamCh4 >= 0.75f) targetTex = yellowArrow;

                    if (lr.sharedMaterial == null || lr.sharedMaterial == staticBlockedMaterial)
                    {
                        lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    }

                    lr.sharedMaterial.mainTexture = targetTex;

                    // Scale UV tiling based on length and master spacing
                    float dist = Vector3.Distance(edge.nodeA.transform.position, edge.nodeB.transform.position);
                    float tilingX = dist / Mathf.Max(0.1f, arrowSpacing);

                    // Flip UV scale visually if direction flows from B to A
                    if (edge.flowDirection == FlowDir.B_To_A)
                    {
                        tilingX *= -1f;
                    }

                    lr.sharedMaterial.mainTextureScale = new Vector2(tilingX, 1);

                    // Create or destroy leak glow overlay
                    if (isleak)
                    {
                        if (edge.leakGlowRenderer == null)
                        {
                            GameObject glowObj = new GameObject($"LeakGlow_{edge.nodeA.name}_{edge.nodeB.name}");
                            glowObj.transform.SetParent(transform);
                            edge.leakGlowRenderer = glowObj.AddComponent<LineRenderer>();
                            edge.leakGlowRenderer.sharedMaterial = leakGlowMaterial;
                            edge.leakGlowRenderer.numCapVertices = 4;
                        }
                        edge.leakGlowRenderer.enabled = showEdges;
                        edge.leakGlowRenderer.startWidth = lineThickness * 2.5f;
                        edge.leakGlowRenderer.endWidth = lineThickness * 2.5f;
                        edge.leakGlowRenderer.positionCount = 2;
                        edge.leakGlowRenderer.SetPosition(0, edge.nodeA.transform.position);
                        edge.leakGlowRenderer.SetPosition(1, edge.nodeB.transform.position);
                        edge.leakGlowRenderer.sortingOrder = -1;
                    }
                    else
                    {
                        DestroyLeakGlow(edge);
                    }
                }
                else
                {
                    // 0 airflow: hide arrows, show static grey line
                    edge.isLeak = false;
                    DestroyLeakGlow(edge);
                    lr.sharedMaterial = staticBlockedMaterial;
                }
            }
            else
            {
                // Determine upstream node and downstream node based on pressure delta
                Node upstreamNode = (edge.nodeA.pressure >= edge.nodeB.pressure) ? edge.nodeA : edge.nodeB;
                Node downstreamNode = (upstreamNode == edge.nodeA) ? edge.nodeB : edge.nodeA;

                // Upstream LineRenderer (shows arrows up to debris position ONLY if there's airflow)
                if (edge.lineRenderer == null)
                {
                    GameObject edgeObj = new GameObject($"Edge_{edge.nodeA.name}_{edge.nodeB.name}");
                    edgeObj.transform.SetParent(transform);
                    edge.lineRenderer = edgeObj.AddComponent<LineRenderer>();
                }

                LineRenderer upstreamLr = edge.lineRenderer;
                upstreamLr.enabled = showEdges;
                upstreamLr.startWidth = lineThickness;
                upstreamLr.endWidth = lineThickness;
                upstreamLr.positionCount = 2;
                upstreamLr.SetPosition(0, upstreamNode.transform.position);
                upstreamLr.SetPosition(1, edge.debrisWorldPosition);

                bool hasUpstreamFlow = edge.calculatedFlowRate > 0.0001f && edge.flowDirection != FlowDir.Static;

                if (hasUpstreamFlow)
                {
                    float maxCh4 = Mathf.Max(edge.nodeA.ch4, edge.nodeB.ch4);
                    Texture targetTex = greenArrow;
                    if (maxCh4 > 1.25f) targetTex = redArrow;
                    else if (maxCh4 >= 0.75f) targetTex = yellowArrow;

                    if (upstreamLr.sharedMaterial == null || upstreamLr.sharedMaterial == staticBlockedMaterial)
                    {
                        upstreamLr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    }

                    upstreamLr.sharedMaterial.mainTexture = targetTex;

                    float upstreamDist = Vector3.Distance(upstreamNode.transform.position, edge.debrisWorldPosition);
                    float upstreamTiling = upstreamDist / Mathf.Max(0.1f, arrowSpacing);

                    if (upstreamNode == edge.nodeB)
                    {
                        upstreamTiling *= -1f;
                    }

                    upstreamLr.sharedMaterial.mainTextureScale = new Vector2(upstreamTiling, 1);
                }
                else
                {
                    upstreamLr.sharedMaterial = staticBlockedMaterial;
                }

                // Downstream LineRenderer (static line with NO arrows)
                if (edge.downstreamLineRenderer == null)
                {
                    GameObject downstreamObj = new GameObject($"Edge_{edge.nodeA.name}_{edge.nodeB.name}_Downstream");
                    downstreamObj.transform.SetParent(transform);
                    edge.downstreamLineRenderer = downstreamObj.AddComponent<LineRenderer>();
                }

                LineRenderer downstreamLr = edge.downstreamLineRenderer;
                downstreamLr.enabled = showEdges;
                downstreamLr.startWidth = lineThickness;
                downstreamLr.endWidth = lineThickness;
                downstreamLr.positionCount = 2;
                downstreamLr.SetPosition(0, edge.debrisWorldPosition);
                downstreamLr.SetPosition(1, downstreamNode.transform.position);

                downstreamLr.sharedMaterial = staticBlockedMaterial;
            }
        }
    }

    private void AnimateArrows()
    {
        if (!Application.isPlaying) return;

        foreach (var edge in edges)
        {
            if (edge.lineRenderer == null || !edge.lineRenderer.enabled || edge.lineRenderer.sharedMaterial == null) continue;
            if (edge.lineRenderer.sharedMaterial == staticBlockedMaterial) continue;

            // Speed driven by flow rate
            float speed = edge.calculatedFlowRate * 0.1f;

            if (edge.flowDirection == FlowDir.Static)
            {
                speed = 0f;
            }

            Vector2 currentOffset = edge.lineRenderer.sharedMaterial.mainTextureOffset;
            currentOffset.x -= speed * Time.deltaTime;
            edge.lineRenderer.sharedMaterial.mainTextureOffset = currentOffset;

            // Faint orange pulse overlay for leak edges
            if (edge.isLeak && edge.leakGlowRenderer != null)
            {
                float t = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f; // 0..1 oscillation
                float alpha = Mathf.Lerp(0.08f, 0.4f, t);
                Color glowColor = new Color(1f, 0.5f, 0f, alpha);
                edge.leakGlowRenderer.startColor = glowColor;
                edge.leakGlowRenderer.endColor = glowColor;
            }
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
                if (edge.lineRenderer != null && edge.lineRenderer.sharedMaterial != null && edge.lineRenderer.sharedMaterial != staticBlockedMaterial)
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