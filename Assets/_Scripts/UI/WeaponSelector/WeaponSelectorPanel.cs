using UnityEngine;
using Unity.Netcode;

public class WeaponSelectorPanel : MonoBehaviour
{
    private PlayerInputHandler inputHandler;
    private ClientComponentSwitcher componentSwitcher;

    private void Update()
    {
        TryBindPlayer();
    }

    private void TryBindPlayer()
{
    // Already bound → stop
    if (inputHandler != null)
        return;

    // Netcode shutting down or not initialized
    if (NetworkManager.Singleton == null)
        return;

    if (!NetworkManager.Singleton.IsClient)
        return;

    var localClient = NetworkManager.Singleton.LocalClient;
    if (localClient == null)
        return;

    var player = localClient.PlayerObject;
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
        UIManager.Instance.ShowWeaponSelector();
        componentSwitcher?.EnableUI();
    }

    // =========================
    // WEAPON SELECTION
    // =========================

    public void SelectWeapon(int weaponId)
    {
        // Always update local selection
        LocalWeaponSelection.SelectedWeaponId = weaponId;

        bool isInGame =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsConnectedClient &&
            NetworkManager.Singleton.LocalClient?.PlayerObject != null;

        // -------------------------
        // LOBBY BEHAVIOUR
        // -------------------------
        if (!isInGame)
        {
            // DO NOTHING ELSE
            // Panel stays open, player can click forever
            return;
        }

        // -------------------------
        // IN-GAME BEHAVIOUR
        // -------------------------
        var loadout = NetworkManager.Singleton.LocalClient.PlayerObject
            .GetComponent<WeaponLoadoutState>();

        loadout?.RequestEquip(weaponId);

        UIManager.Instance.HideWeaponSelector();
        UIManager.Instance.ShowHUD();
        componentSwitcher?.EnableGameplay();
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
            inputHandler.OnToggleWeaponPanelEvent -= ToggleInGame;
    }
}
