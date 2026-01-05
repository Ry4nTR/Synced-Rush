using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private MenuUIManager uiManager;
    [SerializeField] private LobbyManager lobbyManager;

    private int localPlayerId = 0;

    // =========================
    // MAIN MENU
    // =========================

    public void OnCreateMatchPressed()
    {
        lobbyManager.CreateLobby("Host");
        uiManager.ShowLobby();
    }

    public void OnJoinMatchPressed()
    {
        lobbyManager.AddPlayer("Client");
        uiManager.ShowLobby();
    }

    // =========================
    // LOBBY
    // =========================

    public void OnReadyPressed()
    {
        lobbyManager.SetReady(localPlayerId, true);
    }

    public void OnStartMatchPressed()
    {
        if (!lobbyManager.CanStartMatch())
        {
            Debug.Log("Cannot start match yet");
            return;
        }

        lobbyManager.LockLobby();
        Debug.Log("MATCH START (next step later)");
    }

    public void OnLeaveLobbyPressed()
    {
        lobbyManager.LeaveLobby();
        uiManager.ShowMainMenu();
    }
}
