using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LobbyPlayerItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image readyIndicator;
    [SerializeField] private Image hostIndicator;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject dragHandle;

    public ulong ClientId { get; private set; }

    private RectTransform rectTransform;
    private Transform originalParent;
    private bool canDrag;

    private Canvas rootCanvas;
    private RectTransform canvasRect;
    private Vector2 pointerOffset;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetData(NetLobbyPlayer p, bool isHostClient)
    {
        ClientId = p.clientId;

        canDrag = isHostClient; // host can drag everyone (including self)
        if (dragHandle) dragHandle.SetActive(isHostClient);

        if (nameText) nameText.text = p.name.ToString();
        if (readyIndicator) readyIndicator.enabled = p.isReady;
        if (hostIndicator) hostIndicator.enabled = p.isHost;
    }

    private bool EnsureCanvas()
    {
        // Find the closest canvas at the moment we start dragging
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                rootCanvas = FindAnyObjectByType<Canvas>(); // last resort
        }

        if (rootCanvas == null)
            return false;

        canvasRect = rootCanvas.transform as RectTransform;
        return canvasRect != null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!canDrag) return;

        if (!EnsureCanvas())
        {
            Debug.LogError("[LobbyPlayerItemUI] No Canvas found for drag.");
            return;
        }

        originalParent = transform.parent;

        canvasGroup.blocksRaycasts = false;

        // Put it on top so it renders above other UI while dragging
        transform.SetParent(rootCanvas.transform, true);

        // Compute cursor offset so it doesn't snap
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, eventData.pressEventCamera, out var localPointerPos))
        {
            pointerOffset = (Vector2)rectTransform.localPosition - localPointerPos;
        }
        else
        {
            pointerOffset = Vector2.zero;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!canDrag) return;
        if (canvasRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, eventData.pressEventCamera, out var localPointerPos))
        {
            rectTransform.localPosition = localPointerPos + pointerOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!canDrag) return;

        canvasGroup.blocksRaycasts = true;

        if (transform.parent == rootCanvas.transform && originalParent != null)
        {
            transform.SetParent(originalParent, false);
            if (rectTransform != null) rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
