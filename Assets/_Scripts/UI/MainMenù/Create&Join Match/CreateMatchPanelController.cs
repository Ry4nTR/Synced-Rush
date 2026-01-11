using TMPro;
using UnityEngine;

public class CreateMatchPanelController : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_Dropdown gamemodeDropdown;
    [SerializeField] private TMP_Dropdown mapDropdown;
    [SerializeField] private TMP_InputField lobbyNameInput;


    [Header("Refs")]
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private PanelManager uiManager;

    [Header("Data")]
    [SerializeField] private GamemodeDefinition[] gamemodes;
    [SerializeField] private MapDefinition[] maps;

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

        // set to first option
        gamemodeDropdown.value = 0;
        mapDropdown.value = 0;

        gamemodeDropdown.RefreshShownValue();
        mapDropdown.RefreshShownValue();
    }

    // Reset all input fields to default values
    public void ResetInputs()
    {
        playerNameInput.text = "";
        gamemodeDropdown.value = 0;
        mapDropdown.value = 0;
        lobbyNameInput.text = "";
    }

    // =========================
    // UI EVENTS
    // =========================
    public void OnCreateMatchPressed()
    {
        string playerName = string.IsNullOrWhiteSpace(playerNameInput.text)
            ? "Host"
            : playerNameInput.text;

        string lobbyName = string.IsNullOrWhiteSpace(lobbyNameInput.text)
            ? $"{playerName}'s Lobby"
            : lobbyNameInput.text;

        PlayerProfile.PlayerName = playerName;

        // 1. Start network host
        MatchmakingManager.Instance.Host();

        // 2. Initialize LOCAL lobby cache (non-networked)
        lobbyManager.CreateLobby(playerName, lobbyName);
        lobbyManager.SetGamemode(gamemodes[gamemodeDropdown.value]);
        lobbyManager.SetMap(maps[mapDropdown.value]);

        // 3. Push lobby name to NETWORK
        NetworkLobbyState.Instance.SetLobbyNameServerRpc(lobbyName);

        // 4. Switch UI
        uiManager.ShowLobby();

        // 5. Reset inputs for next time
        ResetInputs();
    }
}
