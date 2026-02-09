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

    private void Awake()
    {
        _switcher = GetComponent<ClientComponentSwitcher>();
        _view = GetComponent<PlayerViewResolver>();
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
                    _switcher.SetState_Loadout();
                    _switcher.SetWeaponGameplayEnabled(false);
                }
                else
                {
                    _switcher.SetWeaponGameplayEnabled(true);
                }
            }
        }
    }
}
