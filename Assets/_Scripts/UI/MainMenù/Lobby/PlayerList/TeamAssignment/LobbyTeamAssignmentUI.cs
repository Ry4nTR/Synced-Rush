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

    [Header("Services")]
    [SerializeField] private NetworkLobbyState lobbyState;

    private void Awake()
    {
        if (lobbyState == null)
            lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

    private void OnEnable()
    {
        StartCoroutine(InitWhenReady());
    }

    private IEnumerator InitWhenReady()
    {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        while (lobbyState == null)
        {
            lobbyState = FindFirstObjectByType<NetworkLobbyState>();
            yield return null;
        }

        lobbyState.Players.OnListChanged -= OnPlayersChanged;
        lobbyState.Players.OnListChanged += OnPlayersChanged;

        RebuildUI();
    }

    private void OnDisable()
    {
        if (lobbyState != null && lobbyState.Players != null)
            lobbyState.Players.OnListChanged -= OnPlayersChanged;
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