using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LobbyPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI hostIpText;
    [SerializeField] private GameObject startMatchButton;

    [Header("Refs")]
    [SerializeField] private PanelManager uiManager;

    private void OnEnable()
    {
        RefreshUI();

        if (NetworkLobbyState.Instance != null)
        {
            NetworkLobbyState.Instance.LobbyName.OnValueChanged += OnLobbyNameChanged;
            NetworkLobbyState.Instance.Players.OnListChanged += OnPlayersChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown += OnNetworkShutdown;
        }
    }

    private void OnDisable()
    {
        if (NetworkLobbyState.Instance != null)
        {
            NetworkLobbyState.Instance.LobbyName.OnValueChanged -= OnLobbyNameChanged;
            NetworkLobbyState.Instance.Players.OnListChanged -= OnPlayersChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown -= OnNetworkShutdown;
        }
    }


    // =========================
    // UI EVENTS
    // =========================
    public void OnReadyPressed()
    {
        if (NetworkLobbyState.Instance == null)
            return; 

        NetworkLobbyState.Instance.ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void OnStartMatchPressed()
    {
        if (!NetworkManager.Singleton.IsHost)
            return;

        Debug.Log("[LOBBY] Start Match pressed (validation later)");
    }

    public void OnLeaveLobbyPressed()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[LOBBY] Host stopped the lobby");
            NetworkManager.Singleton.Shutdown();
        }
        else
        {
            Debug.Log("[LOBBY] Client left the lobby");
            NetworkManager.Singleton.Shutdown();
        }

        uiManager.ShowMainMenu();
    }

    // =========================
    // NETWORK EVENTS
    // =========================
    private void OnLobbyNameChanged( FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        RefreshUI();
    }

    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _)
    {
        RefreshUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Host left → client returns to menu
        if (!NetworkManager.Singleton.IsHost &&
            clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("[NET] Host left lobby");
            uiManager.ShowMainMenu();
        }
    }

    private void OnNetworkShutdown()
    {
        Debug.Log("[NET] Network shutdown — returning to main menu");
        uiManager.ShowMainMenu();
    }


    // =========================
    // INTERNAL
    // =========================
    private void RefreshUI()
    {
        bool isHost = NetworkManager.Singleton != null &&
                      NetworkManager.Singleton.IsHost;

        startMatchButton.SetActive(isHost);

        if (NetworkLobbyState.Instance != null)
        {
            lobbyNameText.text =
                NetworkLobbyState.Instance.LobbyName.Value.ToString();
        }
        else
        {
            lobbyNameText.text = string.Empty;
        }

        if (isHost)
        {
            hostIpText.gameObject.SetActive(true);
            hostIpText.text =
                $"Host IP: {MatchmakingManager.Instance.GetLocalIP()}";
        }
        else
        {
            hostIpText.gameObject.SetActive(false);
        }
    }

}
