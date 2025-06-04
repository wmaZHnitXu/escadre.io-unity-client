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

        private FormationUI _formationUIScreen; // Changed from FormationUIMediator
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

        public void Initialize(FormationUI screen, int shipEntityId, Vector2 initialAnchoredPosition, float maxDragRadiusFromCenter)
        {
            _formationUIScreen = screen; // Changed parameter type
            _shipEntityId = shipEntityId;
            _rectTransform.anchoredPosition = initialAnchoredPosition;
            _originalAnchoredPosition = initialAnchoredPosition;
            _maxDragRadius = maxDragRadiusFromCenter;

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
                shipInfoText.text = _shipEntityId.ToString(); 
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_formationUIScreen == null) return;
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            if (backgroundImage != null) backgroundImage.color = Color.yellow; 

            _formationUIScreen.OnShipSlotUIDragBegin(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_formationUIScreen == null || _parentCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform, 
                eventData.position,
                _parentCanvas.worldCamera, 
                out Vector2 localPoint
            );

            if (localPoint.magnitude > _maxDragRadius)
            {
                localPoint = localPoint.normalized * _maxDragRadius;
            }

            _rectTransform.anchoredPosition = localPoint;
            _formationUIScreen.OnShipSlotUIDragging(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_formationUIScreen == null) return;
            if (backgroundImage != null) backgroundImage.color = Color.white; 

            _formationUIScreen.OnShipSlotUIDragEnd(this);
        }

        public void UpdatePosition(Vector2 newAnchoredPosition)
        {
            _rectTransform.anchoredPosition = newAnchoredPosition;
            _originalAnchoredPosition = newAnchoredPosition;
        }

        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}