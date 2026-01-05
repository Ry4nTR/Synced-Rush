using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSelectorPanel : MonoBehaviour
{
    private PlayerInput playerInput;
    private ClientComponentSwitcher componentSwitcher;
    private UIManager uiManager;

    private bool isOpen;

    private void Start()
    {
        uiManager = UIManager.Instance;

        // Bind when a client connects
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
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

        // bind once
        var action = playerInput.actions["ToggleWeaponPanel"];
        action.performed -= OnTogglePerformed;
        action.performed += OnTogglePerformed;
    }

    // =========================
    // IN-GAME TOGGLE
    // =========================
    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        ToggleInGame();
    }
    private void ToggleInGame()
    {
        if (!isOpen)
        {
            uiManager.ShowWeaponSelector();
            uiManager.HideHUD();

            componentSwitcher?.EnableUI();

            isOpen = true;
        }
        else
        {
            uiManager.HideWeaponSelector();
            uiManager.ShowHUD();

            componentSwitcher?.EnableGameplay();

            isOpen = false;
        }
    }

    // =========================
    // WEAPON SELECTION
    // =========================
    public void SelectWeapon(int weaponId)
    {
        LocalWeaponSelection.SelectedWeaponId = weaponId;

        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player == null) return;

        var loadout = player.GetComponent<WeaponLoadoutState>();
        loadout?.RequestEquip(weaponId);

        uiManager.HideWeaponSelector();
        uiManager.ShowHUD();
        componentSwitcher?.EnableGameplay();

        isOpen = false;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}
