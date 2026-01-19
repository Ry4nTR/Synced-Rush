using System;
using UnityEngine;
using static ClientComponentSwitcher;

/// <summary>
/// Manages the in–match UI panels for the player.
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

    [Header("Weapon Selection")]
    [Tooltip("Reference to the weapon selector panel used to equip weapons.")]
    [SerializeField] private WeaponSelectorPanel weaponSelector;

    [Tooltip("Weapon ID to equip automatically if the player has not selected a weapon by the end of the countdown.")]
    [SerializeField] private int defaultWeaponId = 0;

    [Header("HUD")]
    [Tooltip("Reference to the Player HUD responsible for displaying health and ammo.")]
    [SerializeField] private PlayerHUD playerHUD;

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
    /// LOADOUT-PANEL
    /// </summary>
    public void ShowLoadoutPanel()
    {
        Debug.Log("[UI] ShowLoadoutPanel -> forcing SetState_Loadout()");

        ShowCanvasGroup(weaponSelectorPanel);

        ClientComponentSwitcherLocal.Local?.SetState_Loadout();
    }

    public void HideLoadoutPanel()
    {
        HideCanvasGroup(weaponSelectorPanel);
    }

    public void ToggleLoadoutPanel()
    {
        if (weaponSelectorPanel == null) return;
        if (weaponSelectorPanel.alpha > 0.5f)
            HideCanvasGroup(weaponSelectorPanel);
        else
            ShowCanvasGroup(weaponSelectorPanel);
    }

    /// <summary>
    /// HUD
    /// </summary>
    public void ShowHUD()
    {
        ShowCanvasGroup(hudPanel);
    }

    public void HideHUD()
    {
        HideCanvasGroup(hudPanel);
    }

    /// <summary>
    /// EXIT MENU
    /// </summary>
    public void ShowExitMenu()
    {
        ShowCanvasGroup(PausePanel);
    }

    public void HideExitMenu()
    {
        HideCanvasGroup(PausePanel);
    }

    /// <summary>
    /// COUNTDOWN PANEL 
    /// </summary>
    // Starts a pre‑round countdown by delegating to the countdown panel.
    public void StartCountdown(float seconds, System.Action onFinished = null)
    {
        if (countdownPanel == null || countdownController == null)
            return;
        ShowCanvasGroup(countdownPanel);

        weaponSelector.OpenPreRound();

        // When the countdown finishes we need to ensure the player either has selected a weapon or is equipped with the default.
        countdownController.StartCountdown(seconds, () =>
        {
            HideCanvasGroup(countdownPanel);
            OnCountdownFinished();
            onFinished?.Invoke();
        });
    }

    // Invoked internally when the pre‑round countdown finishes.
    private void OnCountdownFinished()
    {
        // Delegate to the weapon selector panel.
        weaponSelector.OnCountdownFinished();
    }

    // Cancels an active countdown if one is running.
    public void CancelCountdown()
    {
        if (countdownController != null)
        {
            countdownController.CancelCountdown();
        }
        HideCanvasGroup(countdownPanel);
    }
    #endregion

    /// <summary>
    /// INTERNALS
    /// </summary>
    private static ClientComponentSwitcher GetLocalSwitcher()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.Log("[UI] GetLocalSwitcher: NetworkManager.Singleton is NULL");
            return null;
        }

        var po = nm.LocalClient?.PlayerObject;
        if (po == null)
        {
            Debug.Log("[UI] GetLocalSwitcher: LocalClient.PlayerObject is NULL");
            return null;
        }

        var sw = po.GetComponent<ClientComponentSwitcher>();
        Debug.Log($"[UI] GetLocalSwitcher: switcher={(sw ? "OK" : "NULL")}");
        return sw;
    }


    // Registers the local player with the HUD.
    public void RegisterPlayer(GameObject player)
    {
        if (playerHUD != null)
        {
            playerHUD.BindPlayer(player);
            ShowHUD();
        }
    }

    // Registers the local player's weapon with the HUD.
    public void RegisterWeapon(WeaponController weapon)
    {
        if (playerHUD != null)
        {
            playerHUD.BindWeapon(weapon);
            ShowHUD();
        }
    }

}