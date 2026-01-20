using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A simplified weapon selection panel that uses the legacy UIManager.  This
/// version is intended for development/testing scenes where the
/// GameplayUIManager is not used.  It shows/hides the weapon selector
/// through UIManager and toggles gameplay input state accordingly.
/// </summary>
public class DevWeaponSelectorPanel : MonoBehaviour
{
    [SerializeField]
    private int defaultWeaponId = 0;

    private UIManager ui;
    private ClientComponentSwitcher componentSwitcher;
    private PlayerInput playerInput;
    private bool isOpen;

    private void Start()
    {
        ui = UIManager.Instance;
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null) return;

        componentSwitcher = player.GetComponent<ClientComponentSwitcher>();
        playerInput = player.GetComponent<PlayerInput>();

        // Bind the toggle action to open/close the panel during gameplay.
        var action = playerInput.actions["ToggleWeaponPanel"];
        action.performed += OnTogglePerformed;
    }

    private void OnDestroy()
    {
        if (playerInput != null)
        {
            var action = playerInput.actions["ToggleWeaponPanel"];
            action.performed -= OnTogglePerformed;
        }
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (!isOpen)
        {
            OpenPanel();
        }
        else
        {
            ClosePanel();
        }
    }

    private void OpenPanel()
    {
        if (ui == null) return;
        ui.ShowWeaponSelector();
        ui.HideHUD();
        componentSwitcher?.SetState_Loadout();
        isOpen = true;
    }

    private void ClosePanel()
    {
        if (ui == null) return;
        ui.HideWeaponSelector();
        ui.ShowHUD();
        componentSwitcher?.SetState_Gameplay();
        isOpen = false;
    }

    /// <summary>
    /// Invoked by buttons to select a weapon.  Updates the local
    /// selection, requests equip on the server, and closes the panel.
    /// </summary>
    public void SelectWeapon(int weaponId)
    {
        LocalWeaponSelection.SelectedWeaponId = weaponId;
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player != null)
        {
            var loadout = player.GetComponent<WeaponLoadoutState>();
            loadout?.RequestEquip(weaponId);
        }
        ClosePanel();
    }

    /// <summary>
    /// Called when the pre‑round countdown finishes.  Equips the
    /// default weapon if none was selected and closes the panel.
    /// </summary>
    public void OnCountdownFinished()
    {
        if (LocalWeaponSelection.SelectedWeaponId < 0)
        {
            SelectWeapon(defaultWeaponId);
        }
        else
        {
            ClosePanel();
        }
    }
}