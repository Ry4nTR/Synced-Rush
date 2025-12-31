using UnityEngine;
using Unity.Netcode;

public class WeaponSelectorPanel : MonoBehaviour
{
    private PlayerInputHandler inputHandler;
    private ClientComponentSwitcher componentSwitcher;
    private UIManager uIManager;

    private void Awake()
    {
        uIManager = UIManager.Instance;
    }
    private void Update()
    {
        TryBindPlayer();
    }

    private void TryBindPlayer()
    {
        // Netcode shutting down or not initialized
        if (NetworkManager.Singleton == null) return;

        // Not connected as client
        var player = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (player == null)
            return;

        inputHandler = player.GetComponent<PlayerInputHandler>();
        componentSwitcher = player.GetComponent<ClientComponentSwitcher>();

        if (inputHandler != null)
            inputHandler.OnToggleWeaponPanelEvent += ToggleInGame;
    }

    // =========================
    // IN-GAME TOGGLE
    // =========================
    private void ToggleInGame()
    {
        uIManager.ShowWeaponSelector();
        componentSwitcher?.EnableUI();
    }

    // =========================
    // WEAPON SELECTION
    // =========================
    public void SelectWeapon(int weaponId)
    {
        // Update local selection
        LocalWeaponSelection.SelectedWeaponId = weaponId;

        bool isInGame =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsConnectedClient &&
            NetworkManager.Singleton.LocalClient?.PlayerObject != null;

        // LOBBY BEHAVIOUR
        if (!isInGame) return;

        // IN-GAME BEHAVIOUR
        var loadout = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<WeaponLoadoutState>();

        loadout?.RequestEquip(weaponId);

        uIManager.HideWeaponSelector();
        uIManager.ShowHUD();
        componentSwitcher?.EnableGameplay();
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
            inputHandler.OnToggleWeaponPanelEvent -= ToggleInGame;
    }
}
