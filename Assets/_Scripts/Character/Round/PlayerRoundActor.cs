using Unity.Netcode;
using UnityEngine;

public class PlayerRoundActor : NetworkBehaviour
{
    [Header("Physics")]
    [SerializeField] private CharacterController characterController;

    [Header("Hit Colliders (optional)")]
    [SerializeField] private Collider[] hitColliders;

    private ClientComponentSwitcher _switcher;
    private PlayerViewResolver _view;
    private ClientSystems _clientSystems;

    private void Awake()
    {
        _switcher = GetComponent<ClientComponentSwitcher>();
        _view = GetComponent<PlayerViewResolver>();
    }

    public void SetClientSystems(ClientSystems systems)
    {
        _clientSystems = systems;
    }

    public void ServerSetAliveState(bool alive)
    {
        if (!IsServer) return;

        // Physical presence
        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;

        if (hitColliders != null)
        {
            for (int i = 0; i < hitColliders.Length; i++)
                if (hitColliders[i] != null) hitColliders[i].enabled = alive;
        }

        // Replicate view + local gameplay lock
        SetAliveStateClientRpc(alive);
    }

    [ClientRpc]
    private void SetAliveStateClientRpc(bool alive)
    {
        if (_view == null) _view = GetComponent<PlayerViewResolver>();
        if (_view != null) _view.ClientSetAlive(alive);

        var weaponSpawner = GetComponent<WeaponInventorySpawner>();
        if (weaponSpawner != null)
            weaponSpawner.SetWeaponVisualsAlive(alive);

        // Owner: toggle weapon gameplay lock BOTH ways
        if (IsOwner)
        {
            if (_switcher == null) _switcher = GetComponent<ClientComponentSwitcher>();

            if (_switcher != null)
            {
                if (!alive)
                {
                    var cam = ResolveLocalOutputCamera(); // add helper (same logic you had)
                    _clientSystems?.DeathCam?.CaptureDeathPose(cam);

                    _switcher.SetState_UIMenu();
                    _switcher.SetWeaponGameplayEnabled(false);
                }
                else
                {
                    _switcher.SetWeaponGameplayEnabled(true);
                    _clientSystems?.DeathCam?.StopKillcam();
                }
            }
        }
    }

    private Camera ResolveLocalOutputCamera()
    {
        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localPlayer != null)
        {
            var cams = localPlayer.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
                if (cams[i] != null && cams[i].enabled) return cams[i];
        }

        var all = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].enabled) return all[i];

        return Camera.main;
    }
}
