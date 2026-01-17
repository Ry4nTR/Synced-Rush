using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Represents a single player in the lobby team assignment UI.  Supports
/// dragging by the host to assign players to teams.  Clients will see
/// the items but cannot drag them.  This script requires a CanvasGroup
/// component for drag visual behaviour.
/// </summary>
public class LobbyPlayerItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image readyIndicator;
    [SerializeField] private Image hostIndicator;
    [SerializeField] private CanvasGroup canvasGroup;

    // The clientId of the player represented by this UI element
    public ulong ClientId { get; private set; }

    // Original parent transform used to restore the item after dragging
    private Transform originalParent;
    // Reference to the root canvas to ensure the dragged item appears on top of other UI elements
    private Canvas rootCanvas;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            // Find the topmost canvas
            var canvases = GetComponentsInParent<Canvas>();
            rootCanvas = canvases.Length > 0 ? canvases[canvases.Length - 1] : rootCanvas;
        }
    }

    /// <summary>
    /// Initializes this UI item with player data.
    /// </summary>
    public void Initialize(ulong clientId, string playerName, bool isReady, bool isHost)
    {
        ClientId = clientId;
        if (nameText != null)
            nameText.text = playerName;
        if (readyIndicator != null)
            readyIndicator.gameObject.SetActive(isReady);
        if (hostIndicator != null)
            hostIndicator.gameObject.SetActive(isHost);
    }

    // Drag handlers
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Only the host can drag items for team assignment
        if (!NetworkManager.Singleton.IsHost)
            return;
        originalParent = transform.parent;
        if (rootCanvas != null)
            transform.SetParent(rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!NetworkManager.Singleton.IsHost)
            return;
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!NetworkManager.Singleton.IsHost)
            return;
        canvasGroup.blocksRaycasts = true;
        // Return to original container if not dropped on a valid zone
        if (eventData.pointerEnter == null || eventData.pointerEnter.GetComponent<TeamDropZone>() == null)
        {
            transform.SetParent(originalParent, false);
        }
        transform.localPosition = Vector3.zero;
    }
}