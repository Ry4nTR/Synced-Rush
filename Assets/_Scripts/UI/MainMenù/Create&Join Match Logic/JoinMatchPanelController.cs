using TMPro;
using UnityEngine;

public class JoinMatchPanelController : MonoBehaviour
{
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField ipInput;

    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private MenuUIManager uiManager;

    private void Start()
    {
        playerNameInput.text = PlayerProfile.PlayerName;
    }

    public void OnJoinPressed()
    {
        string playerName = playerNameInput.text;
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Client";

        PlayerProfile.PlayerName = playerName;

        string ip = ipInput.text;
        if (string.IsNullOrWhiteSpace(ip))
            return;

        MatchmakingManager.Instance.Join(ip);

        lobbyManager.AddPlayer(playerName);

        uiManager.ShowLobby();
    }
}
