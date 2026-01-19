using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles the weapon selection interface for the local player.
/// </summary>
public class WeaponSelectorPanel : MonoBehaviour
{
    [SerializeField] private int defaultWeaponId = 0;

    private PlayerInput playerInput;
    private ClientComponentSwitcher componentSwitcher;
    private GameplayUIManager uiManager;
    private bool isOpen;

    private void Start()
    {
        uiManager = GameplayUIManager.Instance;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;
        TryBindPlayer();
    }

    private void TryBindPlayer()
    {
        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player == null) return;

        playerInput = player.GetComponent<PlayerInput>();
        componentSwitcher = player.GetComponent<ClientComponentSwitcher>();

        var action = playerInput.actions["ToggleWeaponPanel"];
        action.performed -= OnTogglePerformed;
        action.performed += OnTogglePerformed;
    }

    // Toggle loadout panel during a live match
    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (!isOpen)
        {
            uiManager.ShowLoadoutPanel();
            uiManager.HideHUD();
            componentSwitcher?.SetState_Loadout();
            isOpen = true;
        }
        else
        {
            uiManager.HideLoadoutPanel();
            uiManager.ShowHUD();
            componentSwitcher?.SetState_Gameplay();
            isOpen = false;
        }
    }

    // Called by GameplayUIManager when the pre‑round countdown starts
    public void OpenPreRound()
    {
        // Enter UI mode so the player can interact with the panel.
        componentSwitcher?.SetState_Loadout();

        // Show the loadout panel and hide the HUD during pre‑round selection.
        uiManager.ShowLoadoutPanel();
        uiManager.HideHUD();

        isOpen = true;
    }

    // Called when the player clicks a weapon
    public void SelectWeapon(int weaponId)
    {
        LocalWeaponSelection.SelectedWeaponId = weaponId;

        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player != null)
        {
            var loadout = player.GetComponent<WeaponLoadoutState>();
            loadout?.RequestEquip(weaponId);
        }

        uiManager.HideLoadoutPanel();
        uiManager.ShowHUD();
        componentSwitcher?.SetState_Gameplay();
        isOpen = false;
    }

    // Called when the countdown finishes
    public void OnCountdownFinished()
    {
        if (LocalWeaponSelection.SelectedWeaponId < 0)
        {
            SelectWeapon(defaultWeaponId);
        }
        else
        {
            // No selection was needed because the player already chose a
            // weapon.  Hide the panel and switch back to gameplay input.
            componentSwitcher?.SetState_Gameplay();
            uiManager.HideLoadoutPanel();
            uiManager.ShowHUD();
            isOpen = false;
        }
    }
}
