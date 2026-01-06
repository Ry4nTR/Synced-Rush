using UnityEngine;

public class MenuController : MonoBehaviour
{
    [SerializeField] private MenuUIManager uiManager;
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private GamemodeDefinition defaultGamemode;
    [SerializeField] private MapDefinition defaultMap;

    private int localPlayerId = 0;

    // =========================
    // MAIN MENU
    // =========================

    public void OnCreateMatchPressed()
    {
        lobbyManager.CreateLobby("Host");
        lobbyManager.SetGamemode(defaultGamemode);
        lobbyManager.SetMap(defaultMap);
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
        lobbyManager.StartMatch();
    }

    public void OnLeaveLobbyPressed()
    {
        lobbyManager.LeaveLobby();
        uiManager.ShowMainMenu();
    }
}
