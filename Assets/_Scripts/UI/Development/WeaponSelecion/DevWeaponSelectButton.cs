using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev-only button used for selecting a weapon in development scenes.  This
/// mirrors the behaviour of WeaponSelectButton but references the
/// DevWeaponSelectorPanel instead of the gameplay weapon selector.  The
/// DevWeaponSelectorPanel handles registration with the legacy UIManager.
/// </summary>
[RequireComponent(typeof(Button))]
public class DevWeaponSelectButton : MonoBehaviour
{
    [SerializeField] private int weaponId;
    [SerializeField] private DevWeaponSelectorPanel panel;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (panel != null)
            panel.SelectWeapon(weaponId);
    }
}