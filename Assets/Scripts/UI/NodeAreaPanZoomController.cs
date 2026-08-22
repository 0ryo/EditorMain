using UnityEngine;
using UnityEngine.EventSystems;

public class NodeAreaPanZoomController : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform content;
    [SerializeField] float minZoom = 0.4f;
    [SerializeField] float maxZoom = 2.5f;
    [SerializeField] float zoomSpeed = 0.12f;
    [SerializeField] PointerEventData.InputButton panButton = PointerEventData.InputButton.Middle;

    bool isPanning;

    public void Configure(RectTransform viewportRect, RectTransform contentRect)
    {
        viewport = viewportRect;
        content = contentRect;
        ClampZoom();
        ClampPan();
    }

    public void ResetView()
    {
        ResolveDefaults();
        if (!IsReady()) return;

        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        ClampPan();
    }

    public void FocusContentPoint(Vector2 contentPoint)
    {
        ResolveDefaults();
        if (!IsReady()) return;

        float zoom = Mathf.Max(0.001f, content.localScale.x);
        content.anchoredPosition = -contentPoint * zoom;
        ClampPan();
    }

    void Awake()
    {
        ResolveDefaults();
        ClampZoom();
        ClampPan();
    }

    void OnEnable()
    {
        ResolveDefaults();
        ClampZoom();
        ClampPan();
    }

    void OnRectTransformDimensionsChange()
    {
        ClampPan();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!IsReady()) return;
        if (Mathf.Abs(eventData.scrollDelta.y) <= Mathf.Epsilon) return;

        float currentZoom = content.localScale.x;
        float targetZoom = Mathf.Clamp(currentZoom * (1f + eventData.scrollDelta.y * zoomSpeed), minZoom, maxZoom);
        if (Mathf.Abs(targetZoom - currentZoom) <= 0.0001f) return;

        var eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, eventData.position, eventCamera, out var viewportLocal))
        {
            return;
        }

        var contentLocal = (viewportLocal - content.anchoredPosition) / currentZoom;
        content.localScale = Vector3.one * targetZoom;
        content.anchoredPosition = viewportLocal - (contentLocal * targetZoom);
        ClampPan();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsReady()) return;
        isPanning = eventData.button == panButton;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsReady() || !isPanning) return;

        var eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, eventCamera, out var currentLocal) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position - eventData.delta, eventCamera, out var prevLocal))
        {
            return;
        }

        content.anchoredPosition += currentLocal - prevLocal;
        ClampPan();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isPanning = false;
    }

    void ResolveDefaults()
    {
        if (viewport == null)
        {
            viewport = transform as RectTransform;
        }

        if (content == null && viewport != null)
        {
            var found = viewport.Find("GraphContent") as RectTransform;
            if (found != null)
            {
                content = found;
            }
        }
    }

    bool IsReady()
    {
        return viewport != null && content != null && isActiveAndEnabled;
    }

    void ClampZoom()
    {
        if (content == null) return;

        float zoom = content.localScale.x;
        if (zoom <= 0.001f) zoom = 1f;
        zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        content.localScale = new Vector3(zoom, zoom, 1f);
    }

    void ClampPan()
    {
        if (viewport == null || content == null) return;

        var viewportRect = viewport.rect;
        var contentRect = content.rect;
        if (viewportRect.width <= 0f || viewportRect.height <= 0f ||
            contentRect.width <= 0f || contentRect.height <= 0f)
        {
            return;
        }

        float zoom = content.localScale.x;
        float scaledWidth = contentRect.width * zoom;
        float scaledHeight = contentRect.height * zoom;
        float xLimit = Mathf.Max(0f, (scaledWidth - viewportRect.width) * 0.5f);
        float yLimit = Mathf.Max(0f, (scaledHeight - viewportRect.height) * 0.5f);

        var pos = content.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -xLimit, xLimit);
        pos.y = Mathf.Clamp(pos.y, -yLimit, yLimit);
        content.anchoredPosition = pos;
    }
}
