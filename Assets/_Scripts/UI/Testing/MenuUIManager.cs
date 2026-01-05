using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup createMatchPanel;
    [SerializeField] private CanvasGroup joinMatchPanel;
    [SerializeField] private CanvasGroup lobbyPanel;
    [SerializeField] private CanvasGroup optionsPanel;

    private void Start()
    {
        ShowMainMenu();
    }

    // =========================
    // Public UI Navigation
    // =========================

    public void ShowMainMenu()
    {
        HideAll();
        Show(mainMenuPanel);
    }

    public void ShowCreateMatch()
    {
        HideAll();
        Show(createMatchPanel);
    }

    public void ShowJoinMatch()
    {
        HideAll();
        Show(joinMatchPanel);
    }

    public void ShowLobby()
    {
        HideAll();
        Show(lobbyPanel);
    }

    public void ShowOptions()
    {
        HideAll();
        Show(optionsPanel);
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    // =========================
    // Internal helpers
    // =========================

    private void HideAll()
    {
        Hide(mainMenuPanel);
        Hide(createMatchPanel);
        Hide(joinMatchPanel);
        Hide(lobbyPanel);

        if (optionsPanel != null)
            Hide(optionsPanel);
    }

    private void Show(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void Hide(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}