using UnityEngine;

public class PanelManager : MonoBehaviour
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

        // Workaround per bug che non mostra il nome della lobby e l'IP nella nuova UI
        // Il problema sta nel fatto che per qualche diavolo di motivo viene prima eseguito l'OnEnable di LobbyPanelController piuttosto che l'Awake di LobbyManager
        // Grazie Unity per farmi apprezzare di più Godot
        lobbyPanel.gameObject.SetActive(false);
        lobbyPanel.gameObject.SetActive(true);
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