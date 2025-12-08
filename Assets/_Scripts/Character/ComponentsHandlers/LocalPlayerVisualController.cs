using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class LocalPlayerVisualController : NetworkBehaviour
{
    [SerializeField] private GameObject arms;
    [SerializeField] private GameObject fullBody;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            arms.SetActive(true);
            fullBody.SetActive(false);
        }
        else
        {
            arms.SetActive(false);
            fullBody.SetActive(true);
        }
    }
}
