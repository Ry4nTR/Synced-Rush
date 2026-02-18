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
    // Pause input binding (Global)
    // ================================
    [Header("Input Binding")]
    [SerializeField] private string globalActionMapName = "Global";
    [SerializeField] private string togglePauseActionName = "ToggleExitPanel";

    private PlayerInput _playerInput;
    private ClientComponentSwitcher _switcher;
    private bool _pauseOpen;

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
        // Keep trying until we bind once (helps if UI manager spawns before player object)
        if (_playerInput == null || _switcher == null)
            TryBindLocalInput();
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
        _switcher = player.GetComponent<ClientComponentSwitcher>();

        if (_switcher == null)
            _switcher = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;

        if (_playerInput == null || _playerInput.actions == null)
        {
            Debug.LogWarning("[GameplayUIManager] Cannot bind pause: PlayerInput/actions missing.", this);
            return;
        }

        var map = _playerInput.actions.FindActionMap(globalActionMapName, throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogWarning($"[GameplayUIManager] ActionMap '{globalActionMapName}' not found.", this);
            return;
        }

        var action = map.FindAction(togglePauseActionName, throwIfNotFound: false);
        if (action == null)
        {
            Debug.LogWarning($"[GameplayUIManager] Action '{togglePauseActionName}' not found in map '{globalActionMapName}'.", this);
            return;
        }

        if (!map.enabled) map.Enable();
        if (!action.enabled) action.Enable();

        action.performed -= OnTogglePausePerformed;
        action.performed += OnTogglePausePerformed;

        Debug.Log($"[GameplayUIManager] Bound pause toggle '{globalActionMapName}/{togglePauseActionName}'. currentActionMap='{_playerInput.currentActionMap?.name}'.", this);
    }

    private void UnbindPauseAction()
    {
        if (_playerInput == null || _playerInput.actions == null) return;

        var map = _playerInput.actions.FindActionMap(globalActionMapName, throwIfNotFound: false);
        var action = map != null ? map.FindAction(togglePauseActionName, throwIfNotFound: false) : null;
        if (action != null)
            action.performed -= OnTogglePausePerformed;
    }

    private void OnTogglePausePerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log($"[GameplayUIManager] Pause toggle PERFORMED. action='{ctx.action?.name}' control='{ctx.control?.path}' currentActionMap='{_playerInput?.currentActionMap?.name}'.", this);
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
    // Public UI API (RESTORED)
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
    /// ESC / Global pause toggle.
    /// </summary>
    public void ToggleExitMenu()
    {
        if (pausePanel.cg == null) return;

        // Determine next state from alpha
        _pauseOpen = pausePanel.cg.alpha <= 0.5f;

        if (_switcher == null)
            _switcher = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;

        Debug.Log($"[GameplayUIManager] ToggleExitMenu() -> pauseOpen={_pauseOpen} switcherFound={_switcher != null}", this);

        if (_pauseOpen)
        {
            HideHUD();
            HideOptionsPanel();
            ShowExitMenu();

            // IMPORTANT: disable gameplay
            _switcher?.SetState_UIMenu();
            _switcher?.SetMovementGameplayEnabled(false);
            _switcher?.SetWeaponGameplayEnabled(false);
        }
        else
        {
            HideExitMenu();
            HideOptionsPanel();
            ShowHUD();

            // Re-enable gameplay
            _switcher?.SetState_Gameplay();
            _switcher?.SetMovementGameplayEnabled(true);
            _switcher?.SetWeaponGameplayEnabled(true);
        }
    }

    // ----- BUTTON METHODS (wire these in Inspector) -----

    /// <summary>PausePanel -> Options button.</summary>
    public void OpenOptionsFromPauseButton()
    {
        Debug.Log("[GameplayUIManager] OpenOptionsFromPauseButton()", this);
        HideExitMenu();
        ShowOptionsPanel();
    }

    /// <summary>OptionsPanel -> Back button.</summary>
    public void BackToPauseFromOptionsButton()
    {
        Debug.Log("[GameplayUIManager] BackToPauseFromOptionsButton()", this);
        HideOptionsPanel();
        ShowExitMenu();
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
    // Score / Round end (RESTORED)
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
    // Countdown (RESTORED)
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

        if (weaponSelector != null)
            weaponSelector.OpenPreRound();
        else
            Debug.LogError("[GameplayUIManager] LoadoutSelectorPanel missing (cannot OpenPreRound).", this);

        countdownController.StartCountdown(seconds, () =>
        {
            HideCanvasGroup(countdownPanel.cg);
            weaponSelector?.OnCountdownFinished();
            onFinished?.Invoke();
        });
    }

    public void CancelCountdown()
    {
        countdownController?.CancelCountdown();
        HideCanvasGroup(countdownPanel.cg);
    }

    // ================================
    // Binding API (RESTORED)
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
    // HUD Proxies (RESTORED)
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
        if (NetworkManager.Singleton == null) return;

        // Ignore local disconnect; your Exit button handles that path.
        if (clientId == NetworkManager.Singleton.LocalClientId)
            return;

        if (_disconnectRoutine != null)
            return;

        Debug.Log($"[GameplayUIManager] Remote client disconnected: {clientId}. Returning to menu in {disconnectMessageSeconds}s", this);

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
}
