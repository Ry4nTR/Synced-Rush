using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    public NetworkVariable<MatchState> CurrentState =
        new NetworkVariable<MatchState>(
            MatchState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Score")]
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }

    private GamemodeDefinition gamemode;
    private LobbyManager lobbyManager;
    private SpawnManager spawnManager;
    private RoundDeathTracker deathTracker;
    private MapDefinition currentMap;

    // =========================
    // Initialization
    // =========================
    private void Awake()
    {
        // Ensure a single persistent instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Persist across scene loads so lobby and game scenes share the same round manager
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            CurrentState.OnValueChanged += OnMatchStateChanged;
        }
    }

    public void Initialize(LobbyManager lobby, GamemodeDefinition mode)
    {
        if (!IsServer) return;

        lobbyManager = lobby;
        gamemode = mode;

        spawnManager = FindAnyObjectByType<SpawnManager>();
        spawnManager.Initialize(lobby);

        deathTracker = FindAnyObjectByType<RoundDeathTracker>();
        deathTracker.Initialize(lobby, this);

        TeamAScore = 0;
        TeamBScore = 0;

        StartNextRound();
    }

    /// <summary>
    /// Starts a match by loading the selected map scene and initializing the round system once the scene load completes.
    /// </summary>
    public void StartMatch(LobbyManager lobby, GamemodeDefinition mode, MapDefinition map)
    {
        if (!IsServer)
            return;

        // Store references for later initialization
        lobbyManager = lobby;
        gamemode = mode;
        currentMap = map;

        // Subscribe to scene events so we know when the map scene has finished loading
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

        // Attempt to load the map scene. Scenes must be added to the Build Settings.
        var status = NetworkManager.SceneManager.LoadScene(currentMap.sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"Failed to load scene {currentMap.sceneName} with status {status}");
            // Unsubscribe to avoid memory leaks
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    // Callback for Netcode scene events. When the selected map finishes loading,
    // initialize the round system and spawn players.
    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Only act on LoadEventCompleted events for the current map
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
            !string.IsNullOrEmpty(currentMap?.sceneName) &&
            sceneEvent.SceneName == currentMap.sceneName)
        {
            // Unsubscribe to prevent repeated calls
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

            // Initialize managers now that the map is loaded
            spawnManager = FindAnyObjectByType<SpawnManager>();
            if (spawnManager == null)
            {
                Debug.LogError("SpawnManager not found in the loaded scene.");
            }
            else
            {
                spawnManager.Initialize(lobbyManager);
            }

            deathTracker = FindAnyObjectByType<RoundDeathTracker>();
            if (deathTracker == null)
            {
                Debug.LogError("RoundDeathTracker not found in the loaded scene.");
            }
            else
            {
                deathTracker.Initialize(lobbyManager, this);
            }

            // Reset scores
            TeamAScore = 0;
            TeamBScore = 0;

            // Start the first round
            StartNextRound();
        }
    }

    // =========================
    // Round Management
    // =========================
    private void StartNextRound()
    {
        if (!IsServer) return;

        Debug.Log("Preparing next round");

        // Assign teams and reset alive state for each player before spawning
        AssignTeams();
        spawnManager.SpawnAllPlayers();

        GameplayUtils.EnableGameplayForAllPlayers();

        CurrentState.Value = MatchState.InRound;

        Debug.Log("Round started");
    }

    public void EndRound(int winningTeamId)
    {
        if (!IsServer) return;
        if (CurrentState.Value != MatchState.InRound) return;

        CurrentState.Value = MatchState.RoundEnd;

        if (winningTeamId == 0) TeamAScore++;
        else if (winningTeamId == 1) TeamBScore++;

        Debug.Log($"Round ended. Score A:{TeamAScore} B:{TeamBScore}");

        GameplayUtils.DisableGameplayForAllPlayers();

        StartCoroutine(RoundTransitionCoroutine());
    }

    private IEnumerator RoundTransitionCoroutine()
    {
        yield return new WaitForSeconds(3f);
        CheckMatchEnd();
    }

    // =========================
    // Match Management
    // =========================
    private void CheckMatchEnd()
    {
        if (TeamAScore >= gamemode.roundsToWin ||
            TeamBScore >= gamemode.roundsToWin)
        {
            EndMatch();
        }
        else
        {
            StartNextRound();
        }
    }

    private void EndMatch()
    {
        if (!IsServer) return;

        CurrentState.Value = MatchState.MatchEnd;

        int winningTeam = TeamAScore > TeamBScore ? 0 : 1;
        Debug.Log($"Match ended. Winning team: {winningTeam}");

        lobbyManager.OnMatchEnded(winningTeam);
    }

    private void OnMatchStateChanged(MatchState oldState, MatchState newState)
    {
        Debug.Log($"MatchState changed: {oldState} → {newState}");

        switch (newState)
        {
            case MatchState.InRound:
                GameplayUtils.EnableGameplayForAllPlayers();
                break;

            case MatchState.RoundEnd:
            case MatchState.MatchEnd:
            case MatchState.Lobby:
                GameplayUtils.DisableGameplayForAllPlayers();
                break;
        }
    }

    // =========================
    // Team Assignment
    // =========================
    /// <summary>
    /// Assign players to teams before each round begins. Team distribution is based on the
    /// selected gamemode's playersPerTeam value. Players are shuffled to randomize team
    /// composition, then assigned sequentially. All players are marked as not alive prior
    /// to spawning. This should only be called on the server.
    /// </summary>
    private void AssignTeams()
    {
        if (!IsServer)
            return;

        var lobbyState = NetworkLobbyState.Instance;
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState instance not found. Cannot assign teams.");
            return;
        }

        var players = lobbyState.Players;
        int totalPlayers = players.Count;
        if (gamemode == null)
        {
            Debug.LogWarning("Gamemode not set; defaulting to 1 player per team for assignment.");
        }
        int perTeam = gamemode != null ? gamemode.playersPerTeam : 1;
        if (perTeam <= 0)
        {
            perTeam = 1;
        }

        // Create a list of indices and shuffle it to randomize team assignment
        var indices = new System.Collections.Generic.List<int>(totalPlayers);
        for (int i = 0; i < totalPlayers; i++) indices.Add(i);
        // Fisher–Yates shuffle
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
        }

        // Assign teams sequentially based on shuffled order
        for (int index = 0; index < indices.Count; index++)
        {
            int playerIndex = indices[index];
            var p = players[playerIndex];
            // Team is determined by integer division by playersPerTeam (two teams: 0 or 1)
            p.teamId = index / perTeam;
            // Mark as not alive until spawn
            p.isAlive = false;
            players[playerIndex] = p;
        }
    }


}
