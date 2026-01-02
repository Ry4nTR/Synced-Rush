using UnityEngine;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private CanvasGroup connectionPanel;
    [SerializeField] private CanvasGroup weaponSelectorPanel;
    [SerializeField] private CanvasGroup hudPanel;

    [Header("HUD")]
    [SerializeField] private PlayerHUD playerHUD;

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
    // BASIC METHOD FOR PANEL CONTROL
    // =========================
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
    public void HideHUD()
    {
        Hide(hudPanel);
    }

    // =========================
    // REGISTRATION PLAYER AND WEAPON
    // =========================

    public void UIRegisterPlayer(GameObject player)
    {
        playerHUD.BindPlayer(player);

        ShowHUD();
    }
    public void UIRegisterWeapon(WeaponController weapon)
    {
        playerHUD.BindWeapon(weapon);

        ShowHUD();
    }
}