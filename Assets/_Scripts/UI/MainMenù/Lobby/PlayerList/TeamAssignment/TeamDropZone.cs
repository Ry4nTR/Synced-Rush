using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class TeamDropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private int teamId = 0;
    [SerializeField] private Transform container;

    public int TeamId => teamId;
    public Transform Container => container;

    public void OnDrop(PointerEventData eventData)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsHost)
            return;

        var item = eventData.pointerDrag
            ? eventData.pointerDrag.GetComponent<LobbyPlayerItemUI>()
            : null;

        if (item == null) return;
        if (LobbyManager.Instance == null) return;
        if (NetworkLobbyState.Instance == null) return;

        // Find current team of the dragged player
        int currentTeam = -999;
        var players = NetworkLobbyState.Instance.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == item.ClientId)
            {
                currentTeam = players[i].teamId;
                break;
            }
        }

        // If you dropped into the same team, do nothing (and DON'T destroy)
        if (currentTeam == teamId)
        {
            // Return to original parent (EndDrag handles this)
            return;
        }

        // Assign team on server
        LobbyManager.Instance.SetPlayerTeam(item.ClientId, teamId);

        // Destroy dragged visual so we don't see duplicates while UI rebuilds
        Destroy(item.gameObject);
    }
}
