using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AutoTeamToggleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Toggle toggle;

    [Tooltip("Optional: set this to the visual root to hide (e.g. the whole row). If null, we hide only the Toggle.")]
    [SerializeField] private GameObject visualRoot;

    [Header("Behavior")]
    [SerializeField] private bool hideForClients = true;

    private bool isWired;

    private void Awake()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (visualRoot == null)
            visualRoot = gameObject; // default to this object
    }

    private void OnEnable()
    {
        StartCoroutine(InitWhenNetworkReady());
    }

    private void OnDisable()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);

        isWired = false;
    }

    private IEnumerator InitWhenNetworkReady()
    {
        // Wait for NetworkManager to exist
        while (NetworkManager.Singleton == null)
            yield return null;

        // Wait until we're actually listening (host or client started)
        while (!NetworkManager.Singleton.IsListening)
            yield return null;

        bool isHost = NetworkManager.Singleton.IsHost;

        // Hide for clients (but DON'T disable the whole GO permanently by mistake)
        if (hideForClients && !isHost)
        {
            if (visualRoot != null) visualRoot.SetActive(false);
            yield break;
        }

        // Ensure visible for host
        if (visualRoot != null) visualRoot.SetActive(true);

        // Wire the callback once
        if (!isWired && toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
            toggle.onValueChanged.AddListener(OnToggleChanged);
            isWired = true;
        }
    }

    private void OnToggleChanged(bool enabled)
    {
        // Only host can control team assignment
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        if (LobbyManager.Instance == null || NetworkLobbyState.Instance == null)
            return;

        if (enabled)
        {
            // Assign teams using your lobby manager logic (random assignment).
            LobbyManager.Instance.AssignTeamsAutomatically();
        }
        else
        {
            // Return everyone to unassigned
            var players = NetworkLobbyState.Instance.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.teamId = -1;
                p.isAlive = false;
                players[i] = p;
            }
        }
    }
}
