using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Hosts a drag‑and‑drop team assignment UI in the lobby.  Players are
/// represented by draggable list items which can be dropped into team zones.
/// This script listens to the NetworkLobbyState.Players network list and
/// rebuilds the UI whenever the list changes.  Only the host can drag
/// players; clients will see an updated list but cannot interact.
/// </summary>
public class LobbyTeamAssignmentUI : MonoBehaviour
{
    [Header("Containers")]
    [Tooltip("The container that holds players who have not yet been assigned a team.")]
    [SerializeField] private Transform unassignedContainer;

    [Tooltip("List of drop zones corresponding to each team.  The index in this list is the team ID.")]
    [SerializeField] private List<TeamDropZone> teamZones;

    [Header("Prefabs")]
    [Tooltip("Prefab used to represent a player in the list.  Must contain a LobbyPlayerItemUI component.")]
    [SerializeField] private GameObject playerItemPrefab;

    private void OnEnable()
    {
        Refresh();
        if (NetworkLobbyState.Instance != null)
        {
            NetworkLobbyState.Instance.Players.OnListChanged += OnPlayersChanged;
        }
    }

    private void OnDisable()
    {
        if (NetworkLobbyState.Instance != null)
        {
            NetworkLobbyState.Instance.Players.OnListChanged -= OnPlayersChanged;
        }
    }

    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _)
    {
        Refresh();
    }

    /// <summary>
    /// Rebuilds the player list UI.  Clears existing items and recreates
    /// them based on the current network lobby state.  Players with an
    /// assigned teamId are placed under their respective team drop zone;
    /// others go into the unassigned container.
    /// </summary>
    public void Refresh()
    {
        // Clear existing children from all containers
        ClearChildren(unassignedContainer);
        foreach (var zone in teamZones)
        {
            ClearChildren(zone.container);
        }

        // Rebuild from network state
        if (NetworkLobbyState.Instance == null || playerItemPrefab == null)
            return;

        foreach (var p in NetworkLobbyState.Instance.Players)
        {
            var go = Instantiate(playerItemPrefab);
            var item = go.GetComponent<LobbyPlayerItemUI>();
            if (item != null)
            {
                item.Initialize(p.clientId, p.name.ToString(), p.isReady, p.isHost);
            }

            // Assign to the correct team container or the unassigned container
            int teamId = p.teamId;
            if (teamId >= 0 && teamId < teamZones.Count)
            {
                go.transform.SetParent(teamZones[teamId].container, false);
            }
            else
            {
                go.transform.SetParent(unassignedContainer, false);
            }
        }
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }
}