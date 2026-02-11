using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using SyncedRush.Character.Movement;

public class LoadoutSelectorPanel : MonoBehaviour
{
    [SerializeField] private int defaultWeaponId = 0;
    [SerializeField] private CharacterAbility defaultAbility = CharacterAbility.None;

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

            uiManager.HideLoadoutPanel();
            uiManager.ShowHUD();

            // Keep loadout input state during pre-round
            componentSwitcher?.SetState_Loadout();

            isOpen = false;

            // IMPORTANT: do NOT set inPreRound=false here
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
        // Persist selection across rounds.
        // Only assign defaults if player never selected anything yet.

        if (LocalWeaponSelection.SelectedWeaponId < 0)
            LocalWeaponSelection.SelectedWeaponId = defaultWeaponId;

        if (LocalAbilitySelection.SelectedAbility == CharacterAbility.None)
            LocalAbilitySelection.SelectedAbility = defaultAbility;

        uiManager.ShowLoadoutPanel();
        uiManager.HideHUD();

        inPreRound = true;
        isOpen = true;

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

        uiManager.HideLoadoutPanel();
        uiManager.ShowHUD();
        inPreRound = false;
        isOpen = false;
    }
}
