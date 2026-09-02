using MineVent.Network;
using MineVent.Visualization;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    public TextAsset graphJson;

    private void Start()
    {
        var network = GraphJsonImporter.Import(graphJson.text);

        var visualizer = gameObject.AddComponent<MineNetworkVisualizer>();
        visualizer.Build(network);
    }
}