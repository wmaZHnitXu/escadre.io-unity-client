// File: Scripts/Client/UI/Formation/ShipSlotUIElement.cs
using UnityEngine;
using UnityEngine.EventSystems; // Required for drag interfaces
using UnityEngine.UI; // For Image, if you want to change icon/color
using TMPro; // If you want to display ship ID or type on the slot

namespace Client.UI.Formation
{
    public class ShipSlotUIElement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Image backgroundImage; // Optional: for visual feedback (e.g., selected)
        [SerializeField] private TextMeshProUGUI shipInfoText; // Optional: display ship ID or short type

        private FormationUIMediator _mediator;
        private int _shipEntityId;
        private Vector2 _originalAnchoredPosition;
        private float _maxDragRadius; // Max distance from the formation window's center

        private Canvas _parentCanvas;
        private RectTransform _parentCanvasRectTransform;

        public int ShipEntityId => _shipEntityId;
        public Vector2 CurrentAnchoredPosition => _rectTransform.anchoredPosition;

        void Awake()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        }

        public void Initialize(FormationUIMediator mediator, int shipEntityId, Vector2 initialAnchoredPosition, float maxDragRadiusFromCenter)
        {
            _mediator = mediator;
            _shipEntityId = shipEntityId;
            _rectTransform.anchoredPosition = initialAnchoredPosition;
            _originalAnchoredPosition = initialAnchoredPosition;
            _maxDragRadius = maxDragRadiusFromCenter;

            // Cache parent canvas for coordinate conversion during drag
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                _parentCanvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }
            else
            {
                Core.Logging.Logger.LogError($"[ShipSlotUIElement ShipID:{_shipEntityId}] Could not find parent Canvas!");
            }


            if (shipInfoText != null)
            {
                shipInfoText.text = _shipEntityId.ToString(); // Simple display
            }
            // Potentially set a unique color/icon based on ship type if that data is available
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_mediator == null) return;
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            if (backgroundImage != null) backgroundImage.color = Color.yellow; // Highlight while dragging

            // Optional: Bring to front if you have overlapping elements
            // _rectTransform.SetAsLastSibling();
            _mediator.OnShipSlotUIDragBegin(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_mediator == null || _parentCanvas == null) return;

            // Convert screen point to anchored position within the parent container (slotContainer)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform, // The direct parent container
                eventData.position,
                _parentCanvas.worldCamera, // Use canvas camera (null for ScreenSpaceOverlay)
                out Vector2 localPoint
            );

            // Clamp the position to be within the _maxDragRadius circle from the parent container's center (pivot assumed 0.5, 0.5)
            if (localPoint.magnitude > _maxDragRadius)
            {
                localPoint = localPoint.normalized * _maxDragRadius;
            }

            _rectTransform.anchoredPosition = localPoint;
            _mediator.OnShipSlotUIDragging(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_mediator == null) return;
            if (backgroundImage != null) backgroundImage.color = Color.white; // Reset highlight

            _mediator.OnShipSlotUIDragEnd(this);
        }

        public void UpdatePosition(Vector2 newAnchoredPosition)
        {
            _rectTransform.anchoredPosition = newAnchoredPosition;
            _originalAnchoredPosition = newAnchoredPosition;
        }

        // Call this if the slot is being removed
        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}