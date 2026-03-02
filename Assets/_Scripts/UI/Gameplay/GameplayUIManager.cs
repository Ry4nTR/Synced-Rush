// ================================
// GameplayUIManager (MODIFIED) ✅
// - Adds a SYSTEM gameplay lock driven by MatchFlowState
// - Pause now disables ONLY inputs (no physics freeze)
// - PreRound / RoundEnd / Loading etc force lock (disable move+weapons)
// ================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SyncedRush.Gamemode;

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

    [Header("Panel Roots")]
    [SerializeField] private PanelRoot countdownPanel;
    [SerializeField] private PanelRoot loadoutPanel;
    [SerializeField] private PanelRoot hudPanel;
    [SerializeField] private PanelRoot pausePanel;
    [SerializeField] private PanelRoot scorePanel;
    [Tooltip("Root for the in-game options menu.")]
    [SerializeField] private PanelRoot optionsPanel;

    private RoundCountdownPanel countdownController;
    private LoadoutSelectorPanel weaponSelector;
    private PlayerHUD playerHUD;
    private ScorePanel scoreController;

    [Header("Input Binding")]
    [SerializeField] private InputActionReference togglePauseActionRef;

    private PlayerInput _playerInput;
    private ClientComponentSwitcher _switcher;

    private enum UiMode
    {
        Gameplay,
        Loadout,
        Pause,
        Options
    }

    private UiMode _currentMode = UiMode.Gameplay;
    private UiMode _lastNonPauseMode = UiMode.Gameplay;

    private bool _preRoundActive = false;

    // ✅ NEW: driven by RoundManager.MatchFlowState
    // true => force disable movement+weapons (PreRoundFrozen, RoundEnd, Loading, Spawning, MatchEnd)
    private bool _systemGameplayLock = false;

    public bool IsPauseOpen => _currentMode == UiMode.Pause;

    [Header("Scene Management")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Disconnect Handling")]
    [SerializeField] private float disconnectMessageSeconds = 5f;
    private Coroutine _disconnectRoutine;

    private void Awake()
    {
        countdownPanel.Cache("Countdown", this);
        loadoutPanel.Cache("Loadout", this);
        hudPanel.Cache("HUD", this);
        pausePanel.Cache("Pause", this);
        scorePanel.Cache("Score", this);
        if (optionsPanel != null) optionsPanel.Cache("Options", this);

        countdownController = FindOnRootOrChildren<RoundCountdownPanel>(countdownPanel.root, "RoundCountdownPanel");
        weaponSelector = FindOnRootOrChildren<LoadoutSelectorPanel>(loadoutPanel.root, "LoadoutSelectorPanel");
        playerHUD = FindOnRootOrChildren<PlayerHUD>(hudPanel.root, "PlayerHUD");
        scoreController = FindOnRootOrChildren<ScorePanel>(scorePanel.root, "ScorePanel");

        HideCanvasGroup(countdownPanel.cg);
        HideCanvasGroup(loadoutPanel.cg);
        HideCanvasGroup(pausePanel.cg);
        HideCanvasGroup(scorePanel.cg);
        ShowCanvasGroup(hudPanel.cg);

        if (optionsPanel != null)
            HideCanvasGroup(optionsPanel.cg);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // ✅ Subscribe to match flow to drive system lock
        RoundManager.OnMatchFlowStateChanged += OnMatchFlowChanged;
        RoundManager.OnRoundEndPresentation += OnRoundEndPresentation;
        RoundManager.OnMatchEnded += OnMatchEnded;
        RoundManager.OnLoadingScreen += OnLoadingScreenChanged;

        TryBindLocalInput();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        RoundManager.OnMatchFlowStateChanged -= OnMatchFlowChanged;
        RoundManager.OnRoundEndPresentation -= OnRoundEndPresentation;
        RoundManager.OnMatchEnded -= OnMatchEnded;
        RoundManager.OnLoadingScreen -= OnLoadingScreenChanged;

        UnbindPauseAction();
    }

    private void Update()
    {
        if (_playerInput == null || _switcher == null)
            TryBindLocalInput();

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

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
    // ✅ Match Flow -> System lock
    // ================================
    private void OnMatchFlowChanged(MatchFlowState oldState, MatchFlowState newState)
    {
        // Only InRound allows gameplay simulation & input.
        _systemGameplayLock = (newState != MatchFlowState.InRound);

        // If system locked, never keep pause/options open forever (optional but safer)
        if (_systemGameplayLock)
        {
            HideExitMenu();
            HideOptionsPanel();
            // keep loadout if pre-round; otherwise let scoreboard etc manage itself
            if (newState == MatchFlowState.PreRoundFrozen)
            {
                _currentMode = UiMode.Loadout;
            }
            else
            {
                _currentMode = UiMode.Gameplay;
            }
        }

        UpdateInputLocks();
    }

    private void OnRoundEndPresentation(int teamAScore, int teamBScore, bool matchOver, float showScoreSeconds)
    {
        PlayRoundEndPresentation(teamAScore, teamBScore, matchOver, showScoreSeconds);
    }

    private void OnMatchEnded(int winningTeam, int teamAScore, int teamBScore)
    {
        // Keep system lock (MatchEnd is locked by flow)
        ShowScorePanel(teamAScore, teamBScore, matchOver: true);
    }

    private void OnLoadingScreenChanged(bool show)
    {
        // optional: you can wire a loading panel here
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

    public void SetLoadoutVisibility(bool visible)
    {
        if (visible)
        {
            ShowLoadoutPanel();
            HideHUD();

            if (_currentMode != UiMode.Pause && _currentMode != UiMode.Options)
                _currentMode = UiMode.Loadout;
        }
        else
        {
            HideLoadoutPanel();
            if (_preRoundActive)
            {
                ShowHUD();
            }
            else
            {
                _currentMode = UiMode.Gameplay;
                ShowHUD();
            }
        }

        UpdateInputLocks();
    }

    public void ToggleExitMenu()
    {
        // if match flow is locking gameplay, ignore pause toggle
        if (_systemGameplayLock)
            return;

        if (pausePanel.cg == null) return;

        if (_currentMode == UiMode.Pause || _currentMode == UiMode.Options)
        {
            HideExitMenu();
            HideOptionsPanel();

            bool canRestoreLoadout =
                (_lastNonPauseMode == UiMode.Loadout) &&
                (weaponSelector != null) &&
                (weaponSelector.IsOpen || weaponSelector.InPreRound);

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

            UpdateInputLocks();
            return;
        }

        _lastNonPauseMode = _currentMode;
        _currentMode = UiMode.Pause;

        HideHUD();
        HideLoadoutPanel();
        HideOptionsPanel();
        ShowExitMenu();

        UpdateInputLocks();
    }

    public void OpenOptionsFromPauseButton()
    {
        HideExitMenu();
        ShowOptionsPanel();
        _currentMode = UiMode.Options;
        UpdateInputLocks();
    }

    public void BackToPauseFromOptionsButton()
    {
        HideOptionsPanel();
        ShowExitMenu();
        _currentMode = UiMode.Pause;
        UpdateInputLocks();
    }

    public void ExitToMainMenuButton()
    {
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
        if (countdownPanel.cg == null || countdownController == null)
        {
            Debug.LogError("[GameplayUIManager] Cannot StartCountdown: countdown panel/controller missing.", this);
            onFinished?.Invoke();
            return;
        }

        ShowCanvasGroup(countdownPanel.cg);

        _preRoundActive = true;
        if (_currentMode != UiMode.Pause && _currentMode != UiMode.Options)
        {
            _currentMode = UiMode.Loadout;
        }

        ShowLoadoutPanel();
        HideHUD();

        if (weaponSelector != null)
            weaponSelector.OpenPreRound();
        else
            Debug.LogError("[GameplayUIManager] LoadoutSelectorPanel missing (cannot OpenPreRound).", this);

        UpdateInputLocks();

        countdownController.StartCountdown(seconds, () =>
        {
            HideCanvasGroup(countdownPanel.cg);

            weaponSelector?.OnCountdownFinished();

            _preRoundActive = false;

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

        if (clientId == nm.LocalClientId)
            return;

        if (_disconnectRoutine != null)
            return;

        if (!nm.IsHost && clientId == NetworkManager.ServerClientId)
        {
            playerHUD?.ShowDisconnectMessage("Host has disconnected", disconnectMessageSeconds);
            _disconnectRoutine = StartCoroutine(DisconnectAndReturnToMenu(disconnectMessageSeconds));
            return;
        }

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
    // Input Locking (FINAL)
    // ================================
    private void UpdateInputLocks()
    {
        if (_switcher == null)
            _switcher = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
        if (_switcher == null)
            return;

        // ✅ HARD SYSTEM LOCK (not pause)
        if (_systemGameplayLock)
        {
            _switcher.SetState_Loadout();              // UI action map
            _switcher.SetMovementGameplayEnabled(false);
            _switcher.SetWeaponGameplayEnabled(false);
            return;
        }

        // ✅ PAUSE: disable input only (movement sim stays ON => gravity keeps falling)
        switch (_currentMode)
        {
            case UiMode.Pause:
            case UiMode.Options:
                _switcher.SetState_UIMenu();
                _switcher.SetWeaponGameplayEnabled(false);
                // IMPORTANT: do NOT disable movement here
                _switcher.SetMovementGameplayEnabled(true);
                break;

            case UiMode.Loadout:
                _switcher.SetState_Loadout();
                _switcher.SetMovementGameplayEnabled(false);
                _switcher.SetWeaponGameplayEnabled(false);
                break;

            default:
                _switcher.SetState_Gameplay();
                _switcher.SetMovementGameplayEnabled(true);
                _switcher.SetWeaponGameplayEnabled(true);
                break;
        }
    }

    public void EnforceInputLockForCurrentUI()
    {
        UpdateInputLocks();
    }
}