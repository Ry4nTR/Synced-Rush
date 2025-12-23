using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WeaponSelectButton : MonoBehaviour
{
    [SerializeField] private int weaponId;
    [SerializeField] private WeaponSelectorPanel panel;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        panel.SelectWeapon(weaponId);
    }
}
