using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays simple weapon ammo information on a UI Text element.
/// Attach this to a Canvas GameObject and assign the weapon and text fields.
/// The HUD will update every frame with the current and reserve ammo.
/// </summary>
public class WeaponHUD : MonoBehaviour
{
    [Tooltip("The weapon controller whose ammo should be displayed.")]
    public WeaponController weapon;
    [Tooltip("UI Text element that shows the ammo count.")]
    public Text ammoText;

    private void Update()
    {
        if (weapon == null || ammoText == null)
            return;
        ammoText.text = $"{weapon.CurrentAmmo} / {weapon.ReserveAmmo}";
    }
}