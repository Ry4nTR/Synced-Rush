using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

    //  Auto-cached controllers (found from roots)
    private RoundCountdownPanel countdownController;
    private LoadoutSelectorPanel weaponSelector;
    private PlayerHUD playerHUD;
    private ScorePanel scoreController;

    //  Local input binding (pause/exit)
    private PlayerInput _playerInput;
    private ClientComponentSwitcher _switcher;
    private bool _exitOpen;

    private void Awake()
    {
        countdownPanel.Cache("Countdown", this);
        loadoutPanel.Cache("Loadout", this);
        hudPanel.Cache("HUD", this);
        pausePanel.Cache("Pause", this);
        scorePanel.Cache("Score", this);

        countdownController = FindOnRootOrChildren<RoundCountdownPanel>(countdownPanel.root, "RoundCountdownPanel");
        weaponSelector = FindOnRootOrChildren<LoadoutSelectorPanel>(loadoutPanel.root, "LoadoutSelectorPanel");
        playerHUD = FindOnRootOrChildren<PlayerHUD>(hudPanel.root, "PlayerHUD");
        scoreController = FindOnRootOrChildren<ScorePanel>(scorePanel.root, "ScorePanel");

        // default visibility
        HideCanvasGroup(countdownPanel.cg);
        HideCanvasGroup(loadoutPanel.cg);
        HideCanvasGroup(pausePanel.cg);
        HideCanvasGroup(scorePanel.cg);
        ShowCanvasGroup(hudPanel.cg);

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

    // ================================
    //  Binding helpers
    // ================================
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
            Debug.LogWarning("[GameplayUIManager] Action 'ToggleExitPanel' not found (Global map).", this);
            return;
        }

        action.Enable();
        action.performed -= OnToggleExitPerformed;
        action.performed += OnToggleExitPerformed;
    }

    private void OnToggleExitPerformed(InputAction.CallbackContext ctx) => ToggleExitMenu();

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
    //  Round end UI API (scoreboard)
    // ================================
    public void PlayRoundEndPresentation(int teamAScore, int teamBScore, bool matchOver, float showScoreSeconds)
    {
        ShowScorePanel(teamAScore, teamBScore, matchOver);

        CancelInvoke(nameof(HideScorePanel));
        Invoke(nameof(HideScorePanel), showScoreSeconds);
    }

    // ================================
    //  CanvasGroup helpers
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

    #region Panels API
    public void ShowLoadoutPanel() => ShowCanvasGroup(loadoutPanel.cg);
    public void HideLoadoutPanel() => HideCanvasGroup(loadoutPanel.cg);

    public void ShowHUD() => ShowCanvasGroup(hudPanel.cg);
    public void HideHUD() => HideCanvasGroup(hudPanel.cg);

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

    public void ShowScorePanel(int teamAScore, int teamBScore, bool matchOver)
    {
        if (scorePanel.cg == null || scoreController == null) return;
        scoreController.ShowScores(teamAScore, teamBScore, matchOver);
        ShowCanvasGroup(scorePanel.cg);
    }

    public void HideScorePanel() => HideCanvasGroup(scorePanel.cg);

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

    #endregion

    // ================================
    //  Binding API (player & weapon)
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
    //  HUD API (thin proxies to PlayerHUD)
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
}
