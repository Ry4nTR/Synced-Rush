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

    [SerializeField] private CanvasGroup ipJoinWindow;

    private void Start()
    {
        playerNameInput.text = "";
    }

    public void ShowJoinIPWindow()
    {
        Show(ipJoinWindow);
    }

    public void CloseJoinIPWindow()
    {
        Hide(ipJoinWindow);
    }

    public void OnIPJoinPressed()
    {
        string playerName = string.IsNullOrWhiteSpace(playerNameInput.text)
            ? "Client"
            : playerNameInput.text;

        PlayerProfile.PlayerName = playerName;

        string ip = ipInput.text.Trim();
        if (string.IsNullOrEmpty(ip))
            return;

        // Wait for actual network connection
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Start client connection
        MatchmakingManager.Instance.SetLocalPlayerName(playerName);
        MatchmakingManager.Instance.Join(ip);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        // Tell server our chosen name
        NetworkLobbyState.Instance.SetPlayerNameServerRpc(MatchmakingManager.Instance.LocalPlayerName);

        uiManager.ShowLobby();
    }

    private void Show(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void Hide(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

}
