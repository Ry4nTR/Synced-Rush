using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [Tooltip("CanvasGroup used for the round countdown display")]
    [SerializeField] private CanvasGroup countdownPanel;
    [SerializeField] private TMP_Text countdownText;

    [Tooltip("CanvasGroup used for the weapon loadout selection UI")]
    [SerializeField] private CanvasGroup loadoutPanel;

    [Tooltip("CanvasGroup used for the main HUD (ammo, health, crosshair, etc.)")]
    [SerializeField] private CanvasGroup hudPanel;

    [Tooltip("CanvasGroup used for the in‑game pause or exit menu")]
    [SerializeField] private CanvasGroup exitMenuPanel;

    // Internal state
    private Coroutine countdownRoutine;

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Panels default state: hide all except HUD (can be toggled externally)
        HideCanvasGroup(countdownPanel);
        HideCanvasGroup(loadoutPanel);
        HideCanvasGroup(exitMenuPanel);
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
    /// Displays the weapon loadout selection panel.  Typically called when a
    /// round starts or when the player presses the loadout toggle action.
    /// </summary>
    public void ShowLoadoutPanel()
    {
        ShowCanvasGroup(loadoutPanel);
    }

    /// <summary>
    /// Hides the weapon loadout selection panel.
    /// </summary>
    public void HideLoadoutPanel()
    {
        HideCanvasGroup(loadoutPanel);
    }

    /// <summary>
    /// Toggles the loadout panel on or off.  If the panel is visible it will be
    /// hidden, and vice‑versa.
    /// </summary>
    public void ToggleLoadoutPanel()
    {
        if (loadoutPanel == null) return;
        if (loadoutPanel.alpha > 0.5f)
            HideLoadoutPanel();
        else
            ShowLoadoutPanel();
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
        ShowCanvasGroup(exitMenuPanel);
        // Additional game‑pause logic could be inserted here.
    }

    /// <summary>
    /// Hides the exit/pause menu panel.
    /// </summary>
    public void HideExitMenu()
    {
        HideCanvasGroup(exitMenuPanel);
    }

    /// <summary>
    /// Starts a pre‑round countdown.  Shows the countdown panel and updates the
    /// provided text element every second.  When the timer ends, the panel is
    /// hidden and a callback is invoked (if provided).
    /// </summary>
    /// <param name="seconds">Duration of the countdown in seconds.</param>
    /// <param name="onFinished">Optional callback invoked when the timer hits zero.</param>
    public void StartCountdown(float seconds, Action onFinished = null)
    {
        // Stop any existing countdown
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        ShowCanvasGroup(countdownPanel);
        countdownRoutine = StartCoroutine(CountdownCoroutine(seconds, onFinished));
    }

    /// <summary>
    /// Immediately cancels an active countdown and hides the countdown panel.
    /// </summary>
    public void CancelCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
        HideCanvasGroup(countdownPanel);
    }
    #endregion

    #region Internal coroutines
    private IEnumerator CountdownCoroutine(float seconds, Action onFinished)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();
            yield return null;
            remaining -= Time.deltaTime;
        }

        // Ensure text is cleared at the end
        if (countdownText != null)
            countdownText.text = string.Empty;
        HideCanvasGroup(countdownPanel);
        countdownRoutine = null;
        onFinished?.Invoke();
    }
    #endregion
}