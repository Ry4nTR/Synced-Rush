using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class TeamDropZone : MonoBehaviour, IDropHandler
{
    [Header("Drop Zone")]
    [SerializeField] private int teamId = 0;
    [SerializeField] private Transform container;

    public int TeamId => teamId;
    public Transform Container => container;

    [Header("Services (assign in Inspector)")]
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private NetworkLobbyState lobbyState;

    private void Awake()
    {
        // Optional fallbacks (remove if you want strict inspector-only)
        if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (lobbyState == null) lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        var item = eventData.pointerDrag
            ? eventData.pointerDrag.GetComponent<LobbyPlayerItemUI>()
            : null;

        if (item == null) return;
        if (lobbyManager == null || lobbyState == null) return;

        // Find current team
        int currentTeam = -999;
        var players = lobbyState.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == item.ClientId)
            {
                currentTeam = players[i].teamId;
                break;
            }
        }

        // Dropped into same team -> do nothing
        if (currentTeam == teamId)
            return;

        // Assign on server
        lobbyManager.SetPlayerTeam(item.ClientId, teamId);

        // Remove dragged visual (UI will rebuild from NetworkList)
        Destroy(item.gameObject);
    }
}
