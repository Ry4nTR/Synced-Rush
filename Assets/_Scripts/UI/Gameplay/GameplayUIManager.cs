using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the in–match UI panels for the player.
/// Assign ONLY the panel root GameObjects in the inspector.
/// The manager will auto-find CanvasGroups and panel scripts from those roots.
/// </summary>
public class GameplayUIManager : MonoBehaviour
{
    public static GameplayUIManager Instance { get; private set; }

    [System.Serializable]
    private class PanelRoot
    {
        [Tooltip("Root GameObject for the panel (usually the panel container in the Canvas).")]
        public GameObject root;

        [HideInInspector] public CanvasGroup cg;

        public bool IsAssigned => root != null;

        public void Cache(string label, MonoBehaviour owner)
        {
            cg = null;

            if (root == null)
            {
                Debug.LogWarning($"[GameplayUIManager] Panel '{label}' root is NULL (not assigned).", owner);
                return;
            }

            // Prefer CanvasGroup on the root, otherwise search in children.
            cg = root.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = root.GetComponentInChildren<CanvasGroup>(true);

            if (cg == null)
                Debug.LogError($"[GameplayUIManager] Panel '{label}' root '{root.name}' has no CanvasGroup (on root or children).", owner);
        }
    }

    [Header("Panel Roots (assign ONLY these)")]
    [SerializeField] private PanelRoot countdownPanel;
    [SerializeField] private PanelRoot loadoutPanel;
    [SerializeField] private PanelRoot hudPanel;
    [SerializeField] private PanelRoot pausePanel;
    [SerializeField] private PanelRoot scorePanel;

    // Auto-cached controllers (found from roots)
    private RoundCountdownPanel countdownController;
    private LoadoutSelectorPanel weaponSelector;
    private PlayerHUD playerHUD;
    private ScorePanel scoreController;

    // Input binding for pause/exit
    private PlayerInput _playerInput;
    private ClientComponentSwitcher _switcher;
    private bool _exitOpen;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Cache CanvasGroups from roots
        countdownPanel.Cache("Countdown", this);
        loadoutPanel.Cache("Loadout", this);
        hudPanel.Cache("HUD", this);
        pausePanel.Cache("Pause", this);
        scorePanel.Cache("Score", this);

        // Cache required controllers from roots
        countdownController = FindOnRootOrChildren<RoundCountdownPanel>(countdownPanel.root, "CountdownController");
        weaponSelector = FindOnRootOrChildren<LoadoutSelectorPanel>(loadoutPanel.root, "LoadoutSelectorPanel");
        playerHUD = FindOnRootOrChildren<PlayerHUD>(hudPanel.root, "PlayerHUD");
        scoreController = FindOnRootOrChildren<ScorePanel>(scorePanel.root, "ScorePanel");

        // Default visibility
        HideCanvasGroup(countdownPanel.cg);
        HideCanvasGroup(loadoutPanel.cg);
        HideCanvasGroup(pausePanel.cg);
        HideCanvasGroup(scorePanel.cg);
        ShowCanvasGroup(hudPanel.cg);

        // Bind local input when the local client connects.
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        if (_playerInput != null)
        {
            var a = _playerInput.actions["ToggleExitPanel"];
            if (a != null) a.performed -= OnToggleExitPerformed;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        TryBindLocalInput();
    }

    private void TryBindLocalInput()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null) return;

        _playerInput = player.GetComponent<PlayerInput>();
        _switcher = player.GetComponent<ClientComponentSwitcher>();

        if (_playerInput == null || _playerInput.actions == null)
        {
            Debug.LogWarning("[GameplayUIManager] Cannot bind ToggleExitPanel: PlayerInput/actions missing.", this);
            return;
        }

        var action = _playerInput.actions["ToggleExitPanel"];
        if (action == null)
        {
            Debug.LogWarning("[GameplayUIManager] Action 'ToggleExitPanel' not found. Check Global action map.", this);
            return;
        }

        // Enable explicitly so it keeps working across action map switches.
        action.Enable();
        action.performed -= OnToggleExitPerformed;
        action.performed += OnToggleExitPerformed;
    }

    private void OnToggleExitPerformed(InputAction.CallbackContext ctx)
    {
        ToggleExitMenu();
    }

    private T FindOnRootOrChildren<T>(GameObject root, string label) where T : Component
    {
        if (root == null)
            return null;

        var found = root.GetComponent<T>();
        if (found != null) return found;

        found = root.GetComponentInChildren<T>(true);
        if (found == null)
        {
            Debug.LogError($"[GameplayUIManager] Missing component '{typeof(T).Name}' for {label} under root '{root.name}'.", this);
        }
        return found;
    }

    #region CanvasGroup helpers
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

    #region Panels API

    // LOADOUT
    public void ShowLoadoutPanel() => ShowCanvasGroup(loadoutPanel.cg);
    public void HideLoadoutPanel() => HideCanvasGroup(loadoutPanel.cg);

    // HUD
    public void ShowHUD() => ShowCanvasGroup(hudPanel.cg);
    public void HideHUD() => HideCanvasGroup(hudPanel.cg);

    // PAUSE/EXIT
    public void ShowExitMenu() => ShowCanvasGroup(pausePanel.cg);
    public void HideExitMenu() => HideCanvasGroup(pausePanel.cg);

    public void ToggleExitMenu()
    {
        if (pausePanel.cg == null) return;

        _exitOpen = pausePanel.cg.alpha <= 0.5f;
        if (_exitOpen)
        {
            ShowExitMenu();
            HideHUD();
            _switcher?.SetState_UIMenu();
        }
        else
        {
            HideExitMenu();
            ShowHUD();
            _switcher?.SetState_Gameplay();
        }
    }

    // SCORE
    public void ShowScorePanel(int teamAScore, int teamBScore, bool matchOver)
    {
        if (scorePanel.cg == null || scoreController == null)
            return;

        scoreController.ShowScores(teamAScore, teamBScore, matchOver);
        ShowCanvasGroup(scorePanel.cg);
    }

    public void HideScorePanel() => HideCanvasGroup(scorePanel.cg);

    // COUNTDOWN
    public void StartCountdown(float seconds, System.Action onFinished = null)
    {
        if (countdownPanel.cg == null || countdownController == null)
        {
            Debug.LogError("[GameplayUIManager] Cannot StartCountdown: countdown panel or controller missing.", this);
            onFinished?.Invoke();
            return;
        }

        ShowCanvasGroup(countdownPanel.cg);

        if (weaponSelector == null)
            Debug.LogError("[GameplayUIManager] LoadoutSelectorPanel missing (cannot OpenPreRound).", this);
        else
            weaponSelector.OpenPreRound();

        countdownController.StartCountdown(seconds, () =>
        {
            HideCanvasGroup(countdownPanel.cg);
            OnCountdownFinished();
            onFinished?.Invoke();
        });
    }

    private void OnCountdownFinished()
    {
        if (weaponSelector == null)
        {
            Debug.LogError("[GameplayUIManager] OnCountdownFinished: weaponSelector missing.", this);
            return;
        }

        weaponSelector.OnCountdownFinished();
    }

    public void CancelCountdown()
    {
        if (countdownController != null)
            countdownController.CancelCountdown();

        HideCanvasGroup(countdownPanel.cg);
    }

    #endregion

    #region Binding API (player & weapon)

    public void RegisterPlayer(GameObject player)
    {
        if (playerHUD == null)
        {
            Debug.LogError("[GameplayUIManager] RegisterPlayer failed: PlayerHUD is NULL. Check HUD root assignment.", this);
            return;
        }

        playerHUD.BindPlayer(player);
        ShowHUD();
    }

    public void RegisterWeapon(WeaponController weapon)
    {
        if (playerHUD == null)
        {
            Debug.LogError("[GameplayUIManager] RegisterWeapon failed: PlayerHUD is NULL. Check HUD root assignment.", this);
            return;
        }

        playerHUD.BindWeapon(weapon);
        ShowHUD();
    }

    #endregion
}
