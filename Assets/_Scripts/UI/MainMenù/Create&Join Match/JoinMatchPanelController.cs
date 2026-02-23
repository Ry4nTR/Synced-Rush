using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class JoinMatchPanelController : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField ipInput;

    [Header("UI")]
    [SerializeField] private PanelManager uiManager;
    [SerializeField] private CanvasGroup ipJoinWindow;

    [Header("Lobby List")]
    [SerializeField] private RectTransform lobbyListContainer;
    [SerializeField] private LobbyItemUI lobbyItemPrefab;

    [Header("Password Prompt")]
    [SerializeField] private CanvasGroup passwordPromptWindow;
    [SerializeField] private TMP_InputField passwordPromptInput;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Services")]
    [SerializeField] private MatchmakingManager matchmakingManager;

    private readonly Dictionary<string, LobbyItemUI> itemsByIp = new();
    private readonly Dictionary<string, LobbyDiscoveryService.LobbyInfo> lastInfoByIp = new();

    private string selectedLobbyIp;
    private string pendingJoinIp;
    private string pendingJoinPassword;

    private Coroutine refreshRoutine;

    private void Awake()
    {
        if (matchmakingManager == null)
            matchmakingManager = FindFirstObjectByType<MatchmakingManager>();

        Hide(ipJoinWindow);
        Hide(passwordPromptWindow);
        SetStatus(string.Empty);
    }

    private void OnEnable()
    {
        LobbyDiscoveryService.Instance?.StartListening();

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);

        refreshRoutine = StartCoroutine(RefreshListLoop());
    }

    private void OnDisable()
    {
        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }

        LobbyDiscoveryService.Instance?.StopListening();
    }

    private IEnumerator RefreshListLoop()
    {
        var wait = new WaitForSecondsRealtime(0.35f);
        while (true)
        {
            RefreshLobbyListOnce();
            yield return wait;
        }
    }

    private void RefreshLobbyListOnce()
    {
        if (LobbyDiscoveryService.Instance == null || lobbyListContainer == null || lobbyItemPrefab == null)
            return;

        List<LobbyDiscoveryService.LobbyInfo> lobbies = LobbyDiscoveryService.Instance.GetLobbies();
        lobbies = lobbies.Where(l => l != null).ToList();
        lobbies.Sort((a, b) => string.Compare(a.LobbyName, b.LobbyName, System.StringComparison.OrdinalIgnoreCase));

        var seen = new HashSet<string>();

        foreach (var info in lobbies)
        {
            if (string.IsNullOrWhiteSpace(info.Ip))
                continue;

            seen.Add(info.Ip);
            lastInfoByIp[info.Ip] = info;

            if (!itemsByIp.TryGetValue(info.Ip, out var item) || item == null)
            {
                item = Instantiate(lobbyItemPrefab, lobbyListContainer);
                itemsByIp[info.Ip] = item;

                item.Initialize(
                    info.Ip,
                    info.LobbyName,
                    info.PlayerCount,
                    Mathf.Max(1, info.MaxPlayers),
                    info.HasPassword,
                    this
                );
            }
            else
            {
                item.UpdatePlayerCount(info.PlayerCount, Mathf.Max(1, info.MaxPlayers));
            }
        }

        var toRemove = new List<string>();
        foreach (var kv in itemsByIp)
            if (!seen.Contains(kv.Key))
                toRemove.Add(kv.Key);

        foreach (var ip in toRemove)
        {
            if (itemsByIp.TryGetValue(ip, out var item) && item != null)
                Destroy(item.gameObject);

            itemsByIp.Remove(ip);
            lastInfoByIp.Remove(ip);

            if (selectedLobbyIp == ip)
                selectedLobbyIp = null;
        }
    }

    // =========================
    // SELECTION / JOIN
    // =========================

    public void OnLobbyItemSelected(string lobbyIp) => selectedLobbyIp = lobbyIp;

    public void OnLobbyItemDoubleClicked(string lobbyIp)
    {
        selectedLobbyIp = lobbyIp;
        TryJoinLobby(lobbyIp);
    }

    public void OnJoinSelectedPressed()
    {
        if (string.IsNullOrWhiteSpace(selectedLobbyIp))
        {
            SetStatus("No lobby selected.", 2f);
            return;
        }

        TryJoinLobby(selectedLobbyIp);
    }

    private void TryJoinLobby(string lobbyIp)
    {
        if (string.IsNullOrWhiteSpace(lobbyIp))
            return;

        if (lastInfoByIp.TryGetValue(lobbyIp, out var info) && info != null)
        {
            int max = Mathf.Max(1, info.MaxPlayers);

            if (info.PlayerCount >= max)
            {
                SetStatus("Lobby is full.", 2.5f);
                return;
            }

            if (info.HasPassword)
            {
                pendingJoinIp = lobbyIp;
                pendingJoinPassword = string.Empty;

                if (passwordPromptInput != null)
                    passwordPromptInput.text = string.Empty;

                Show(passwordPromptWindow);
                return;
            }
        }

        JoinByIp(lobbyIp, string.Empty);
    }

    // =========================
    // JOIN BY IP WINDOW
    // =========================

    public void ShowJoinIPWindow() => Show(ipJoinWindow);
    public void CloseJoinIPWindow() => Hide(ipJoinWindow);

    public void OnIPJoinPressed()
    {
        string ip = ipInput != null ? ipInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(ip))
            return;

        TryJoinLobby(ip);
    }

    // =========================
    // PASSWORD PROMPT WINDOW
    // =========================

    public void ClosePasswordPromptWindow()
    {
        pendingJoinIp = null;
        pendingJoinPassword = null;
        Hide(passwordPromptWindow);
    }

    public void OnPasswordJoinConfirmPressed()
    {
        if (string.IsNullOrWhiteSpace(pendingJoinIp))
            return;

        string pwd = passwordPromptInput != null ? passwordPromptInput.text : string.Empty;
        pendingJoinPassword = pwd ?? string.Empty;

        Hide(passwordPromptWindow);
        JoinByIp(pendingJoinIp, pendingJoinPassword);
    }

    // =========================
    // CORE JOIN
    // =========================

    private void JoinByIp(string ip, string password)
    {
        string playerName = (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            ? playerNameInput.text.Trim()
            : "Client";

        PlayerProfile.PlayerName = playerName;
        matchmakingManager?.SetLocalPlayerName(playerName);

        pendingJoinIp = ip;
        pendingJoinPassword = password ?? string.Empty;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        matchmakingManager.Join(ip);
        SetStatus($"Connecting to {ip}...");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        StartCoroutine(WaitForSessionThen(() =>
        {
            var s = SessionServices.Current;
            if (s == null || s.LobbyState == null) return;

            s.LobbyState.SetPlayerNameServerRpc(matchmakingManager.LocalPlayerName);

            if (!string.IsNullOrEmpty(pendingJoinPassword))
                s.LobbyState.SubmitLobbyPasswordServerRpc(pendingJoinPassword);

            uiManager.ShowLobby();
            SetStatus(string.Empty);
        }));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        uiManager.ShowMainMenu();
        SetStatus("Disconnected (wrong password or lobby full).", 3.0f);
    }

    private IEnumerator WaitForSessionThen(System.Action action)
    {
        float timeout = 6f;
        float t = 0f;

        while (t < timeout)
        {
            var s = SessionServices.Current;
            if (s != null && s.LobbyState != null && s.LobbyState.IsSpawned)
            {
                action?.Invoke();
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        SetStatus("Network session not ready.", 3.0f);
    }

    // =========================
    // UI HELPERS
    // =========================

    private void SetStatus(string message, float clearAfterSeconds = 0f)
    {
        if (statusText == null) return;

        statusText.text = message;

        if (clearAfterSeconds > 0f)
        {
            StopCoroutine(nameof(ClearStatusAfter));
            StartCoroutine(ClearStatusAfter(clearAfterSeconds));
        }
    }

    private IEnumerator ClearStatusAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (statusText != null)
            statusText.text = string.Empty;
    }

    private void Show(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha = 1f;
        g.interactable = true;
        g.blocksRaycasts = true;
    }

    private void Hide(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
    }
}