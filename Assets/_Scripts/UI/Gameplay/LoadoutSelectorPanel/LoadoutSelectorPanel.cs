using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using SyncedRush.Character.Movement;

public class LoadoutSelectorPanel : MonoBehaviour
{
    [SerializeField] private ClientSystems clientSystems;
    [SerializeField] private int defaultWeaponId = 0;
    [SerializeField] private CharacterAbility defaultAbility = CharacterAbility.None;

    private PlayerInput playerInput;
    private ClientComponentSwitcher componentSwitcher;
    private GameplayUIManager uiManager;
    private bool isOpen;
    // Tracks whether the selector is currently used in a pre‑round state.
    private bool inPreRound = false;

    public bool IsOpen => isOpen;
    public bool InPreRound => inPreRound;

    private void Start()
    {
        if (clientSystems == null)
            clientSystems = FindFirstObjectByType<ClientSystems>();

        uiManager = clientSystems != null ? clientSystems.UI : null;

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
        if (action != null)
        {
            // Ensure it works across action map switches (Gameplay/UI)
            action.Enable();
            action.performed -= OnTogglePerformed;
            action.performed += OnTogglePerformed;
        }
    }

    // Toggle loadout panel during a live match
    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        // During pre‑round, the panel starts open. Pressing the toggle should
        // allow the player to close the panel early and return to gameplay.
        if (inPreRound)
        {
            if (!isOpen) return;
            // Hide the loadout panel but remain in loadout mode.  Let the
            // UI manager update its own mode and input locks.  Do not
            // transition to gameplay; keep pre‑round active.
            uiManager?.SetLoadoutVisibility(false);

            // Keep loadout input state during pre‑round
            componentSwitcher?.SetState_Loadout();

            isOpen = false;

            // IMPORTANT: do NOT set inPreRound=false here
            return;
        }

        // Live round toggle behaviour
        if (!isOpen)
        {
            // Show the loadout panel and switch to UI mode.  The UI manager
            // will update its internal mode and input locks.
            uiManager?.SetLoadoutVisibility(true);
            // Switch the local player into UI mode so they can click buttons
            componentSwitcher?.SetState_Loadout();
            isOpen = true;
        }
        else
        {
            // Hide the loadout panel and return to gameplay.  The UI
            // manager will update its mode and input locks accordingly.
            uiManager?.SetLoadoutVisibility(false);
            // Switch back to gameplay mode on toggle close
            componentSwitcher?.SetState_Gameplay();
            isOpen = false;
        }
    }

    // Called by GameplayUIManager when the pre‑round countdown starts.
    public void OpenPreRound()
    {
        // Persist selection across rounds.
        // Only assign defaults if player never selected anything yet.

        if (LocalWeaponSelection.SelectedWeaponId < 0)
            LocalWeaponSelection.SelectedWeaponId = defaultWeaponId;

        if (LocalAbilitySelection.SelectedAbility == CharacterAbility.None)
            LocalAbilitySelection.SelectedAbility = defaultAbility;

        // The UI manager now handles showing/hiding panels and managing
        // input state for the pre‑round.  Simply mark our local state and
        // ensure the player is using the UI action map.
        inPreRound = true;
        isOpen = true;

        // Switch to the UI action map so that the player can select
        // weapons/abilities.  Movement remains disabled via the UI manager.
        componentSwitcher?.SetState_Loadout();
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

    // Called when the player clicks an ability button.
    public void SelectAbility(CharacterAbility ability)
    {
        LocalAbilitySelection.SelectedAbility = ability;

        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player != null)
        {
            var move = player.GetComponent<MovementController>();
            if (move != null)
            {
                // Instead of move.Ability.CurrentAbility = ability; (which is local only)
                // Use the new method we'll add to MovementController
                move.ChangeAbility(ability);
            }
        }
    }

    // Called when the countdown finishes
    public void OnCountdownFinished()
    {
        // Ensure networked equip + ability are applied (owner)
        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player != null)
        {
            var loadout = player.GetComponent<WeaponLoadoutState>();
            if (loadout != null && LocalWeaponSelection.SelectedWeaponId >= 0)
                loadout.RequestEquip(LocalWeaponSelection.SelectedWeaponId);

            var move = player.GetComponent<MovementController>();
            if (move != null && LocalAbilitySelection.SelectedAbility != CharacterAbility.None)
                move.ChangeAbility(LocalAbilitySelection.SelectedAbility);
        }

        // Let the UI manager handle showing/hiding panels.  Clear our local
        // state flags so the UI manager knows the pre‑round is finished.
        inPreRound = false;
        isOpen = false;
    }
}
