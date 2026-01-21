using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dev-only HUD/controller.
/// - Shows input/debug status on screen
/// - Ensures legacy UIManager binds the local player (and current weapon if found)
///
/// Add this to your DevScene Canvas.
/// Assign the TMP_Text fields (optional).
/// </summary>
public class DevHUD : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] private TMP_Text actionMapText;
    [SerializeField] private TMP_Text weaponToggleText;
    [SerializeField] private TMP_Text exitToggleText;

    [Header("Legacy UI (optional)")]
    [SerializeField] private UIManager uiManager;

    private PlayerInput _playerInput;
    private WeaponController _lastWeapon;
    private float _nextRescan;

    private void Start()
    {
        if (uiManager == null)
            uiManager = UIManager.Instance;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // If we are already connected (host) try immediately
        TryBind();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        TryBind();
    }

    private void TryBind()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null) return;

        _playerInput = player.GetComponent<PlayerInput>();

        // Bind legacy HUD
        if (uiManager != null)
            uiManager.UIRegisterPlayer(player.gameObject);

        // Weapon bind (best-effort)
        RescanWeapon(player);
    }

    private void Update()
    {
        if (_playerInput == null)
        {
            // Try again occasionally (player object may spawn later)
            if (Time.unscaledTime >= _nextRescan)
            {
                _nextRescan = Time.unscaledTime + 0.5f;
                TryBind();
            }
            return;
        }

        if (actionMapText != null)
            actionMapText.text = $"ActionMap: {_playerInput.currentActionMap?.name ?? "NULL"}";

        var toggleWeapon = _playerInput.actions?["ToggleWeaponPanel"];
        if (weaponToggleText != null)
        {
            string state = toggleWeapon == null ? "MISSING" : (toggleWeapon.enabled ? "ENABLED" : "DISABLED");
            weaponToggleText.text = $"ToggleWeaponPanel: {state}";
        }

        var toggleExit = _playerInput.actions?["ToggleExitPanel"];
        if (exitToggleText != null)
        {
            string state = toggleExit == null ? "MISSING" : (toggleExit.enabled ? "ENABLED" : "DISABLED");
            exitToggleText.text = $"ToggleExitPanel: {state}";
        }

        // Rebind weapon if changed/spawned
        if (Time.unscaledTime >= _nextRescan)
        {
            _nextRescan = Time.unscaledTime + 0.5f;
            var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (player != null)
                RescanWeapon(player);
        }
    }

    private void RescanWeapon(NetworkObject player)
    {
        if (uiManager == null) return;

        var wc = player.GetComponentInChildren<WeaponController>(true);
        if (wc != null && wc != _lastWeapon)
        {
            _lastWeapon = wc;
            uiManager.UIRegisterWeapon(wc);
        }
    }
}
