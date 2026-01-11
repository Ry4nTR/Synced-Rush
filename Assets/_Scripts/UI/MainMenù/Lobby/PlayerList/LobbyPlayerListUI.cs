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
            string role = p.isHost ? " (HOST)" : " (CLIENT)";
            string line = p.name.ToString() + role;

            if (p.isReady)
                line += " [READY]";

            listText.text += line + "\n";
        }
    }
}
