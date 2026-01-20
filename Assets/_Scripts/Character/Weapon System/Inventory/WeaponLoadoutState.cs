using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owns the networked weapon loadout state.
/// </summary>
public class WeaponLoadoutState : NetworkBehaviour
{
    public NetworkVariable<int> EquippedWeaponId = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner && LocalWeaponSelection.SelectedWeaponId >= 0)
            RequestEquipServerRpc(LocalWeaponSelection.SelectedWeaponId);
    }

    //Called by WeaponSelectorPanel
    public void RequestEquip(int weaponId)
    {
        if (!IsOwner && !IsServer) return;

        Debug.Log($"[WeaponLoadoutState] RequestEquip({weaponId}) IsOwner={IsOwner} IsServer={IsServer} OwnerClientId={OwnerClientId}", this);

        RequestEquipServerRpc(weaponId);
    }

    // Request client to server to equip a weapon
    [ServerRpc]
    private void RequestEquipServerRpc(int weaponId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[WeaponLoadoutState] ServerRpc RequestEquipServerRpc({weaponId}) from sender={rpcParams.Receive.SenderClientId} on server IsServer={IsServer}", this);

        EquippedWeaponId.Value = weaponId;

        Debug.Log($"[WeaponLoadoutState] EquippedWeaponId set to {EquippedWeaponId.Value} (server)", this);
    }
}
