using TMPro;
using Unity.Netcode;
using UnityEngine;

public class JoinMatchPanelController : MonoBehaviour
{
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField ipInput;

    [SerializeField] private PanelManager uiManager;

    [Header("Services")]
    [SerializeField] private MatchmakingManager matchmakingManager;
    [SerializeField] private NetworkLobbyState lobbyState;

    [SerializeField] private CanvasGroup ipJoinWindow;

    private void Awake()
    {
        if (matchmakingManager == null) matchmakingManager = FindFirstObjectByType<MatchmakingManager>();
        if (lobbyState == null) lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

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

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        matchmakingManager.SetLocalPlayerName(playerName);
        matchmakingManager.Join(ip);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        lobbyState.SetPlayerNameServerRpc(matchmakingManager.LocalPlayerName);
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