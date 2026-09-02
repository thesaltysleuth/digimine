using MineVent.Network;
using MineVent.Visualization;
using UnityEngine;

public class MineNetworkBootstrap : MonoBehaviour
{
    [SerializeField] private TextAsset graphJson;
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool createVisualizerIfMissing = true;

    private void Start()
    {
        if (buildOnStart)
        {
            BuildFromJson();
        }
    }

    [ContextMenu("Build Network")]
    public void BuildFromJson()
    {
        if (graphJson == null)
        {
            Debug.LogError("MineNetworkBootstrap: graphJson is not assigned.");
            return;
        }

        var network = GraphJsonImporter.Import(graphJson.text);

        MineNetworkVisualizer visualizer = GetComponent<MineNetworkVisualizer>();
        if (visualizer == null && createVisualizerIfMissing)
        {
            visualizer = gameObject.AddComponent<MineNetworkVisualizer>();
        }

        if (visualizer == null)
        {
            Debug.LogError("MineNetworkBootstrap: no MineNetworkVisualizer component found on this object.");
            return;
        }

        visualizer.Build(network);
    }
}