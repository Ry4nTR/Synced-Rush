using TMPro;
using UnityEngine;
using Unity.Netcode;

public class LobbyPlayerListUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI listText;
    [SerializeField] private NetworkLobbyState lobbyState;

    private void Awake()
    {
        if (lobbyState == null) lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

    private void OnEnable()
    {
        Refresh();
        if (lobbyState != null)
            lobbyState.Players.OnListChanged += OnPlayersChanged;
    }

    private void OnDisable()
    {
        if (lobbyState != null)
            lobbyState.Players.OnListChanged -= OnPlayersChanged;
    }

    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _) => Refresh();

    private void Refresh()
    {
        if (lobbyState == null) return;

        listText.text = "";
        foreach (var p in lobbyState.Players)
        {
            // Display the player's name along with their host/client role
            string role = p.isHost ? " (HOST)" : " (CLIENT)";
            // Show team information if it has been assigned (>=0)
            string teamInfo = p.teamId >= 0 ? $" [Team {p.teamId}]" : string.Empty;
            string line = p.name.ToString() + role + teamInfo;

            if (p.isReady)
                line += " [READY]";

            listText.text += line + "\n";
        }
    }
}
