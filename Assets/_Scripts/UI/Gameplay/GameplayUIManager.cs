using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameplayUIManager : MonoBehaviour
{
    [System.Serializable]
    private class PanelRoot
    {
        public GameObject root;
        [HideInInspector] public CanvasGroup cg;

        public void Cache(string label, MonoBehaviour owner)
        {
            cg = null;

            if (root == null)
            {
                Debug.LogWarning($"[GameplayUIManager] Panel '{label}' root is NULL.", owner);
                return;
            }

            cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.GetComponentInChildren<CanvasGroup>(true);

            if (cg == null)
                Debug.LogError($"[GameplayUIManager] Panel '{label}' root '{root.name}' has no CanvasGroup.", owner);
        }
    }

    // ================================
    // Panels
    // ================================
    [Header("Panel Roots")]
    [SerializeField] private PanelRoot countdownPanel;
    [SerializeField] private PanelRoot loadoutPanel;
    [SerializeField] private PanelRoot hudPanel;
    [SerializeField] private PanelRoot pausePanel;
    [SerializeField] private PanelRoot scorePanel;
    [Tooltip("Root for the in-game options menu.")]
    [SerializeField] private PanelRoot optionsPanel;

    // Auto-cached controllers (found from roots)
    private RoundCountdownPanel countdownController;
    private LoadoutSelectorPanel weaponSelector;
    private PlayerHUD playerHUD;
    private ScorePanel scoreController;

    // ================================
    // Pause / UI State management
    // ================================
    [Header("Input Binding")]
    [SerializeField] private InputActionReference togglePauseActionRef;

    private PlayerInput _playerInput;
    private ClientComponentSwitcher _switcher;

    /// <summary>
    /// Enumeration of the high‑level UI state.  This replaces relying on CanvasGroup alpha
    /// values to determine what is visible.  The possible states are:
    ///  - Gameplay: normal HUD/gameplay state.
    ///  - Loadout: loadout selection is visible (pre‑round or mid‑match).
    ///  - Pause: the pause menu is open.
    ///  - Options: the options sub‑panel is open.
    /// </summary>
    private enum UiMode
    {
        Gameplay,
        Loadout,
        Pause,
        Options
    }

    // Tracks the current UI mode.  Defaults to Gameplay.
    private UiMode _currentMode = UiMode.Gameplay;

    // Remembers the mode we were in before opening the pause menu.  This allows
    // restoring the correct mode when unpausing (e.g. returning to loadout if the
    // pre‑round is still active).
    private UiMode _lastNonPauseMode = UiMode.Gameplay;

    // Indicates whether the game is currently in a pre‑round countdown.  When
    // true, the loadout panel should be considered a pre‑round panel and
    // gameplay input should remain disabled.
    private bool _preRoundActive = false;

    /// <summary>
    /// Returns true when the pause menu is currently the topmost UI.  This
    /// property should be used instead of inspecting panel alpha.
    /// </summary>
    public bool IsPauseOpen => _currentMode == UiMode.Pause;

    // ================================
    // Scene Management
    // ================================
    [Header("Scene Management")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ================================
    // Disconnect Handling
    // ================================
    [Header("Disconnect Handling")]
    [SerializeField] private float disconnectMessageSeconds = 5f;
    private Coroutine _disconnectRoutine;

    private void Awake()
    {
        // Cache CanvasGroups
        countdownPanel.Cache("Countdown", this);
        loadoutPanel.Cache("Loadout", this);
        hudPanel.Cache("HUD", this);
        pausePanel.Cache("Pause", this);
        scorePanel.Cache("Score", this);
        if (optionsPanel != null) optionsPanel.Cache("Options", this);

        // Find controllers
        countdownController = FindOnRootOrChildren<RoundCountdownPanel>(countdownPanel.root, "RoundCountdownPanel");
        weaponSelector = FindOnRootOrChildren<LoadoutSelectorPanel>(loadoutPanel.root, "LoadoutSelectorPanel");
        playerHUD = FindOnRootOrChildren<PlayerHUD>(hudPanel.root, "PlayerHUD");
        scoreController = FindOnRootOrChildren<ScorePanel>(scorePanel.root, "ScorePanel");

        // Default visibility
        HideCanvasGroup(countdownPanel.cg);
        HideCanvasGroup(loadoutPanel.cg);
        HideCanvasGroup(pausePanel.cg);
        HideCanvasGroup(scorePanel.cg);
        ShowCanvasGroup(hudPanel.cg);

        if (optionsPanel != null)
            HideCanvasGroup(optionsPanel.cg);

        // Netcode callbacks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // Try bind immediately (in case player already exists)
        TryBindLocalInput();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        UnbindPauseAction();
    }

    private void Update()
    {
        if (_playerInput == null || _switcher == null)
            TryBindLocalInput();

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // If we’re a client and we’re no longer connected/listening -> host is gone
        if (!nm.IsHost && (_disconnectRoutine == null) && (!nm.IsConnectedClient || !nm.IsListening))
        {
            Debug.Log("[GameplayUIManager] Lost connection to host (watchdog).");
            playerHUD?.ShowDisconnectMessage("Host has disconnected", disconnectMessageSeconds);
            _disconnectRoutine = StartCoroutine(DisconnectAndReturnToMenu(disconnectMessageSeconds));
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        TryBindLocalInput();
    }

    // ================================
    // Binding (Global action map)
    // ================================
    private void TryBindLocalInput()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null) return;

        _playerInput = player.GetComponent<PlayerInput>();
        if (_playerInput == null)
            _playerInput = player.GetComponentInChildren<PlayerInput>(true);

        _switcher = player.GetComponent<ClientComponentSwitcher>();
        if (_switcher == null)
            _switcher = player.GetComponentInChildren<ClientComponentSwitcher>(true);

        if (_playerInput == null || _playerInput.actions == null)
            return;

        if (togglePauseActionRef == null || togglePauseActionRef.action == null)
            return;

        // Find the runtime action by GUID inside PlayerInput.actions (safe even if renamed)
        var runtimeAction = _playerInput.actions.FindAction(togglePauseActionRef.action.id);
        if (runtimeAction == null)
            return;

        runtimeAction.actionMap.Enable();
        runtimeAction.Enable();

        runtimeAction.performed -= OnTogglePausePerformed;
        runtimeAction.performed += OnTogglePausePerformed;
    }

    private void UnbindPauseAction()
    {
        if (_playerInput == null || _playerInput.actions == null) return;
        if (togglePauseActionRef == null || togglePauseActionRef.action == null) return;

        var runtimeAction = _playerInput.actions.FindAction(togglePauseActionRef.action.id);
        if (runtimeAction != null)
            runtimeAction.performed -= OnTogglePausePerformed;
    }

    private void OnTogglePausePerformed(InputAction.CallbackContext ctx)
    {
        ToggleExitMenu();
    }

    // ================================
    // Panel + CanvasGroup helpers
    // ================================
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

    private T FindOnRootOrChildren<T>(GameObject root, string label) where T : Component
    {
        if (root == null) return null;

        var found = root.GetComponent<T>();
        if (found != null) return found;

        found = root.GetComponentInChildren<T>(true);
        if (found == null)
            Debug.LogError($"[GameplayUIManager] Missing '{typeof(T).Name}' for {label} under '{root.name}'.", this);

        return found;
    }

    // ================================
    // Public UI API
    // ================================
    #region Panels API

    public void ShowLoadoutPanel() => ShowCanvasGroup(loadoutPanel.cg);
    public void HideLoadoutPanel() => HideCanvasGroup(loadoutPanel.cg);

    public void ShowHUD() => ShowCanvasGroup(hudPanel.cg);
    public void HideHUD() => HideCanvasGroup(hudPanel.cg);

    public void ShowExitMenu() => ShowCanvasGroup(pausePanel.cg);
    public void HideExitMenu() => HideCanvasGroup(pausePanel.cg);

    public void ShowOptionsPanel()
    {
        if (optionsPanel?.cg == null) return;
        ShowCanvasGroup(optionsPanel.cg);
    }

    public void HideOptionsPanel()
    {
        if (optionsPanel?.cg == null) return;
        HideCanvasGroup(optionsPanel.cg);
    }

    /// <summary>
    /// Sets the visibility of the loadout panel outside of the pre‑round
    /// countdown.  When <paramref name="visible"/> is true the UI mode
    /// switches to Loadout and the HUD is hidden; otherwise the mode
    /// reverts to Gameplay (unless a pre‑round countdown is still active).
    /// Input locks are updated automatically.
    /// </summary>
    public void SetLoadoutVisibility(bool visible)
    {
        if (visible)
        {
            // Show loadout and hide HUD
            ShowLoadoutPanel();
            HideHUD();

            // When opening the loadout outside of pause we switch to loadout mode
            if (_currentMode != UiMode.Pause && _currentMode != UiMode.Options)
                _currentMode = UiMode.Loadout;
        }
        else
        {
            // Hide the panel.  If we are still in a pre‑round countdown,
            // keep the mode as Loadout (so movement stays disabled) but
            // allow the HUD to show so players can see the timer.  Otherwise
            // revert to Gameplay.
            HideLoadoutPanel();
            if (_preRoundActive)
            {
                // Keep loadout mode but allow the HUD to be visible
                ShowHUD();
            }
            else
            {
                // Transition back to gameplay
                _currentMode = UiMode.Gameplay;
                ShowHUD();
            }
        }

        UpdateInputLocks();
    }

    /// <summary>
    /// ESC / Global pause toggle.  Uses the current UI mode and pre‑round flag
    /// to determine how to transition in and out of pause.  When opening the
    /// pause menu, we remember the previous mode so that unpausing can
    /// restore the correct state.  When unpausing we either return to
    /// loadout (if still in pre‑round) or to the last non‑pause mode.
    /// </summary>
    public void ToggleExitMenu()
    {
        if (pausePanel.cg == null) return;

        // If the pause menu or options menu are currently open, closing the pause
        // should restore the appropriate mode (loadout or gameplay).
        if (_currentMode == UiMode.Pause || _currentMode == UiMode.Options)
        {
            // Hide pause and options panels
            HideExitMenu();
            HideOptionsPanel();

            // Determine which mode to return to
            if (_preRoundActive)
            {
                // During the pre‑round countdown we are still in loadout mode.
                // If the loadout panel is still open (i.e. the player did not
                // manually close it), restore the panel; otherwise keep the
                // panel hidden but remain in loadout mode so inputs stay
                // disabled.
                _currentMode = UiMode.Loadout;
                if (weaponSelector != null && weaponSelector.IsOpen)
                {
                    ShowLoadoutPanel();
                    HideHUD();
                }
                else
                {
                    HideLoadoutPanel();
                    ShowHUD();
                }
            }
            else
            {
                bool canRestoreLoadout =
                    (_lastNonPauseMode == UiMode.Loadout) &&
                    (weaponSelector != null) &&
                    (weaponSelector.IsOpen || weaponSelector.InPreRound); // only if actually intended

                if (canRestoreLoadout)
                {
                    _currentMode = UiMode.Loadout;
                    ShowLoadoutPanel();
                    HideHUD();
                }
                else
                {
                    _currentMode = UiMode.Gameplay;
                    HideLoadoutPanel();
                    ShowHUD();
                }
            }

            // Update input locks for the new mode
            UpdateInputLocks();
            return;
        }

        // Opening the pause menu.  Remember the current mode so we can
        // restore it later when unpausing.
        _lastNonPauseMode = _currentMode;
        _currentMode = UiMode.Pause;

        // Hide other panels and show pause
        HideHUD();
        HideLoadoutPanel();
        HideOptionsPanel();
        ShowExitMenu();

        // Apply input locks
        UpdateInputLocks();
    }

    // ----- BUTTON METHODS (wire these in Inspector) -----

    /// <summary>PausePanel -> Options button.</summary>
    public void OpenOptionsFromPauseButton()
    {
        Debug.Log("[GameplayUIManager] OpenOptionsFromPauseButton()", this);
        // Transition from pause to options.  Keep the input lock the same as pause.
        HideExitMenu();
        ShowOptionsPanel();
        _currentMode = UiMode.Options;
        UpdateInputLocks();
    }

    /// <summary>OptionsPanel -> Back button.</summary>
    public void BackToPauseFromOptionsButton()
    {
        Debug.Log("[GameplayUIManager] BackToPauseFromOptionsButton()", this);
        // Return from options to pause.  Input lock remains in pause state.
        HideOptionsPanel();
        ShowExitMenu();
        _currentMode = UiMode.Pause;
        UpdateInputLocks();
    }

    /// <summary>PausePanel -> Exit button.</summary>
    public void ExitToMainMenuButton()
    {
        Debug.Log("[GameplayUIManager] ExitToMainMenuButton()", this);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    #endregion

    // ================================
    // Score / Round end
    // ================================
    public void PlayRoundEndPresentation(int teamAScore, int teamBScore, bool matchOver, float showScoreSeconds)
    {
        ShowScorePanel(teamAScore, teamBScore, matchOver);

        CancelInvoke(nameof(HideScorePanel));
        Invoke(nameof(HideScorePanel), showScoreSeconds);
    }

    public void ShowScorePanel(int teamAScore, int teamBScore, bool matchOver)
    {
        if (scorePanel.cg == null || scoreController == null) return;
        scoreController.ShowScores(teamAScore, teamBScore, matchOver);
        ShowCanvasGroup(scorePanel.cg);
    }

    public void HideScorePanel()
    {
        if (scorePanel.cg == null) return;
        HideCanvasGroup(scorePanel.cg);
    }

    // ================================
    // Countdown
    // ================================
    public void StartCountdown(float seconds, System.Action onFinished = null)
    {
        // Sanity check for required components
        if (countdownPanel.cg == null || countdownController == null)
        {
            Debug.LogError("[GameplayUIManager] Cannot StartCountdown: countdown panel/controller missing.", this);
            onFinished?.Invoke();
            return;
        }

        // Show the countdown panel
        ShowCanvasGroup(countdownPanel.cg);

        // Pre‑round begins: mark that a countdown is active.  If we are not already
        // paused or in options, switch the mode to Loadout.  Remember that the
        // loadout UI is visible during the countdown.
        _preRoundActive = true;
        if (_currentMode != UiMode.Pause && _currentMode != UiMode.Options)
        {
            _currentMode = UiMode.Loadout;
        }

        // Show the loadout panel and hide the HUD.  The LoadoutSelectorPanel
        // may contain additional logic (e.g. weapon preselection) but UI
        // visibility and input state are handled here.
        ShowLoadoutPanel();
        HideHUD();

        // Inform the loadout selector that pre‑round is starting so it can
        // initialize selections.  Do not change UI visibility here; we handle it above.
        if (weaponSelector != null)
            weaponSelector.OpenPreRound();
        else
            Debug.LogError("[GameplayUIManager] LoadoutSelectorPanel missing (cannot OpenPreRound).", this);

        // Apply the updated input locks now that we've entered the loadout mode
        UpdateInputLocks();

        // Begin the countdown.  When it finishes, we will end the pre‑round.
        countdownController.StartCountdown(seconds, () =>
        {
            // Hide the countdown panel
            HideCanvasGroup(countdownPanel.cg);

            // Tell the loadout selector that the countdown finished so it can
            // apply the selected weapon/ability.  It should not modify UI state.
            weaponSelector?.OnCountdownFinished();

            // The pre-round has ended
            _preRoundActive = false;

            // If pre-round ended while we are paused/options, never restore Loadout on unpause.
            if (_currentMode == UiMode.Pause || _currentMode == UiMode.Options)
            {
                _lastNonPauseMode = UiMode.Gameplay;
            }

            if (_currentMode != UiMode.Pause && _currentMode != UiMode.Options)
            {
                _currentMode = UiMode.Gameplay;
                HideLoadoutPanel();
                ShowHUD();
            }

            UpdateInputLocks();
            onFinished?.Invoke();
        });
    }

    public void CancelCountdown()
    {
        countdownController?.CancelCountdown();
        HideCanvasGroup(countdownPanel.cg);
    }

    // ================================
    // Binding API
    // ================================
    public void RegisterPlayer(GameObject player)
    {
        if (playerHUD == null)
        {
            Debug.LogError("[GameplayUIManager] RegisterPlayer failed: PlayerHUD is NULL.", this);
            return;
        }

        playerHUD.BindPlayer(player);
        ShowHUD();
    }

    public void RegisterWeapon(WeaponController weapon)
    {
        if (playerHUD == null)
        {
            Debug.LogError("[GameplayUIManager] RegisterWeapon failed: PlayerHUD is NULL.", this);
            return;
        }

        playerHUD.BindWeapon(weapon);
        ShowHUD();
    }

    // ================================
    // HUD Proxies
    // ================================
    public void SetJetpackUIVisible(bool value)
    {
        if (playerHUD == null) return;
        playerHUD.SetJetpackUIVisibility(value);
    }

    public void SetDashCharge(float currentCharge, float maxCharge)
    {
        if (playerHUD == null) return;
        playerHUD.UpdateDashCharge(currentCharge, maxCharge);
    }

    public void SetJetpackCharge(float currentCharge, float maxCharge)
    {
        if (playerHUD == null) return;
        playerHUD.UpdateJetpackCharge(currentCharge, maxCharge);
    }

    public void PlayHitmarker(bool isKill, bool isHeadshot)
    {
        if (playerHUD == null) return;
        playerHUD.PlayHitmarker(isKill, isHeadshot);
    }

    // ================================
    // Disconnect Handling
    // ================================
    private void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Ignore our own intentional shutdown (Exit button path)
        if (clientId == nm.LocalClientId)
            return;

        if (_disconnectRoutine != null)
            return;

        // If we are a client and the server disconnected -> host is gone
        if (!nm.IsHost && clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("[GameplayUIManager] Host disconnected during match.");
            playerHUD?.ShowDisconnectMessage("Host has disconnected", disconnectMessageSeconds);
            _disconnectRoutine = StartCoroutine(DisconnectAndReturnToMenu(disconnectMessageSeconds));
            return;
        }

        // Otherwise: peer client disconnected (if you want to keep match alive, change this)
        Debug.Log($"[GameplayUIManager] Client disconnected: {clientId}. Returning to menu in {disconnectMessageSeconds}s");
        playerHUD?.ShowDisconnectMessage("A player has disconnected", disconnectMessageSeconds);
        _disconnectRoutine = StartCoroutine(DisconnectAndReturnToMenu(disconnectMessageSeconds));
    }

    private IEnumerator DisconnectAndReturnToMenu(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    // ================================
    // Input Locking
    // ================================
    /// <summary>
    /// Applies the appropriate input state based on the current UI mode.
    /// Gameplay is enabled only when the mode is Gameplay; for Loadout,
    /// Pause and Options, all movement and weapon input is disabled.
    /// </summary>
    private void UpdateInputLocks()
    {
        if (_switcher == null)
            _switcher = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
        if (_switcher == null)
            return;

        switch (_currentMode)
        {
            case UiMode.Pause:
            case UiMode.Options:
                // Pause and options menus fully block gameplay input
                _switcher.SetState_UIMenu();
                _switcher.SetMovementGameplayEnabled(false);
                _switcher.SetWeaponGameplayEnabled(false);
                break;
            case UiMode.Loadout:
                // Loadout selection blocks gameplay input but keeps UI action map
                _switcher.SetState_Loadout();
                _switcher.SetMovementGameplayEnabled(false);
                _switcher.SetWeaponGameplayEnabled(false);
                break;
            default:
                // Gameplay: enable everything
                _switcher.SetState_Gameplay();
                _switcher.SetMovementGameplayEnabled(true);
                _switcher.SetWeaponGameplayEnabled(true);
                break;
        }
    }

    /// <summary>
    /// Backwards‑compatibility wrapper that applies the new UI state based
    /// locking.  This method remains public for other systems that expect
    /// to call EnforceInputLockForCurrentUI().
    /// </summary>
    public void EnforceInputLockForCurrentUI()
    {
        UpdateInputLocks();
    }
}
