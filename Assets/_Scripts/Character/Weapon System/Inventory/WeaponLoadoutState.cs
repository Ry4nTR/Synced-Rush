using Unity.Netcode;

/// <summary>
/// Owns the networked weapon loadout state.
/// </summary>
public class WeaponLoadoutState : NetworkBehaviour
{
    public NetworkVariable<int> EquippedWeaponId = new NetworkVariable<int>(
        -1, // -1 = no weapon
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner && LocalWeaponSelection.SelectedWeaponId > 0)
            RequestEquipServerRpc(LocalWeaponSelection.SelectedWeaponId);
    }

    //Called by WeaponSelectorPanel
    public void RequestEquip(int weaponId)
    {
        if (!IsOwner && !IsServer) return;

        RequestEquipServerRpc(weaponId);
    }

    // Request client to server to equip a weapon
    [ServerRpc]
    private void RequestEquipServerRpc(int weaponId, ServerRpcParams rpcParams = default)
    {
        EquippedWeaponId.Value = weaponId;
    }
}
