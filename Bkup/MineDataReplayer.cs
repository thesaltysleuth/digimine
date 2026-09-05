using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public struct TelemetryFrame
{
    public string timestamp;
    public float AN311;
    public float AN422;
    public float AN423;
    public float MM252;
    public float MM261;
    public float MM262;
    public float MM263;
    public float MM264;
    public float MM256;
    public float MM211;
    public float AMP1_IR;
    public float AMP2_IR;
    public float F_SIDE;
    public float V;
}

public class MineDataReplayer : MonoBehaviour
{
    [Header("Data Source")]
    public string relativeCsvPath = "Assets/data/methane_data.csv";
    public int maxLoadedRows = 5000;

    [Header("Playback State")]
    public bool isPlaying = true;
    [Range(0.1f, 50f)]
    public float playbackSpeed = 5.0f; // 5x speed for hackathon demo
    public int currentFrameIndex = 0;
    public TelemetryFrame currentFrame;

    [Header("Scene Mappings")]
    public GraphManager graphManager;
    public ShearerController shearerController;

    private List<TelemetryFrame> loadedFrames = new List<TelemetryFrame>();
    private float timer = 0f;
    private Dictionary<string, int> columnIndexMap = new Dictionary<string, int>();

    private void Start()
    {
        if (graphManager == null) graphManager = FindAnyObjectByType<GraphManager>();
        if (shearerController == null) shearerController = FindAnyObjectByType<ShearerController>();

        StartCoroutine(LoadCsvDataRoutine());
    }

    private IEnumerator LoadCsvDataRoutine()
    {
        string fullPath = Path.Combine(Application.dataPath, "..", relativeCsvPath);
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Application.dataPath, "data/methane_data.csv");
        }

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[MineDataReplayer] CSV file not found at: {fullPath}. Creating fallback synthetic frames.");
            GenerateFallbackFrames();
            yield break;
        }

        using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader reader = new StreamReader(fs))
        {
            string headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine)) yield break;

            string[] headers = headerLine.Replace("\"", "").Split(',');
            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexMap[headers[i].Trim()] = i;
            }

            int count = 0;
            while (!reader.EndOfStream && count < maxLoadedRows)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;

                string[] tokens = line.Split(',');
                if (tokens.Length < headers.Length) continue;

                TelemetryFrame frame = ParseFrame(tokens);
                loadedFrames.Add(frame);
                count++;

                // Yield to keep UI frame rates smooth while loading initial chunk
                if (count % 1000 == 0) yield return null;
            }
        }

        Debug.Log($"[MineDataReplayer] Successfully loaded {loadedFrames.Count} real telemetry frames from CSV.");
    }

    private TelemetryFrame ParseFrame(string[] t)
    {
        TelemetryFrame f = new TelemetryFrame();
        f.timestamp = $"{GetVal(t, "hour"):00}:{GetVal(t, "minute"):00}:{GetVal(t, "second"):00}";
        f.AN311 = GetVal(t, "AN311");
        f.AN422 = GetVal(t, "AN422");
        f.AN423 = GetVal(t, "AN423");
        f.MM252 = GetVal(t, "MM252");
        f.MM261 = GetVal(t, "MM261");
        f.MM262 = GetVal(t, "MM262");
        f.MM263 = GetVal(t, "MM263");
        f.MM264 = GetVal(t, "MM264");
        f.MM256 = GetVal(t, "MM256");
        f.MM211 = GetVal(t, "MM211");
        f.AMP1_IR = GetVal(t, "AMP1_IR");
        f.AMP2_IR = GetVal(t, "AMP2_IR");
        f.F_SIDE = GetVal(t, "F_SIDE");
        f.V = GetVal(t, "V");
        return f;
    }

    private float GetVal(string[] tokens, string colName)
    {
        if (columnIndexMap.TryGetValue(colName, out int idx) && idx < tokens.Length)
        {
            if (float.TryParse(tokens[idx], out float val)) return val;
        }
        return 0f;
    }

    private void GenerateFallbackFrames()
    {
        for (int i = 0; i < 500; i++)
        {
            TelemetryFrame f = new TelemetryFrame();
            f.timestamp = TimeSpan.FromSeconds(i).ToString(@"hh\:mm\:ss");
            f.MM263 = 0.2f + Mathf.PingPong(i * 0.05f, 1.8f); // Oscillates into Warning & Alarm
            f.MM264 = 0.3f + Mathf.PingPong(i * 0.03f, 1.2f);
            f.MM256 = 0.15f + Mathf.Sin(i * 0.1f) * 0.4f;
            f.AN422 = 2.5f + Mathf.Sin(i * 0.2f) * 0.5f;
            f.AMP1_IR = 25f + Mathf.Sin(i * 0.5f) * 15f;
            f.V = 45f;
            f.F_SIDE = (i % 100 < 50) ? 1f : 0f;
            loadedFrames.Add(f);
        }
    }

    private void Update()
    {
        if (!isPlaying || loadedFrames == null || loadedFrames.Count == 0) return;

        timer += Time.deltaTime * playbackSpeed;
        if (timer >= 1.0f)
        {
            int steps = Mathf.FloorToInt(timer);
            timer -= steps;
            currentFrameIndex = (currentFrameIndex + steps) % loadedFrames.Count;
            ApplyFrame(loadedFrames[currentFrameIndex]);
        }
    }

    private void ApplyFrame(TelemetryFrame f)
    {
        currentFrame = f;

        // Apply sensor telemetry to mapped graph nodes
        if (graphManager != null && graphManager.allNodes != null)
        {
            foreach (Node n in graphManager.allNodes)
            {
                if (n == null) continue;
                if (n.nodeID.Contains("3") || n.sensorID == "MM263") n.methaneConcentration = f.MM263;
                else if (n.nodeID.Contains("4") || n.sensorID == "MM264") n.methaneConcentration = f.MM264;
                else if (n.nodeID.Contains("6") || n.sensorID == "MM256") n.methaneConcentration = f.MM256;
                else if (n.nodeID.Contains("2") || n.sensorID == "MM262") n.methaneConcentration = f.MM262;
                else n.methaneConcentration = (f.MM263 + f.MM264) * 0.5f;
            }
        }

        // Apply telemetry to shearer machine
        if (shearerController != null)
        {
            shearerController.UpdateTelemetry(f.V, f.F_SIDE, f.AMP1_IR, f.AMP2_IR);
        }
    }

    public void TogglePlayPause()
    {
        isPlaying = !isPlaying;
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, 0.1f, 50f);
    }
}
