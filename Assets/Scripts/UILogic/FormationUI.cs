// File: Scripts/Client/UI/Formation/FormationUI.cs (or wherever your FormationUI.cs is)
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Network.Proxies;
using Logger = Core.Logging.Logger;
using System; // For Tuple
using Client.UI; // For IUIContext (if ClientUIManager provides it) and BaseActionButtonHandler (for close button)
using Client.UI.Formation;

public class FormationUI : UIScreen // Inherit from UIScreen
{
    [Header("Formation UI References")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private ShipSlotUIElement shipSlotPrefab;
    [SerializeField] private UnityEngine.UI.Button closeFormationButton;
    // Removed applyFormationButton
    // Removed openFormationButton (will be external)

    [Header("Configuration")]
    [Tooltip("The radius of the circular area in UI units where slots can be dragged.")]
    [SerializeField] private float uiDisplayRadius = 200f;
    [Tooltip("How much to scale the game world formation offsets to fit into the UI display radius.")]
    [SerializeField] private float worldToUIScaleFactor = 20f;

    private ClientComposer _clientComposer;
    private EscadreProxy.ClientProxy _localEscadreProxy;
    private IUIContext _uiContext; // To get ClientComposer

    private Dictionary<int, ShipSlotUIElement> _activeShipSlotsUI = new Dictionary<int, ShipSlotUIElement>();
    private List<Tuple<int, Core.Primitives.Vector2>> _pendingFormationLayout = new List<Tuple<int, Core.Primitives.Vector2>>();

    private bool _isScreenInitialized = false; // UIScreen has its own IsVisible

    // This Initialize method is called by your UIManager (e.g., ClientUIManager)
    public void Initialize(IUIContext uiContext)
    {
        if (_isScreenInitialized) return;
        _uiContext = uiContext;
        _clientComposer = _uiContext?.GetClientComposer();

        if (_clientComposer == null)
        {
            Logger.LogError("[FormationUI] ClientComposer not found via IUIContext. Cannot initialize screen.");
            // gameObject.SetActive(false); // UIScreen's Hide can do this
            return;
        }
        if (shipSlotPrefab == null || slotContainer == null)
        {
            Logger.LogError("[FormationUI] Critical UI references (Slot Prefab or Slot Container) not set. Disabling functionality.");
            return;
        }

        _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
        // Initial check, though OnShow will also handle this.
        HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy);

        if (closeFormationButton != null)
        {
            closeFormationButton.onClick.AddListener(() => Hide()); // Close button now calls Hide()
        }

        _isScreenInitialized = true;
        Logger.Log("[FormationUI] Initialized.");
        Hide(true); // Start hidden
    }

    private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
    {
        if (_localEscadreProxy != null && IsVisible) // Only if visible should it react immediately to proxy changes for events
        {
            _localEscadreProxy.OnFormationChanged -= RefreshFormationDisplayFromServer;
        }
        _localEscadreProxy = newProxy;
        if (_localEscadreProxy != null && IsVisible)
        {
            _localEscadreProxy.OnFormationChanged += RefreshFormationDisplayFromServer;
        }

        if (IsVisible) // If the screen is currently visible, repopulate
        {
            PopulateShipSlots();
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        if (!_isScreenInitialized)
        {
            Logger.LogWarning("[FormationUI] OnShow called but screen not initialized. Attempting to re-check proxy.");
            if (_clientComposer != null) HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy);
            else return; // Cannot proceed without composer
        }
        if (_localEscadreProxy == null && _clientComposer != null) // Ensure proxy is fresh
        {
            HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy);
        }


        if (_localEscadreProxy == null || !_localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue))
        {
            System.Text.StringBuilder reasonBuilder = new System.Text.StringBuilder();
            if (_localEscadreProxy == null) reasonBuilder.Append("LocalEscadreProxy is null. ");
            else
            {
                if (_localEscadreProxy.IsDestroyed) reasonBuilder.Append("LocalEscadreProxy is destroyed. ");
                if (!_localEscadreProxy.FormationSlots.Any()) reasonBuilder.Append("FormationSlots list is empty. ");
                else if (!_localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue))
                    reasonBuilder.Append($"FormationSlots has {_localEscadreProxy.FormationSlots.Count} entries, but none have a ShipEntityId. ");
            }
            Logger.LogWarning($"[FormationUI] Cannot fully show. Reason(s): {reasonBuilder.ToString()} Will show empty or hide again.");
            // Optionally, auto-hide if conditions aren't met for a meaningful display
            // Hide();
            // return;
        }

        PopulateShipSlots();
        if (_localEscadreProxy != null)
        {
            _localEscadreProxy.OnFormationChanged -= RefreshFormationDisplayFromServer; // Ensure no double subscription
            _localEscadreProxy.OnFormationChanged += RefreshFormationDisplayFromServer;
        }
        Logger.Log("[FormationUI] Screen shown.");
    }

    protected override void OnHide()
    {
        base.OnHide();
        ClearShipSlotsUI();
        _pendingFormationLayout.Clear();
        if (_localEscadreProxy != null)
        {
            _localEscadreProxy.OnFormationChanged -= RefreshFormationDisplayFromServer;
        }
        Logger.Log("[FormationUI] Screen hidden.");
        UIManager.Instance.SwitchToScreen(Assets.Scripts.UILogic.UIScreenType.GameUI);
    }

    private void PopulateShipSlots()
    {
        ClearShipSlotsUI();
        if (_localEscadreProxy == null || !_localEscadreProxy.FormationSlots.Any())
        {
            _pendingFormationLayout = new List<Tuple<int, Core.Primitives.Vector2>>();
            return;
        }

        _pendingFormationLayout = _localEscadreProxy.FormationSlots
            .Where(s => s.ShipEntityId.HasValue)
            .Select(s => Tuple.Create(s.ShipEntityId.Value, s.RelativeOffset))
            .ToList();

        if (!_pendingFormationLayout.Any())
        {
            return; // No ships with IDs to display
        }

        foreach (var slotDataTuple in _pendingFormationLayout)
        {
            ShipSlotUIElement newSlotUI = Instantiate(shipSlotPrefab, slotContainer);
            UnityEngine.Vector2 uiPosition = ConvertWorldOffsetToUIPosition(slotDataTuple.Item2);

            float slotMaxDragRadius = uiDisplayRadius;
            RectTransform prefabRect = shipSlotPrefab.GetComponent<RectTransform>();
            if (prefabRect != null && slotContainer != null)
            {
                slotMaxDragRadius = uiDisplayRadius - (prefabRect.sizeDelta.x / 2f * slotContainer.localScale.x);
            }

            newSlotUI.Initialize(this, slotDataTuple.Item1, uiPosition, slotMaxDragRadius);
            _activeShipSlotsUI.Add(slotDataTuple.Item1, newSlotUI);
        }
        Logger.Log($"[FormationUI] Populated {_activeShipSlotsUI.Count} ship slots in UI.");
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
        if (!_isScreenInitialized || !IsVisible || _localEscadreProxy == null) return;
        Logger.Log("[FormationUI] Refreshing formation display due to server update.");
        PopulateShipSlots();
    }

    public void OnShipSlotUIDragBegin(ShipSlotUIElement slotUI) { /* Optional: feedback or state change */ }
    public void OnShipSlotUIDragging(ShipSlotUIElement slotUI) { /* Optional: continuous feedback */ }

    public void OnShipSlotUIDragEnd(ShipSlotUIElement slotUI)
    {
        Core.Primitives.Vector2 newWorldOffset = ConvertUIPositionToWorldOffset(slotUI.CurrentAnchoredPosition);
        int shipId = slotUI.ShipEntityId;

        var existingTupleIndex = _pendingFormationLayout.FindIndex(t => t.Item1 == shipId);
        if (existingTupleIndex != -1)
        {
            _pendingFormationLayout[existingTupleIndex] = Tuple.Create(shipId, newWorldOffset);
            Logger.Log($"[FormationUI] ShipID {shipId} dragged. New pending world offset: {newWorldOffset}");
        }
        else
        {
            Logger.LogWarning($"[FormationUI] Drag ended for shipID {shipId} not found in pending layout.");
            return;
        }
        // Auto-apply changes
        ApplyPendingFormationChanges();
    }

    private void ApplyPendingFormationChanges()
    {
        if (!_isScreenInitialized || _clientComposer == null || _clientComposer.GameActions == null || _localEscadreProxy == null || !_pendingFormationLayout.Any())
        {
            Logger.LogWarning("[FormationUI] Cannot apply formation changes: Not ready or no pending changes.");
            return;
        }
        _clientComposer.GameActions.RequestSetFormation(_pendingFormationLayout);
        Logger.Log($"[FormationUI] Sent formation change request with {_pendingFormationLayout.Count} slots.");
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

    void OnDestroy() // Unity's OnDestroy
    {
        if (closeFormationButton != null) closeFormationButton.onClick.RemoveAllListeners();

        if (_clientComposer != null)
        {
            _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
        }
        // Ensure event unsubscription happens even if OnHide wasn't called during destruction
        if (_localEscadreProxy != null)
        {
            _localEscadreProxy.OnFormationChanged -= RefreshFormationDisplayFromServer;
        }
        ClearShipSlotsUI(); // Ensure UI elements are destroyed
        _isScreenInitialized = false;
    }
}
