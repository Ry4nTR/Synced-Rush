using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class JoinMatchPanelController : MonoBehaviour
{
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField ipInput;

    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private PanelManager uiManager;

    private void Start()
    { 

        playerNameInput.text = "";
    }

    public void OnJoinPressed()
    {
        string playerName = string.IsNullOrWhiteSpace(playerNameInput.text)
            ? "Client"
            : playerNameInput.text;

        PlayerProfile.PlayerName = playerName;

        string ip = ipInput.text.Trim();
        if (string.IsNullOrEmpty(ip))
            return;

        // Start client connection
        MatchmakingManager.Instance.Join(ip);

        // Wait for actual network connection
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        uiManager.ShowLobby();
    }
}
