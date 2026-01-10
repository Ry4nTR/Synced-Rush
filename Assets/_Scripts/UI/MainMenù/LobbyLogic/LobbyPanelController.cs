using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyPanelController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject startMatchButton;
    [SerializeField] private TextMeshProUGUI hostIpText;
    [SerializeField] private TextMeshProUGUI lobbyNameText;

    [Header("Refs")]
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private MenuUIManager uiManager;

    private int localPlayerId = 0; // TEMP until networking player IDs

    private void OnEnable()
    {
        // Show start button only for host
        bool isHost = NetworkManager.Singleton.IsHost;
        startMatchButton.SetActive(isHost);

        // Update lobby name
        lobbyNameText.text = lobbyManager.LobbyName;

        // Show host IP if we are the host
        if (NetworkManager.Singleton.IsHost)
        {
            string ip = MatchmakingManager.Instance.GetLocalIP();
            hostIpText.text = $"Host IP: {ip}";
            hostIpText.gameObject.SetActive(true);
        }
        else
        {
            hostIpText.gameObject.SetActive(false);
        }
    }

    // =========================
    // LOBBY BUTTON EVENTS
    // =========================

    public void OnReadyPressed()
    {
        lobbyManager.SetReady(localPlayerId, true);
    }

    public void OnStartMatchPressed()
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        lobbyManager.StartMatch();
    }

    public void OnLeaveLobbyPressed()
    {
        lobbyManager.LeaveLobby();
        MatchmakingManager.Instance.Leave(); // we add this method below
        uiManager.ShowMainMenu();
    }
}
