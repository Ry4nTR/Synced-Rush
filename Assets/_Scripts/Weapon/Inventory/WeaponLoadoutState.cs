using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owns the networked weapon loadout state.
/// NO automatic initialization.
/// Weapon is equipped ONLY via explicit ServerRpc requests.
/// </summary>
public class WeaponLoadoutState : NetworkBehaviour
{
    // -1 means: no weapon equipped
    public NetworkVariable<int> EquippedWeaponId = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Apply pre-lobby selection AFTER the player exists
        if (IsOwner && LocalWeaponSelection.SelectedWeaponId > 0)
            RequestEquipServerRpc(LocalWeaponSelection.SelectedWeaponId);
    }

    /// <summary>
    /// Called by UI / owning client to equip a weapon.
    /// </summary>
    public void RequestEquip(int weaponId)
    {
        // Allow owner OR server (host case)
        if (!IsOwner && !IsServer)
        {
            return;
        }

        RequestEquipServerRpc(weaponId);
    }

    [ServerRpc]
    private void RequestEquipServerRpc(int weaponId, ServerRpcParams rpcParams = default)
    {
        EquippedWeaponId.Value = weaponId;
    }
}
