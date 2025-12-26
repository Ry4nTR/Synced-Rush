using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private CanvasGroup connectionPanel;
    [SerializeField] private CanvasGroup weaponSelectorPanel;
    [SerializeField] private CanvasGroup hudPanel;

    [Header("HUD")]
    [SerializeField] private PlayerHUD playerHUD;

    private WeaponController weapon;
    private HealthSystem health;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // START STATE
        Show(connectionPanel);
        Show(weaponSelectorPanel);
        Hide(hudPanel);
    }

    // =========================
    // PANEL CONTROL (LAYERED)
    // =========================

    public void HideConnection()
    {
        Hide(connectionPanel);
    }

    public void HideWeaponSelector()
    {
        Hide(weaponSelectorPanel);
    }

    public void ShowWeaponSelector()
    {
        Show(weaponSelectorPanel);
    }

    public void ShowHUD()
    {
        Show(hudPanel);
    }

    private void Show(CanvasGroup cg)
    {
        if (!cg) return;
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    private void Hide(CanvasGroup cg)
    {
        if (!cg) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    // =========================
    // PLAYER DATA
    // =========================

    public void RegisterPlayer(GameObject player)
    {
        playerHUD.BindPlayer(player);

        ShowHUD();
    }
    public void RegisterWeapon(WeaponController weapon)
    {
        playerHUD.BindWeapon(weapon);

        ShowHUD();
    }
}
