using Unity.Netcode;
using UnityEngine;

public class PlayerViewResolver : NetworkBehaviour
{
    [Header("Visual Roots (NO COLLIDERS INSIDE)")]
    [SerializeField] private GameObject fullBodyVisual;
    [SerializeField] private GameObject firstPersonArmsVisual;

    public override void OnNetworkSpawn()
    {
        // Visual logic is CLIENT-ONLY
        if (!IsClient)
            return;

        ResolveView();
    }

    private void ResolveView()
    {
        if (IsLocalPlayer)
        {
            // Local player (FPS view)
            SetFullBodyVisible(false);
            SetArmsVisible(true);
        }
        else
        {
            // Remote player (3P view)
            SetFullBodyVisible(true);
            SetArmsVisible(false);
        }
    }

    private void SetFullBodyVisible(bool value)
    {
        if (!fullBodyVisual)
            return;

        fullBodyVisual.SetActive(value);
    }

    private void SetArmsVisible(bool value)
    {
        if (!firstPersonArmsVisual)
            return;

        firstPersonArmsVisual.SetActive(value);
    }
}
