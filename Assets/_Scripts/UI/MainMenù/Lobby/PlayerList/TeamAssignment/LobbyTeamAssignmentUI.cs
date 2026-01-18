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
        // Start a safe init that waits until NetworkLobbyState exists
        StartCoroutine(InitWhenReady());
    }

    private void OnDisable()
    {
        if (lobbyState != null && lobbyState.Players != null)
            lobbyState.Players.OnListChanged -= OnPlayersChanged;
    }

    private IEnumerator InitWhenReady()
    {
        // Wait for NetworkManager and NetworkLobbyState to exist
        while (NetworkManager.Singleton == null || NetworkLobbyState.Instance == null)
            yield return null;

        lobbyState = NetworkLobbyState.Instance;

        // Subscribe once
        lobbyState.Players.OnListChanged -= OnPlayersChanged;
        lobbyState.Players.OnListChanged += OnPlayersChanged;

        // Build immediately
        RebuildUI();
    }

    private void OnPlayersChanged(Unity.Netcode.NetworkListEvent<NetLobbyPlayer> _)
    {
        RebuildUI();
    }

    private void RebuildUI()
    {
        if (playerItemPrefab == null)
        {
            Debug.LogError("[LobbyTeamAssignmentUI] Player Item Prefab is NOT assigned.");
            return;
        }

        if (unassignedContainer == null)
        {
            Debug.LogError("[LobbyTeamAssignmentUI] Unassigned Container is NOT assigned.");
            return;
        }

        if (lobbyState == null)
        {
            Debug.LogWarning("[LobbyTeamAssignmentUI] No lobbyState yet.");
            return;
        }

        // Clear existing items
        ClearChildren(unassignedContainer);
        foreach (var dz in teamDropZones)
        {
            if (dz != null && dz.Container != null)
                ClearChildren(dz.Container);
        }

        bool isHost = NetworkManager.Singleton.IsHost;

        // Recreate all items from the network list
        foreach (var p in lobbyState.Players)
        {
            var item = Instantiate(playerItemPrefab);
            item.SetData(p, isHost);

            Transform parent = unassignedContainer;

            // Team assignment
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