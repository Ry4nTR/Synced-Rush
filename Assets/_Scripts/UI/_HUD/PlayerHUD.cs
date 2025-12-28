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

    // =========================
    // INITIALIZATION
    // =========================

    /// <summary>
    /// Called once by UIManager when the local player is known.
    /// </summary>
    public void BindPlayer(GameObject player)
    {
        health = player.GetComponent<HealthSystem>();

        UpdateHealth();

        // Subscribe to networked health changes
        if (health != null)
            health.currentHealth.OnValueChanged += OnHealthChanged;
    }

    public void BindWeapon(WeaponController weaponController)
    {
        weapon = weaponController;
        UpdateAmmo();
    }


    private void OnDestroy()
    {
        if (health != null)
            health.currentHealth.OnValueChanged -= OnHealthChanged;
    }

    // =========================
    // UPDATE LOOP
    // =========================

    private void Update()
    {
        // Ammo is local-only → safe and cheap to update per frame
        if (weapon != null)
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

    // UpdateHelth not casted to int to show decimal health values
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
