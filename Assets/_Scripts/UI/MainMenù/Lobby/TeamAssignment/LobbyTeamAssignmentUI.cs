using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LobbyTeamAssignmentUI : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private LobbyPlayerItemUI playerItemPrefab;

    [Header("Containers")]
    [SerializeField] private Transform unassignedContainer;

    [Header("Team Drop Zones (Team A/B only)")]
    [SerializeField] private List<TeamDropZone> teamDropZones = new();

    private NetworkLobbyState lobbyState;

    private void OnEnable()
    {
        Bind();
        SessionServices.OnReady += OnSessionReady;
        StartCoroutine(InitWhenReady());
    }

    private void OnDisable()
    {
        SessionServices.OnReady -= OnSessionReady;

        if (lobbyState != null && lobbyState.Players != null)
            lobbyState.Players.OnListChanged -= OnPlayersChanged;
    }

    private void OnSessionReady(SessionServices s)
    {
        Bind();
        StartCoroutine(InitWhenReady());
    }

    private void Bind()
    {
        var s = SessionServices.Current;
        if (s == null) return;

        lobbyState = s.LobbyState;
    }

    private IEnumerator InitWhenReady()
    {
        // Wait for net
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        // Wait for session + lobby state
        while (SessionServices.Current == null || lobbyState == null || !lobbyState.IsSpawned)
        {
            Bind();
            yield return null;
        }

        // Subscribe once
        lobbyState.Players.OnListChanged -= OnPlayersChanged;
        lobbyState.Players.OnListChanged += OnPlayersChanged;

        RebuildUI();
    }

    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _)
    {
        RebuildUI();
    }

    private void RebuildUI()
    {
        if (playerItemPrefab == null) { Debug.LogError("[LobbyTeamAssignmentUI] playerItemPrefab not assigned."); return; }
        if (unassignedContainer == null) { Debug.LogError("[LobbyTeamAssignmentUI] unassignedContainer not assigned."); return; }
        if (lobbyState == null) { Debug.LogWarning("[LobbyTeamAssignmentUI] lobbyState missing."); return; }

        // Clear
        ClearChildren(unassignedContainer);
        foreach (var dz in teamDropZones)
        {
            if (dz != null && dz.Container != null)
                ClearChildren(dz.Container);
        }

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        foreach (var p in lobbyState.Players)
        {
            var item = Instantiate(playerItemPrefab);
            item.SetData(p, isHost);

            Transform parent = unassignedContainer;

            if (p.teamId >= 0)
            {
                var dz = teamDropZones.Find(z => z != null && z.TeamId == p.teamId);
                if (dz != null && dz.Container != null)
                    parent = dz.Container;
            }

            item.transform.SetParent(parent, false);
        }
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}