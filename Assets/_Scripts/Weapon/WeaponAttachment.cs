using Unity.Netcode;
using UnityEngine;

public class WeaponAttachment : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Get the player owning the weapon
        var ownerObject = NetworkManager.Singleton.ConnectedClients[OwnerClientId].PlayerObject;
        Transform socket;

        if (IsOwner)
        {
            // FIRST PERSON SOCKET
            socket = ownerObject.transform.Find("WeaponSocket_FP");
            Instantiate(ownerObject.GetComponent<WeaponController>().weaponData.viewModelPrefab, socket);
        }
        else
        {
            // THIRD PERSON SOCKET
            socket = ownerObject.transform.Find("WeaponSocket");
            Instantiate(ownerObject.GetComponent<WeaponController>().weaponData.worldModelPrefab, socket);
        }
    }
}
