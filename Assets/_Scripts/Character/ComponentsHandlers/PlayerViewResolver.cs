using Unity.Netcode;
using UnityEngine;

public class PlayerViewResolver : NetworkBehaviour
{
    [Header("Visual Roots (NO COLLIDERS INSIDE)")]
    [SerializeField] private GameObject fullBodyVisual;       // your "Fullbody"
    [SerializeField] private GameObject firstPersonArmsVisual; // your "Arms"

    // Local cached state so we can re-apply consistently
    private bool _isAlive = true;

    public override void OnNetworkSpawn()
    {
        ApplyView();
    }

    /// <summary>
    /// Called on clients to update alive/dead presentation.
    /// This does NOT decide teams or gameplay; it is purely view/visibility.
    /// </summary>
    public void ClientSetAlive(bool alive)
    {
        _isAlive = alive;
        ApplyView();
    }

    private void ApplyView()
    {
        // If dead: hide EVERYTHING visual for everyone.
        if (!_isAlive)
        {
            if (fullBodyVisual) fullBodyVisual.SetActive(false);
            if (firstPersonArmsVisual) firstPersonArmsVisual.SetActive(false);
            return;
        }

        // Alive: normal owner vs remote split
        if (IsOwner)
        {
            // Local FPS: arms only
            if (fullBodyVisual) fullBodyVisual.SetActive(false);
            if (firstPersonArmsVisual) firstPersonArmsVisual.SetActive(true);
        }
        else
        {
            // Remote 3P: full body only
            if (fullBodyVisual) fullBodyVisual.SetActive(true);
            if (firstPersonArmsVisual) firstPersonArmsVisual.SetActive(false);
        }
    }
}
