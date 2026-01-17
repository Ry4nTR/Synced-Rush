using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Component attached to UI panels representing team drop zones in the lobby.
/// When a player list item is dropped onto this zone, the host assigns that
/// player to the zone's team via LobbyManager.SetPlayerTeam().  This script
/// requires an EventSystem and Canvas to be present in the scene.
/// </summary>
public class TeamDropZone : MonoBehaviour, IDropHandler
{
    [Tooltip("The team index associated with this drop zone.  Should match the index in LobbyTeamAssignmentUI.teamZones.")]
    public int teamId;

    [Tooltip("Container under this drop zone where player items should be placed.")]
    public Transform container;

    /// <summary>
    /// Called by the EventSystem when a draggable object is dropped on this zone.
    /// </summary>
    /// <param name="eventData">Information about the drop event.</param>
    public void OnDrop(PointerEventData eventData)
    {
        // Only the host can assign teams manually
        if (LobbyManager.Instance == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
            return;

        // Retrieve the player item being dropped
        var item = eventData.pointerDrag?.GetComponent<LobbyPlayerItemUI>();
        if (item != null)
        {
            // Assign to the server via LobbyManager
            LobbyManager.Instance.SetPlayerTeam(item.ClientId, teamId);
        }
    }
}