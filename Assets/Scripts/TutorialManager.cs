using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Main References")]
    [Tooltip("The root help/tutorial modal panel.")]
    public GameObject tutorialPanel;

    [Tooltip("The question mark button at the top left of the screen.")]
    public Button helpButton;

    [Tooltip("Close button inside the modal.")]
    public Button closeButton;

    [Tooltip("Optional backdrop click-catcher to dismiss modal.")]
    public Button backdropButton;

    [Header("Tabs")]
    public Button overviewTabBtn;
    public Button controlsTabBtn;
    public Button toolsTabBtn;

    public GameObject overviewContent;
    public GameObject controlsContent;
    public GameObject toolsContent;

    [Header("Tab Styling Colors")]
    public Color activeTabColor = new Color(0.2f, 0.45f, 0.85f, 1f);
    public Color inactiveTabColor = new Color(0.18f, 0.22f, 0.28f, 0.8f);
    public Color activeTextColor = Color.white;
    public Color inactiveTextColor = new Color(0.75f, 0.8f, 0.88f, 1f);

    private int currentTab = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (helpButton != null)
        {
            helpButton.onClick.RemoveListener(ToggleHelp);
            helpButton.onClick.AddListener(ToggleHelp);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseHelp);
            closeButton.onClick.AddListener(CloseHelp);
        }

        if (backdropButton != null)
        {
            backdropButton.onClick.RemoveListener(CloseHelp);
            backdropButton.onClick.AddListener(CloseHelp);
        }

        if (overviewTabBtn != null)
        {
            overviewTabBtn.onClick.RemoveListener(() => ShowTab(0));
            overviewTabBtn.onClick.AddListener(() => ShowTab(0));
        }

        if (controlsTabBtn != null)
        {
            controlsTabBtn.onClick.RemoveListener(() => ShowTab(1));
            controlsTabBtn.onClick.AddListener(() => ShowTab(1));
        }

        if (toolsTabBtn != null)
        {
            toolsTabBtn.onClick.RemoveListener(() => ShowTab(2));
            toolsTabBtn.onClick.AddListener(() => ShowTab(2));
        }

        // Default tab
        ShowTab(0);

        // Initially closed or open based on preference (start hidden so user clicks the ? icon)
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Press Escape to close tutorial if open
        if (Keyboard.current.escapeKey.wasPressedThisFrame && tutorialPanel != null && tutorialPanel.activeSelf)
        {
            CloseHelp();
        }

        // Press F1 or H to toggle help
        if (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.hKey.wasPressedThisFrame)
        {
            ToggleHelp();
        }
    }

    public void ToggleHelp()
    {
        if (tutorialPanel == null) return;
        bool newState = !tutorialPanel.activeSelf;
        tutorialPanel.SetActive(newState);
        if (newState)
        {
            ShowTab(currentTab);
        }
    }

    public void OpenHelp()
    {
        if (tutorialPanel == null) return;
        tutorialPanel.SetActive(true);
        ShowTab(currentTab);
    }

    public void CloseHelp()
    {
        if (tutorialPanel == null) return;
        tutorialPanel.SetActive(false);
    }

    public void ShowTab(int tabIndex)
    {
        currentTab = tabIndex;

        if (overviewContent != null) overviewContent.SetActive(tabIndex == 0);
        if (controlsContent != null) controlsContent.SetActive(tabIndex == 1);
        if (toolsContent != null) toolsContent.SetActive(tabIndex == 2);

        UpdateTabButtonStyle(overviewTabBtn, tabIndex == 0);
        UpdateTabButtonStyle(controlsTabBtn, tabIndex == 1);
        UpdateTabButtonStyle(toolsTabBtn, tabIndex == 2);
    }

    private void UpdateTabButtonStyle(Button btn, bool isActive)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isActive ? activeTabColor : inactiveTabColor;
        }

        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.color = isActive ? activeTextColor : inactiveTextColor;
            txt.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
