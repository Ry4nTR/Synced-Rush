using System.Collections;
using TMPro;
using UnityEngine;

public class CreateMatchPanelController : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_Dropdown gamemodeDropdown;
    [SerializeField] private TMP_Dropdown mapDropdown;

    [Header("Lobby Password")]
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Refs")]
    [SerializeField] private PanelManager uiManager;

    [Header("Data")]
    [SerializeField] private GamemodeDefinition[] gamemodes;
    [SerializeField] private MapDefinition[] maps;

    [Header("Services")]
    [SerializeField] private MatchmakingManager matchmakingManager;

    private Coroutine createRoutine;

    private void Awake()
    {
        if (matchmakingManager == null)
            matchmakingManager = FindFirstObjectByType<MatchmakingManager>();
    }

    private void Start()
    {
        PopulateDropdowns();
        ResetInputs();
    }

    private void PopulateDropdowns()
    {
        gamemodeDropdown.ClearOptions();
        mapDropdown.ClearOptions();

        foreach (var gm in gamemodes)
            gamemodeDropdown.options.Add(new TMP_Dropdown.OptionData(gm.gamemodeType.ToString()));

        foreach (var map in maps)
            mapDropdown.options.Add(new TMP_Dropdown.OptionData(map.mapName));

        gamemodeDropdown.value = 0;
        mapDropdown.value = 0;

        gamemodeDropdown.RefreshShownValue();
        mapDropdown.RefreshShownValue();
    }

    public void ResetInputs()
    {
        playerNameInput.text = "";
        lobbyNameInput.text = "";
        gamemodeDropdown.value = 0;
        mapDropdown.value = 0;

        if (passwordInput != null)
            passwordInput.text = string.Empty;
    }

    // =========================
    // UI EVENT
    // =========================

    public void OnCreateMatchPressed()
    {
        string playerName = string.IsNullOrWhiteSpace(playerNameInput.text) ? "Host" : playerNameInput.text.Trim();
        string lobbyName = string.IsNullOrWhiteSpace(lobbyNameInput.text) ? $"{playerName}'s Lobby" : lobbyNameInput.text.Trim();
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        PlayerProfile.PlayerName = playerName;

        matchmakingManager.SetLocalPlayerName(playerName);
        matchmakingManager.Host();

        if (createRoutine != null)
            StopCoroutine(createRoutine);

        createRoutine = StartCoroutine(HostCreateLobbyFlow(playerName, lobbyName, password));
    }

    private IEnumerator HostCreateLobbyFlow(string playerName, string lobbyName, string password)
    {
        float timeout = 6f;
        float t = 0f;

        while (t < timeout && (SessionServices.Current == null || SessionServices.Current.LobbyState == null || SessionServices.Current.LobbyManager == null))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        var s = SessionServices.Current;
        if (s == null) yield break;

        var lobbyManager = s.LobbyManager;
        var lobbyState = s.LobbyState;

        var gm = gamemodes[gamemodeDropdown.value];
        var map = maps[mapDropdown.value];

        lobbyManager.CreateLobby(playerName, lobbyName, password);
        lobbyManager.SetGamemode(gm);
        lobbyManager.SetMap(map);

        while (!lobbyState.IsSpawned)
            yield return null;

        lobbyState.SetLobbyNameServerRpc(lobbyName);

        // ✅ max players based on gamemode
        lobbyState.SetMaxPlayersServerRpc(Mathf.Max(1, gm.requiredPlayers));

        // ✅ password (server only)
        lobbyState.ServerSetLobbyPassword(password);

        // ✅ host name (otherwise stays "Connecting...")
        lobbyState.SetPlayerNameServerRpc(playerName);

        // ✅ LAN broadcast includes MaxPlayers now
        var discovery = LobbyDiscoveryService.Instance;
        if (discovery != null)
        {
            bool hasPassword = !string.IsNullOrEmpty(password);

            discovery.StartBroadcasting(
                lobbyName,
                () => lobbyState.Players.Count,
                () => lobbyState.MaxPlayers.Value,
                () => lobbyManager.CurrentState == LobbyState.InGame,
                hasPassword
            );
        }

        uiManager.ShowLobby();
        ResetInputs();
    }
}