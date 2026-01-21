using UnityEngine;

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

    // Auto-cached controllers (found from roots)
    private RoundCountdownPanel countdownController;
    private WeaponSelectorPanel weaponSelector;
    private PlayerHUD playerHUD;

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

        // Cache required controllers from roots
        countdownController = FindOnRootOrChildren<RoundCountdownPanel>(countdownPanel.root, "CountdownController");
        weaponSelector = FindOnRootOrChildren<WeaponSelectorPanel>(loadoutPanel.root, "WeaponSelectorPanel");
        playerHUD = FindOnRootOrChildren<PlayerHUD>(hudPanel.root, "PlayerHUD");

        // Default visibility
        HideCanvasGroup(countdownPanel.cg);
        HideCanvasGroup(loadoutPanel.cg);
        HideCanvasGroup(pausePanel.cg);
        ShowCanvasGroup(hudPanel.cg);

        // Debug summary (very useful)
        /*
        Debug.Log(
            $"[GameplayUIManager] Awake | " +
            $"CountdownRoot={(countdownPanel.root ? countdownPanel.root.name : "NULL")} cg={(countdownPanel.cg ? "OK" : "NULL")} ctrl={(countdownController ? "OK" : "NULL")} | " +
            $"LoadoutRoot={(loadoutPanel.root ? loadoutPanel.root.name : "NULL")} cg={(loadoutPanel.cg ? "OK" : "NULL")} selector={(weaponSelector ? "OK" : "NULL")} | " +
            $"HUDRoot={(hudPanel.root ? hudPanel.root.name : "NULL")} cg={(hudPanel.cg ? "OK" : "NULL")} hud={(playerHUD ? "OK" : "NULL")} | " +
            $"PauseRoot={(pausePanel.root ? pausePanel.root.name : "NULL")} cg={(pausePanel.cg ? "OK" : "NULL")}",
            this
        );
        */
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

    public void ToggleLoadoutPanel()
    {
        if (loadoutPanel.cg == null) return;
        if (loadoutPanel.cg.alpha > 0.5f) HideCanvasGroup(loadoutPanel.cg);
        else ShowCanvasGroup(loadoutPanel.cg);
    }

    // HUD
    public void ShowHUD() => ShowCanvasGroup(hudPanel.cg);
    public void HideHUD() => HideCanvasGroup(hudPanel.cg);

    // PAUSE
    public void ShowExitMenu() => ShowCanvasGroup(pausePanel.cg);
    public void HideExitMenu() => HideCanvasGroup(pausePanel.cg);

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
            Debug.LogError("[GameplayUIManager] WeaponSelectorPanel missing (cannot OpenPreRound).", this);
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
