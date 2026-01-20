using UnityEngine;
using TMPro;

/// <summary>
/// Owns and updates all HUD-related visuals for the local player.
/// This includes ammo, health, and any future HUD values.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text healthText;

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
        Debug.Log($"[PlayerHUD] BindPlayer player={(player ? player.name : "NULL")} health={(player ? player.GetComponent<HealthSystem>() : null)} healthText={(healthText ? "OK" : "NULL")}", this);

        if (health != null)
            health.currentHealth.OnValueChanged -= OnHealthChanged;

        health = player.GetComponent<HealthSystem>();

        if (health != null)
            health.currentHealth.OnValueChanged += OnHealthChanged;

        UpdateHealth();
    }

    public void BindWeapon(WeaponController weaponController)
    {
        Debug.Log($"[PlayerHUD] BindWeapon weapon={(weaponController ? weaponController.name : "NULL")} ammoText={(ammoText ? "OK" : "NULL")}", this);

        weapon = weaponController;
        UpdateAmmo();
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
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        UpdateHealth();
    }
}
