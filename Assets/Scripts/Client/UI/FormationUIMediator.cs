// File: Scripts/Client/UI/Formation/FormationUIMediator.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Network.Proxies;
// Removed Core.Primitives using here to be more explicit, relying on fully qualified names or adapter inputs/outputs.
using Logger = Core.Logging.Logger;
using System; // For Tuple

namespace Client.UI.Formation
{
    public class FormationUIMediator : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject formationWindowPanel; 
        [SerializeField] private RectTransform slotContainer;    
        [SerializeField] private ShipSlotUIElement shipSlotPrefab;
        [SerializeField] private UnityEngine.UI.Button applyFormationButton; 
        [SerializeField] private UnityEngine.UI.Button closeFormationButton;
        [SerializeField] private UnityEngine.UI.Button openFormationButton; 

        [Header("Configuration")]
        [Tooltip("The radius of the circular area in UI units where slots can be dragged.")]
        [SerializeField] private float uiDisplayRadius = 200f;
        [Tooltip("How much to scale the game world formation offsets to fit into the UI display radius.")]
        [SerializeField] private float worldToUIScaleFactor = 20f; 

        private ClientComposer _clientComposer;
        private EscadreProxy.ClientProxy _localEscadreProxy;
        private IUIContext _uiContext;

        private Dictionary<int, ShipSlotUIElement> _activeShipSlotsUI = new Dictionary<int, ShipSlotUIElement>();
        private List<Tuple<int, Core.Primitives.Vector2>> _pendingFormationLayout = new List<Tuple<int, Core.Primitives.Vector2>>();

        private bool _isInitialized = false;
        private bool _isWindowOpen = false;

        public void Initialize(IUIContext uiContext)
        {
            if (_isInitialized) return;
            _uiContext = uiContext;
            _clientComposer = _uiContext?.GetClientComposer();

            if (_clientComposer == null)
            {
                Logger.LogError("[FormationUIMediator] ClientComposer not found via IUIContext. Cannot initialize.");
                enabled = false;
                return;
            }
            if (formationWindowPanel == null || shipSlotPrefab == null || slotContainer == null)
            {
                Logger.LogError("[FormationUIMediator] Critical UI references (Window Panel, Slot Prefab, or Slot Container) not set. Disabling.");
                enabled = false;
                return;
            }

            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
            HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy); 

            if (applyFormationButton != null) applyFormationButton.onClick.AddListener(ApplyPendingFormationChanges);
            if (closeFormationButton != null) closeFormationButton.onClick.AddListener(CloseFormationWindow);
            if (openFormationButton != null) openFormationButton.onClick.AddListener(OpenFormationWindow);


            formationWindowPanel.SetActive(false); 
            _isWindowOpen = false;
            _isInitialized = true;
            Logger.Log("[FormationUIMediator] Initialized.");
        }

        private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
        {
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnFormationChanged -= RefreshFormationDisplayFromServer;
            }
            _localEscadreProxy = newProxy;
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnFormationChanged += RefreshFormationDisplayFromServer;
            }

            if (_isWindowOpen) 
            {
                PopulateShipSlots();
            }
            UpdateOpenButtonInteractability();
        }

        private void UpdateOpenButtonInteractability()
        {
            if (openFormationButton != null)
            {
                bool canOpen = false;
                if (_clientComposer != null && _clientComposer.IsSessionFullyActive &&
                    _localEscadreProxy != null && !_localEscadreProxy.IsDestroyed)
                {
                    canOpen = _localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue);
                }
                else if (_clientComposer == null)
                {
                    // This case should ideally not happen if initialized properly
                }
                else if (!_clientComposer.IsSessionFullyActive)
                {
                    // Session not fully active
                }
                else if (_localEscadreProxy == null)
                {
                    // Local escadre proxy is null
                }
                else if (_localEscadreProxy.IsDestroyed)
                {
                    // Local escadre proxy is destroyed
                }

                openFormationButton.interactable = canOpen;
            }
        }


        public void OpenFormationWindow()
        {
            if (!_isInitialized || _localEscadreProxy == null || !_localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue))
            {
                System.Text.StringBuilder reasonBuilder = new System.Text.StringBuilder();
                if (!_isInitialized) reasonBuilder.Append("Not initialized. ");
                if (_localEscadreProxy == null) reasonBuilder.Append("LocalEscadreProxy is null. ");
                else 
                {
                    if (_localEscadreProxy.IsDestroyed) reasonBuilder.Append("LocalEscadreProxy is destroyed. ");
                    if (!_localEscadreProxy.FormationSlots.Any()) reasonBuilder.Append("FormationSlots list is empty. ");
                    else if (!_localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue))
                    {
                        reasonBuilder.Append($"FormationSlots has {_localEscadreProxy.FormationSlots.Count} entries, but none have a ShipEntityId. ");
                        int slotsWithNullId = _localEscadreProxy.FormationSlots.Count(s => !s.ShipEntityId.HasValue);
                        reasonBuilder.Append($"({slotsWithNullId} slots have null ShipEntityId). ");
                    }
                }
                
                if (_clientComposer != null)
                {
                     reasonBuilder.Append($"ClientComposer.IsSessionFullyActive: {_clientComposer.IsSessionFullyActive}. ");
                     if (_clientComposer.LocalEscadreProxy == null) reasonBuilder.Append("Composer's LocalEscadreProxy is null. ");
                     else if (_clientComposer.LocalEscadreProxy != _localEscadreProxy) reasonBuilder.Append("Internal _localEscadreProxy differs from ClientComposer's. ");
                     else
                     {
                        reasonBuilder.Append($"Composer's LocalEscadreProxy.FormationSlots.Count: {_clientComposer.LocalEscadreProxy.FormationSlots.Count}, ");
                        reasonBuilder.Append($"Composer's LocalEscadreProxy SlotsWithShipID: {_clientComposer.LocalEscadreProxy.FormationSlots.Count(s => s.ShipEntityId.HasValue)}. ");
                     }

                } else {
                    reasonBuilder.Append("ClientComposer is null. ");
                }

                Logger.LogWarning($"[FormationUIMediator] Cannot open formation window. Reason(s): {reasonBuilder.ToString()}");
                return;
            }

            formationWindowPanel.SetActive(true);
            _isWindowOpen = true;
            PopulateShipSlots();
            Logger.Log("[FormationUIMediator] Formation window opened.");
        }

        public void CloseFormationWindow()
        {
            formationWindowPanel.SetActive(false);
            _isWindowOpen = false;
            ClearShipSlotsUI(); 
            _pendingFormationLayout.Clear(); 
            Logger.Log("[FormationUIMediator] Formation window closed.");
        }

        private void PopulateShipSlots()
        {
            ClearShipSlotsUI();
            if (_localEscadreProxy == null || !_localEscadreProxy.FormationSlots.Any())
            {
                _pendingFormationLayout = new List<Tuple<int, Core.Primitives.Vector2>>();
                 if (applyFormationButton != null) applyFormationButton.interactable = false; // Disable apply if no slots
                return;
            }

            _pendingFormationLayout = _localEscadreProxy.FormationSlots
                .Where(s => s.ShipEntityId.HasValue) 
                .Select(s => Tuple.Create(s.ShipEntityId.Value, s.RelativeOffset))
                .ToList();

            if (!_pendingFormationLayout.Any())
            {
                 if (applyFormationButton != null) applyFormationButton.interactable = false; // Disable apply if no *actual* ships
                return;
            }
            if (applyFormationButton != null) applyFormationButton.interactable = true;


            foreach (var slotDataTuple in _pendingFormationLayout)
            {
                ShipSlotUIElement newSlotUI = Instantiate(shipSlotPrefab, slotContainer);
                UnityEngine.Vector2 uiPosition = ConvertWorldOffsetToUIPosition(slotDataTuple.Item2);

                float slotMaxDragRadius = uiDisplayRadius;
                RectTransform prefabRect = shipSlotPrefab.GetComponent<RectTransform>();
                if (prefabRect != null && slotContainer != null) // Added null check for slotContainer
                {
                    slotMaxDragRadius = uiDisplayRadius - (prefabRect.sizeDelta.x / 2f * slotContainer.localScale.x) ; 
                }

                newSlotUI.Initialize(this, slotDataTuple.Item1, uiPosition, slotMaxDragRadius);
                _activeShipSlotsUI.Add(slotDataTuple.Item1, newSlotUI);
            }
            Logger.Log($"[FormationUIMediator] Populated {_activeShipSlotsUI.Count} ship slots in UI.");
        }

        private void ClearShipSlotsUI()
        {
            foreach (var slotUI in _activeShipSlotsUI.Values)
            {
                if (slotUI != null) slotUI.Dispose();
            }
            _activeShipSlotsUI.Clear();
        }

        private void RefreshFormationDisplayFromServer()
        {
            if (!_isInitialized || !_isWindowOpen || _localEscadreProxy == null) return;

            Logger.Log("[FormationUIMediator] Refreshing formation display due to server update.");
            PopulateShipSlots();
        }

        public void OnShipSlotUIDragBegin(ShipSlotUIElement slotUI) {  }
        public void OnShipSlotUIDragging(ShipSlotUIElement slotUI) {  }
        public void OnShipSlotUIDragEnd(ShipSlotUIElement slotUI)
        {
            Core.Primitives.Vector2 newWorldOffset = ConvertUIPositionToWorldOffset(slotUI.CurrentAnchoredPosition);
            int shipId = slotUI.ShipEntityId;

            var existingTupleIndex = _pendingFormationLayout.FindIndex(t => t.Item1 == shipId);
            if (existingTupleIndex != -1)
            {
                _pendingFormationLayout[existingTupleIndex] = Tuple.Create(shipId, newWorldOffset);
                Logger.Log($"[FormationUIMediator] ShipID {shipId} dragged. New pending world offset: {newWorldOffset}");
            }
            else
            {
                Logger.LogWarning($"[FormationUIMediator] Drag ended for shipID {shipId} not found in pending layout.");
                return;
            }

            if (applyFormationButton == null || !applyFormationButton.gameObject.activeInHierarchy || !applyFormationButton.interactable)
            {
                 // Auto-apply if button isn't visible/interactable, or simply let user click it if it is.
                 // For now, we assume if the button is there, it should be clicked.
                 // If you want auto-apply on drag end when button is hidden:
                 // ApplyPendingFormationChanges();
            }
            // If apply button is present and interactable, user should click it.
            // No auto-apply here to give user explicit control if button is visible.
        }

        private void ApplyPendingFormationChanges()
        {
            if (!_isInitialized || _clientComposer == null || _clientComposer.GameActions == null || _localEscadreProxy == null || !_pendingFormationLayout.Any())
            {
                Logger.LogWarning("[FormationUIMediator] Cannot apply formation changes: Not ready or no pending changes.");
                return;
            }
            _clientComposer.GameActions.RequestSetFormation(_pendingFormationLayout);
            Logger.Log($"[FormationUIMediator] Sent formation change request with {_pendingFormationLayout.Count} slots.");
        }

        private UnityEngine.Vector2 ConvertWorldOffsetToUIPosition(Core.Primitives.Vector2 worldOffset)
        {
            Core.Primitives.Vector2 scaledWorldOffset = new Core.Primitives.Vector2(
                worldOffset.X * worldToUIScaleFactor,
                worldOffset.Y * worldToUIScaleFactor
            );
            return scaledWorldOffset.ToUnityVector();
        }

        private Core.Primitives.Vector2 ConvertUIPositionToWorldOffset(UnityEngine.Vector2 uiPosition)
        {
            UnityEngine.Vector2 unscaledUiPosition = new UnityEngine.Vector2(
                uiPosition.x / worldToUIScaleFactor,
                uiPosition.y / worldToUIScaleFactor
            );
            return unscaledUiPosition.ToCoreVector();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (_isWindowOpen)
                {
                    CloseFormationWindow();
                }
                else
                {
                    bool canOpenWindow = _isInitialized &&
                                       _localEscadreProxy != null &&
                                       !_localEscadreProxy.IsDestroyed &&
                                       _localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue);
                    
                    if (_clientComposer != null) // Further check on composer state
                    {
                        canOpenWindow = canOpenWindow && _clientComposer.IsSessionFullyActive;
                    } else {
                        canOpenWindow = false; // If composer is null, can't be ready
                    }


                    if (canOpenWindow)
                    {
                        OpenFormationWindow();
                    }
                    else
                    {
                        System.Text.StringBuilder reasonBuilder = new System.Text.StringBuilder();
                        if (!_isInitialized) reasonBuilder.Append("Not initialized. ");
                        if (_clientComposer == null) reasonBuilder.Append("ClientComposer is null. ");
                        else if (!_clientComposer.IsSessionFullyActive) reasonBuilder.Append("Session not fully active. ");
                        
                        if (_localEscadreProxy == null) reasonBuilder.Append("LocalEscadreProxy is null. ");
                        else
                        {
                            if (_localEscadreProxy.IsDestroyed) reasonBuilder.Append("LocalEscadreProxy is destroyed. ");
                            if (!_localEscadreProxy.FormationSlots.Any()) reasonBuilder.Append("FormationSlots list is empty. ");
                            else if (!_localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue))
                                 reasonBuilder.Append($"FormationSlots has {_localEscadreProxy.FormationSlots.Count} entries, but none have a ShipEntityId. ");
                        }
                        Logger.LogWarning($"[FormationUIMediator F-Key] Cannot open formation window. Reason(s): {reasonBuilder.ToString()}");
                    }
                }
            }

            if (_isInitialized && Time.frameCount % 30 == 0) 
            {
                UpdateOpenButtonInteractability();
                 if (applyFormationButton != null && _isWindowOpen) // Also update apply button interactability if window is open
                {
                    applyFormationButton.interactable = _pendingFormationLayout.Any();
                }
            }
        }

        void OnDestroy()
        {
            if (_clientComposer != null)
            {
                _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            }
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnFormationChanged -= RefreshFormationDisplayFromServer;
            }
            if (applyFormationButton != null) applyFormationButton.onClick.RemoveAllListeners();
            if (closeFormationButton != null) closeFormationButton.onClick.RemoveAllListeners();
            if (openFormationButton != null) openFormationButton.onClick.RemoveAllListeners();

            ClearShipSlotsUI();
            _isInitialized = false;
        }
    }
}