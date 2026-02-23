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

    private LobbyManager lobbyManager;
    private NetworkLobbyState lobbyState;

    private void OnEnable()
    {
        Bind();
        SessionServices.OnReady += OnSessionReady;
    }

    private void OnDisable()
    {
        SessionServices.OnReady -= OnSessionReady;
    }

    private void OnSessionReady(SessionServices s) => Bind();

    private void Bind()
    {
        var s = SessionServices.Current;
        if (s == null) return;

        lobbyManager = s.LobbyManager;
        lobbyState = s.LobbyState;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        if (lobbyManager == null || lobbyState == null)
            return;

        var item = eventData.pointerDrag
            ? eventData.pointerDrag.GetComponent<LobbyPlayerItemUI>()
            : null;

        if (item == null) return;

        lobbyManager.SetPlayerTeam(item.ClientId, teamId);

        // If you want the UI to “snap” immediately like before:
        Destroy(item.gameObject);
    }
}