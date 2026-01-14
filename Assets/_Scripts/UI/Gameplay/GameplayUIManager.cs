using UnityEngine;

/// <summary>
/// Manages the in–match UI panels for the player.  This includes the round
/// countdown panel, weapon loadout selection panel, HUD and pause/exit
/// menus.  Panels are controlled via their CanvasGroup rather than
/// enabling/disabling GameObjects so they can be faded in/out and
/// maintain their layout.
///
/// The manager exposes methods that can be called by game logic (e.g.,
/// RoundManager or input actions) to show or hide specific panels.  It
/// also provides a countdown coroutine which updates a TMP_Text during
/// the pre‑round timer.
/// </summary>
public class GameplayUIManager : MonoBehaviour
{
    public static GameplayUIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private CanvasGroup countdownPanel;
    [SerializeField] private RoundCountdownPanel countdownController;

    [SerializeField] private CanvasGroup weaponSelectorPanel;
    [SerializeField] private CanvasGroup hudPanel;
    [SerializeField] private CanvasGroup PausePanel;

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Hide non‑HUD panels by default
        HideCanvasGroup(countdownPanel);
        HideCanvasGroup(weaponSelectorPanel);
        HideCanvasGroup(PausePanel);
        ShowCanvasGroup(hudPanel);
    }

    #region Panel control helpers
    private void ShowCanvasGroup(CanvasGroup cg)
    {
        if (!cg) return;
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    private void HideCanvasGroup(CanvasGroup cg)
    {
        if (!cg) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }
    #endregion

    #region Public API
    /// <summary>
    /// Displays the weapon loadout selection panel by delegating to its own
    /// controller.  Typically called when a round starts or when the player
    /// presses the loadout toggle action.
    /// </summary>
    public void ShowLoadoutPanel()
    {
        ShowCanvasGroup(weaponSelectorPanel);
    }

    /// <summary>
    /// Hides the weapon loadout selection panel.
    /// </summary>
    public void HideLoadoutPanel()
    {
        HideCanvasGroup(weaponSelectorPanel);
    }

    /// <summary>
    /// Toggles the loadout panel on or off.  If the panel is visible it will be
    /// hidden, and vice‑versa.
    /// </summary>
    public void ToggleLoadoutPanel()
    {
        if (weaponSelectorPanel == null) return;
        if (weaponSelectorPanel.alpha > 0.5f)
            HideCanvasGroup(weaponSelectorPanel);
        else
            ShowCanvasGroup(weaponSelectorPanel);
    }

    /// <summary>
    /// Shows the main HUD.  Use this after hiding loadout or countdown panels.
    /// </summary>
    public void ShowHUD()
    {
        ShowCanvasGroup(hudPanel);
    }

    /// <summary>
    /// Hides the main HUD.
    /// </summary>
    public void HideHUD()
    {
        HideCanvasGroup(hudPanel);
    }

    /// <summary>
    /// Displays the exit/pause menu panel.  Pauses gameplay if necessary.
    /// </summary>
    public void ShowExitMenu()
    {
        ShowCanvasGroup(PausePanel);
    }

    /// <summary>
    /// Hides the exit/pause menu panel.
    /// </summary>
    public void HideExitMenu()
    {
        HideCanvasGroup(PausePanel);
    }

    /// <summary>
    /// Starts a pre‑round countdown by delegating to the countdown panel.  Shows
    /// the countdown UI and invokes a callback when finished.
    /// </summary>
    /// <param name="seconds">Duration of the countdown in seconds.</param>
    /// <param name="onFinished">Optional callback invoked when the timer hits zero.</param>
    public void StartCountdown(float seconds, System.Action onFinished = null)
    {
        if (countdownPanel == null || countdownController == null)
            return;
        ShowCanvasGroup(countdownPanel);
        countdownController.StartCountdown(seconds, () =>
        {
            HideCanvasGroup(countdownPanel);
            onFinished?.Invoke();
        });
    }

    /// <summary>
    /// Cancels an active countdown if one is running.
    /// </summary>
    public void CancelCountdown()
    {
        if (countdownController != null)
        {
            countdownController.CancelCountdown();
        }
        HideCanvasGroup(countdownPanel);
    }
    #endregion
}