using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Handles the weapon selection UI panel.
/// Opens/closes via PlayerInputHandler (ToggleWeaponPanel action),
/// supports pre-lobby selection and in-game weapon switching.
/// </summary>
public class WeaponSelectorPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool openAtStart = true;

    private PlayerInputHandler inputHandler;
    private ClientComponentSwitcher componentSwitcher;

    private bool isOpen;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (openAtStart)
            OpenInitial();
        else
            Hide();
    }

    private void Update()
    {
        TryBindPlayer();
    }

    /// <summary>
    /// Binds PlayerInputHandler and ClientComponentSwitcher
    /// once the local player exists.
    /// </summary>
    private void TryBindPlayer()
    {
        if (inputHandler != null)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            return;

        var player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player == null)
            return;

        inputHandler = player.GetComponent<PlayerInputHandler>();
        componentSwitcher = player.GetComponent<ClientComponentSwitcher>();

        if (inputHandler != null)
        {
            inputHandler.OnToggleWeaponPanelEvent += TogglePanel;
        }
    }

    // =========================
    // PANEL TOGGLING
    // =========================

    private void TogglePanel()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    private void OpenInitial()
    {
        isOpen = true;
        Show();

        // Pre-lobby state: no player yet
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Open()
    {
        isOpen = true;
        Show();

        // In-game: disable gameplay input & camera
        componentSwitcher?.EnableUI();
    }

    private void Close()
    {
        isOpen = false;
        Hide();

        // In-game: re-enable gameplay input & camera
        componentSwitcher?.EnableGameplay();
    }

    /// <summary>
    /// Used by LANConnectionUI when Host/Client is pressed.
    /// Does not assume player exists.
    /// </summary>
    public void ForceClose()
    {
        isOpen = false;
        Hide();
    }

    // =========================
    // UI VISIBILITY
    // =========================

    private void Show()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // =========================
    // WEAPON SELECTION (called by buttons)
    // =========================

    public void SelectWeapon(int weaponId)
    {
        // Store selection locally (pre-lobby support)
        LocalWeaponSelection.SelectedWeaponId = weaponId;

        // If player exists, apply immediately
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsConnectedClient &&
            NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var loadout = NetworkManager.Singleton.LocalClient.PlayerObject
                .GetComponent<WeaponLoadoutState>();

            loadout?.RequestEquip(weaponId);

            Close();
        }
        else
        {
            Hide();
            isOpen = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }


    private void OnDestroy()
    {
        // Clean unsubscribe (important when leaving scene)
        if (inputHandler != null)
        {
            inputHandler.OnToggleWeaponPanelEvent -= TogglePanel;
        }
    }
}
