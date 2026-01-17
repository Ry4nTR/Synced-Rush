using TMPro;
using UnityEngine;
using Unity.Netcode;

public class LobbyPlayerListUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI listText;

    private void OnEnable()
    {
        Refresh();

        if (NetworkLobbyState.Instance != null)
            NetworkLobbyState.Instance.Players.OnListChanged += OnPlayersChanged;
    }

    private void OnDisable()
    {
        if (NetworkLobbyState.Instance != null)
            NetworkLobbyState.Instance.Players.OnListChanged -= OnPlayersChanged;
    }

    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (NetworkLobbyState.Instance == null)
            return;

        listText.text = "";

        foreach (var p in NetworkLobbyState.Instance.Players)
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
