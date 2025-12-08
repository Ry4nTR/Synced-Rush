using Unity.Netcode;
using UnityEngine;

public class PlayerViewResolver : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject fullBodyModel;
    [SerializeField] private GameObject firstPersonArms;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Local Player
            fullBodyModel.SetActive(false);
            firstPersonArms.SetActive(true);
        }
        else
        {
            // Remote Player
            fullBodyModel.SetActive(true);
            firstPersonArms.SetActive(false);
        }
    }
}
