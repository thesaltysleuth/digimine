using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates and manages a sleek, high-tech dark-mode HUD dashboard Canvas
/// for the Digimine hackathon demo, displaying live telemetry, scenario controls,
/// and gas alarm alerts.
/// </summary>
public class MineDashboardUI : MonoBehaviour
{
    [Header("Script References")]
    public ScenarioManager scenarioManager;
    public MineDataReplayer dataReplayer;

    private Canvas mainCanvas;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI telemetryText;
    private TextMeshProUGUI alertBannerText;
    private Image alertBannerBg;

    private void Start()
    {
        if (scenarioManager == null) scenarioManager = FindAnyObjectByType<ScenarioManager>();
        if (dataReplayer == null) dataReplayer = FindAnyObjectByType<MineDataReplayer>();

        CreateDashboardUI();
    }

    private void CreateDashboardUI()
    {
        // 1. Create Canvas
        GameObject canvasObj = new GameObject("DigimineDashboardCanvas");
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Header Panel
        GameObject headerPanel = CreatePanel(canvasObj.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -35), new Vector2(0, 70), new Color(0.05f, 0.08f, 0.12f, 0.9f));
        CreateText(headerPanel.transform, "DIGIMINE | Mine Ventilation & IoT Gas Simulation Digital Twin", 20, TextAlignmentOptions.Left, new Color(0.0f, 0.9f, 1.0f));

        // 3. Scenario Control Panel (Top Right)
        GameObject scenarioPanel = CreatePanel(canvasObj.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1f, 1f), new Vector2(-20, -90), new Vector2(260, 240), new Color(0.08f, 0.12f, 0.18f, 0.85f));
        CreateText(scenarioPanel.transform, "WHAT-IF SCENARIOS", 14, TextAlignmentOptions.Center, Color.yellow, new Vector2(0, 100));

        CreateButton(scenarioPanel.transform, "🚨 Methane Leak", new Vector2(0, 60), () => scenarioManager?.TriggerMethaneLeakScenario(), new Color(0.8f, 0.2f, 0.1f));
        CreateButton(scenarioPanel.transform, "🛑 Fan Failure", new Vector2(0, 20), () => scenarioManager?.TriggerFanFailureScenario(), new Color(0.8f, 0.5f, 0.1f));
        CreateButton(scenarioPanel.transform, "🪨 Debris Blockage", new Vector2(0, -20), () => scenarioManager?.TriggerDebrisBlockageScenario(), new Color(0.5f, 0.5f, 0.6f));
        CreateButton(scenarioPanel.transform, "🚪 Door Open", new Vector2(0, -60), () => scenarioManager?.TriggerDoorShortCircuitScenario(), new Color(0.2f, 0.6f, 0.8f));
        CreateButton(scenarioPanel.transform, "🔄 Reset Normal", new Vector2(0, -100), () => scenarioManager?.ResetToNormalOperation(), new Color(0.1f, 0.7f, 0.3f));

        // 4. Telemetry TelePrompter Card (Bottom Left)
        GameObject telemetryCard = CreatePanel(canvasObj.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0f, 0f), new Vector2(20, 20), new Vector2(340, 180), new Color(0.06f, 0.1f, 0.15f, 0.9f));
        telemetryText = CreateText(telemetryCard.transform, "Telemetry Data Stream...", 13, TextAlignmentOptions.TopLeft, Color.white, Vector2.zero);

        // 5. Alert Banner (Top Center)
        GameObject bannerObj = CreatePanel(canvasObj.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(500, 45), new Color(0.1f, 0.15f, 0.2f, 0.95f));
        alertBannerBg = bannerObj.GetComponent<Image>();
        alertBannerText = CreateText(bannerObj.transform, "SYSTEM STATUS: NORMAL VENTILATION", 16, TextAlignmentOptions.Center, new Color(0f, 1f, 0.5f));
    }

    private void Update()
    {
        if (dataReplayer != null && telemetryText != null)
        {
            TelemetryFrame f = dataReplayer.currentFrame;
            telemetryText.text = $"<b>IoT TELEMETRY STREAM [{f.timestamp}]</b>\n" +
                                 $"• MM263 (Target Sensor 1): <color=#00E676>{f.MM263:F2}% CH4</color>\n" +
                                 $"• MM264 (Target Sensor 2): <color=#00E676>{f.MM264:F2}% CH4</color>\n" +
                                 $"• MM256 (Target Sensor 3): <color=#00E676>{f.MM256:F2}% CH4</color>\n" +
                                 $"• AN422 (Anemometer): {f.AN422:F1} m/s\n" +
                                 $"• Shearer Speed V: {f.V:F0} Hz | Current: {f.AMP1_IR:F0} A";

            // Update Alert Banner based on gas thresholds
            float maxGas = Mathf.Max(f.MM263, f.MM264, f.MM256);
            if (scenarioManager != null && scenarioManager.currentScenario == WhatIfScenario.MethaneLeak)
            {
                maxGas = 4.2f;
            }

            if (maxGas >= 1.5f)
            {
                alertBannerText.text = $"⚠️ ALARM: HIGH METHANE ({maxGas:F2}% CH4) - EVACUATE SEAM!";
                alertBannerText.color = Color.white;
                if (alertBannerBg != null) alertBannerBg.color = new Color(0.9f, 0.15f, 0.1f, 0.95f);
            }
            else if (maxGas >= 1.0f)
            {
                alertBannerText.text = $"⚠️ WARNING: METHANE ELEVATED ({maxGas:F2}% CH4)";
                alertBannerText.color = Color.black;
                if (alertBannerBg != null) alertBannerBg.color = new Color(1.0f, 0.85f, 0.1f, 0.95f);
            }
            else
            {
                alertBannerText.text = "SYSTEM STATUS: NORMAL VENTILATION";
                alertBannerText.color = new Color(0f, 1f, 0.5f);
                if (alertBannerBg != null) alertBannerBg.color = new Color(0.06f, 0.12f, 0.18f, 0.95f);
            }
        }
    }

    private GameObject CreatePanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        GameObject panel = new GameObject("UIPanel");
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = color;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, TextAlignmentOptions align, Color color, Vector2 pos = default)
    {
        GameObject textObj = new GameObject("UIText");
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 10);
        rt.offsetMax = new Vector2(-10, -10);
        rt.anchoredPosition = pos;
        return text;
    }

    private GameObject CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject btnObj = new GameObject("UIButton_" + label);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = color;
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(220, 32);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 13;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        return btnObj;
    }
}
