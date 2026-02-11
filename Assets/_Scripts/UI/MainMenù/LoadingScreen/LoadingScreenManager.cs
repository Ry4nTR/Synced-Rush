using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingScreenManager : MonoBehaviour
{
    [Header("Loading UI")]
    [SerializeField] private CanvasGroup loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusLabel;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Hide();
    }

    private void OnEnable()
    {
        RoundManager.OnLoadingScreen += HandleLoading;
    }

    private void OnDisable()
    {
        RoundManager.OnLoadingScreen -= HandleLoading;
    }

    private void HandleLoading(bool show)
    {
        if (show) Show();
        else Hide();
    }

    public void Show()
    {
        if (loadingPanel != null)
        {
            loadingPanel.alpha = 1f;
            loadingPanel.interactable = true;
            loadingPanel.blocksRaycasts = true;
        }
        if (progressBar != null) progressBar.value = 0f;
        if (statusLabel != null) statusLabel.text = string.Empty;
    }

    public void Hide()
    {
        if (loadingPanel != null)
        {
            loadingPanel.alpha = 0f;
            loadingPanel.interactable = false;
            loadingPanel.blocksRaycasts = false;
        }
    }

    public void SetProgress(float progress, string status = null)
    {
        if (progressBar != null) progressBar.value = Mathf.Clamp01(progress);
        if (!string.IsNullOrEmpty(status) && statusLabel != null) statusLabel.text = status;
    }
}
