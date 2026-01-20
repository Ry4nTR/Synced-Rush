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
    // Tracks whether the selector is currently used in a pre‑round state.
    private bool inPreRound = false;

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
        // During pre‑round, the panel starts open. Pressing the toggle should
        // allow the player to close the panel early and return to gameplay.
        if (inPreRound)
        {
            if (isOpen)
            {
                uiManager.HideLoadoutPanel();
                uiManager.ShowHUD();
                // Switch back to gameplay mode on toggle close
                componentSwitcher?.SetState_Gameplay();
                isOpen = false;
                // Exit pre‑round early so further toggles behave normally
                inPreRound = false;
            }
            else
            {
                // If the panel is closed during pre‑round, we treat this as a no‑op
                return;
            }
            return;
        }

        // Live round toggle behaviour
        if (!isOpen)
        {
            uiManager.ShowLoadoutPanel();
            uiManager.HideHUD();
            // Switch the local player into UI mode so they can click buttons
            componentSwitcher?.SetState_Loadout();
            isOpen = true;
        }
        else
        {
            uiManager.HideLoadoutPanel();
            uiManager.ShowHUD();
            // Switch back to gameplay mode on toggle close
            componentSwitcher?.SetState_Gameplay();
            isOpen = false;
        }
    }

    // Called by GameplayUIManager when the pre‑round countdown starts.
    public void OpenPreRound()
    {
        // Reset any previous selection before starting a new pre‑round.
        LocalWeaponSelection.SelectedWeaponId = -1;

        // Show the loadout panel and hide the HUD during pre‑round selection.
        uiManager.ShowLoadoutPanel();
        uiManager.HideHUD();

        // Mark that we are in pre‑round so toggle hotkeys are disabled
        inPreRound = true;
        isOpen = true;
    }

    // Called when the player clicks a weapon
    public void SelectWeapon(int weaponId)
    {
        // Store the selected weapon locally so it can be applied when the player object spawns.
        LocalWeaponSelection.SelectedWeaponId = weaponId;

        // If the local player object already exists, immediately request the equip; otherwise
        // WeaponLoadoutState.OnNetworkSpawn will pick up the selection later.
        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player != null)
        {
            var loadout = player.GetComponent<WeaponLoadoutState>();
            loadout?.RequestEquip(weaponId);
        }
    }

    // Called when the countdown finishes
    public void OnCountdownFinished()
    {
        // When the countdown finishes the RoundManager will switch inputs.
        if (LocalWeaponSelection.SelectedWeaponId < 0)
        {
            // Equip default weapon and close the panel
            LocalWeaponSelection.SelectedWeaponId = defaultWeaponId;
            var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (player != null)
            {
                var loadout = player.GetComponent<WeaponLoadoutState>();
                loadout?.RequestEquip(defaultWeaponId);
            }
        }
        // Hide UI and reset pre‑round flag
        uiManager.HideLoadoutPanel();
        uiManager.ShowHUD();
        inPreRound = false;
        isOpen = false;
    }
}
