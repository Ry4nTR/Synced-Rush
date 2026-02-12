using Unity.Netcode;
using UnityEngine;

public class LocalClientSystemsBinder : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        var systems = FindFirstObjectByType<ClientSystems>();
        if (systems == null)
        {
            Debug.LogError("[LocalClientSystemsBinder] ClientSystems not found.");
            return;
        }

        // Inject into the scripts that need it
        var sw = GetComponent<ClientComponentSwitcher>();
        if (sw != null) sw.SetClientSystems(systems);
    }
}
