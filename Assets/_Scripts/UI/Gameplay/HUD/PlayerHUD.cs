using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Owns and updates all HUD-related visuals for the local player.
/// This includes ammo, health, and any future HUD values.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text healthText;

    [Header("UI Elements")]
    [SerializeField] private Image hpBar;
    [SerializeField] private Image jetpackBar;
    [SerializeField] private Image dashBar;

    [Header("Ability Elements")]
    [SerializeField] private GameObject jetpackAbilityElements;

    [Header("UI Elements Controllers")]
    [SerializeField] private DamageIndicatorController damageIndicatorController;

    // Cached gameplay references (LOCAL PLAYER ONLY)
    private WeaponController weapon;
    private HealthSystem health;

    private void Update()
    {
        UpdateAmmo();
    }
    private void OnDestroy()
    {
        if (health != null)
            health.currentHealth.OnValueChanged -= OnHealthChanged;
    }

    // =========================
    // BINDING PLAYER AND WEAPON
    // =========================

    public void BindPlayer(GameObject player)
    {
        if (health != null)
            health.currentHealth.OnValueChanged -= OnHealthChanged;

        health = player.GetComponent<HealthSystem>();

        if (health != null)
            health.currentHealth.OnValueChanged += OnHealthChanged;

        UpdateHealth();
    }

    public void BindWeapon(WeaponController weaponController)
    {
        weapon = weaponController;
        UpdateAmmo();
    }

    public void SetJetpackUIVisibility(bool value)
    {
        jetpackAbilityElements.SetActive(value);
    }

    public void UpdateDashCharge(float currentCharge, float maxCharge)
    {
        if (maxCharge != 0f)
        {
            dashBar.fillAmount = currentCharge / maxCharge;
        }
        else
            dashBar.fillAmount = 0f;
    }

    public void UpdateJetpackCharge(float currentCharge, float maxCharge)
    {
        if (maxCharge != 0f)
        {
            jetpackBar.fillAmount = currentCharge / maxCharge;
        }
        else
            jetpackBar.fillAmount = 0f;
    }
    // =========================
    // DAMAGE INDICATOR
    // =========================
    public void ShowDamageIndicator(Vector3 attackerPosition, Transform localPlayerTransform)
    {
        if (damageIndicatorController == null || localPlayerTransform == null) return;

        Vector3 playerPos = localPlayerTransform.position;
        Vector3 playerForward = localPlayerTransform.forward;

        damageIndicatorController.OnTakeDamage(playerPos, playerForward, attackerPosition);
    }

    // =========================
    // HUD UPDATES
    // =========================

    private void UpdateAmmo()
    {
        ammoText.text = weapon != null
            ? $"{weapon.CurrentAmmo} / {weapon.ReserveAmmo}"
            : "-- / --";
    }

    private void UpdateHealth()
    {
        healthText.text = health != null
            ? $"HP {health.currentHealth.Value:F1}"
            : "HP --";

        if (health.maxHealth != 0f)
        {
            hpBar.fillAmount = health.CurrentHealth / health.maxHealth;
        }
        else
            hpBar.fillAmount = 0f;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        UpdateHealth();
    }

}
