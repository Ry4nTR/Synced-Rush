using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Provides a simple, scene‑agnostic loading screen that can be shown when changing scenes.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Loading UI")]
    [SerializeField] private CanvasGroup loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusLabel;

    private void Awake()
    {
        // Singleton enforcement
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Hide on boot so it doesn't obstruct the main menu
        Hide();
    }

    /// <summary>
    /// Show the loading overlay.  Progress bar starts at zero unless
    /// explicitly set via SetProgress().
    /// </summary>
    public void Show()
    {
        if (loadingPanel != null)
        {
            loadingPanel.alpha = 1f;
            loadingPanel.interactable = true;
            loadingPanel.blocksRaycasts = true;
        }
        if (progressBar != null)
            progressBar.value = 0f;
        if (statusLabel != null)
            statusLabel.text = string.Empty;
    }

    /// <summary>
    /// Hide the loading overlay.
    /// </summary>
    public void Hide()
    {
        if (loadingPanel != null)
        {
            loadingPanel.alpha = 0f;
            loadingPanel.interactable = false;
            loadingPanel.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Update the progress bar and optional status text.  Progress should be
    /// normalized between 0 and 1.
    /// </summary>
    public void SetProgress(float progress, string status = null)
    {
        if (progressBar != null)
            progressBar.value = Mathf.Clamp01(progress);
        if (!string.IsNullOrEmpty(status) && statusLabel != null)
            statusLabel.text = status;
    }
}