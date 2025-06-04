// Scripts/UI/ShopUI.cs
using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.UILogic; 
using DG.Tweening;
using System.Collections;
using System.Linq; 
using UnityEngine.UI;
using Core.Model; 
using Core.Network.Proxies; 
using Client.UI; 
using Logger = Core.Logging.Logger; 
using Core.Network; // For IClientProxy

public class ShopUI : UIScreen
{
    [Header("Shop Layout")]
    [SerializeField] private Transform contentUpLine; // For Ship Purchases
    [SerializeField] private RectTransform upLine;
    [SerializeField] private Transform contentDownLine; // For Ship Upgrades
    [SerializeField] private RectTransform downLine;
    [SerializeField] private GameObject productButtonPrefab; // For Ship Purchases
    [SerializeField] private GameObject shipUpgradeItemPrefab; 
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject score; 

    [Header("Item Visuals")]
    [Tooltip("Default icon if a specific one isn't found for a ship type for purchases.")]
    [SerializeField] private Sprite defaultShipPurchaseIcon;
    [Tooltip("Default icon for ships in the upgrade list.")]
    [SerializeField] private Sprite defaultShipUpgradeIcon; 

    [Header("Animation Settings")]
    public float animationDuration = 0.5f; 
    public Ease easeType = Ease.OutExpo;

    private Vector2 topPanelOnScreenPosition;
    private Vector2 bottomPanelOnScreenPosition;
    private bool positionsInitialized = false;

    private UIManager uiManager; 
    private IUIContext _uiContext;
    private ClientComposer _clientComposer;
    private EscadreProxy.ClientProxy _localEscadreProxy;
    
    private List<ShopItemUI> _activePurchaseItemUIs = new List<ShopItemUI>(); 
    private List<ShipUpgradeItemUI> _activeUpgradeItemUIs = new List<ShipUpgradeItemUI>(); 
    private bool _isShopPopulated = false;
    private bool _refreshPending = false; // Flag to handle refreshes carefully


    public void Initialize(IUIContext context)
    {
        _uiContext = context;
        _clientComposer = _uiContext?.GetClientComposer();

        if (_clientComposer == null)
        {
            Logger.LogError("[ShopUI] ClientComposer could not be retrieved from IUIContext. Shop will not function.");
            return;
        }
        Logger.Log("[ShopUI] Initialized with ClientComposer.");
        Hide(true); 
    }


    protected override void Awake()
    {
        base.Awake(); 
        uiManager = UIManager.Instance; 

        if (contentUpLine == null) Logger.LogError("[ShopUI Error] contentUpLine is NOT assigned!");
        if (upLine == null) Logger.LogError("[ShopUI Error] upLine RectTransform is NOT assigned!");
        if (contentDownLine == null) Logger.LogError("[ShopUI Error] contentDownLine is NOT assigned!");
        if (downLine == null) Logger.LogError("[ShopUI Error] downLine RectTransform is NOT assigned!");
        if (productButtonPrefab == null) Logger.LogError("[ShopUI Error] productButtonPrefab is NOT assigned!");
        if (shipUpgradeItemPrefab == null) Logger.LogError("[ShopUI Error] shipUpgradeItemPrefab is NOT assigned!");
        if (exitButton == null) Logger.LogError("[ShopUI Error] exitButton is NOT assigned!");

        exitButton?.onClick.AddListener(OnExitButtonClicked);
        StartCoroutine(InitializePanelPositions());
    }

    private IEnumerator InitializePanelPositions()
    {
        yield return null; 
        yield return null; 

        if (upLine != null && upLine.gameObject.activeInHierarchy) 
        {
            topPanelOnScreenPosition = upLine.anchoredPosition;
             if (upLine.rect.height > 0) { 
                upLine.anchoredPosition = new Vector2(
                    topPanelOnScreenPosition.x,
                    topPanelOnScreenPosition.y + upLine.rect.height
                );
             } else {
                Logger.LogWarning("[ShopUI] upLine rect.height is 0 or invalid. Off-screen positioning might be incorrect.");
                 upLine.anchoredPosition = new Vector2(topPanelOnScreenPosition.x, topPanelOnScreenPosition.y + 300); 
             }
        } else if (upLine == null) {
            Logger.LogError("[ShopUI] upLine is null in InitializePanelPositions.");
        }

        if (downLine != null && downLine.gameObject.activeInHierarchy)
        {
            bottomPanelOnScreenPosition = downLine.anchoredPosition;
            if(downLine.rect.height > 0) {
                downLine.anchoredPosition = new Vector2(
                    bottomPanelOnScreenPosition.x,
                    bottomPanelOnScreenPosition.y - downLine.rect.height
                );
            } else {
                Logger.LogWarning("[ShopUI] downLine rect.height is 0 or invalid. Off-screen positioning might be incorrect.");
                downLine.anchoredPosition = new Vector2(bottomPanelOnScreenPosition.x, bottomPanelOnScreenPosition.y - 300); 
            }
        } else if (downLine == null) {
             Logger.LogError("[ShopUI] downLine is null in InitializePanelPositions.");
        }
        positionsInitialized = true;
    }


    protected override void OnShow()
    {
        base.OnShow();
        if (_clientComposer == null)
        {
            Logger.LogError("[ShopUI] OnShow called, but ClientComposer is null. Cannot function.");
            Hide(true); 
            return;
        }
        
        _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
        if (_clientComposer.ClientLevel != null) 
        {
            _clientComposer.ClientLevel.OnProxyAdded += HandleClientLevelProxyAddedOrRemoved; // Consolidated
            _clientComposer.ClientLevel.OnProxyRemoved += HandleClientLevelProxyAddedOrRemoved; // Consolidated
        }
        HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy); 

        StartCoroutine(ShowSequence());
    }

    protected override void OnHide()
    {
        base.OnHide(); 
        
        if (_clientComposer != null)
        {
            _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            if (_clientComposer.ClientLevel != null) 
            {
                _clientComposer.ClientLevel.OnProxyAdded -= HandleClientLevelProxyAddedOrRemoved;
                _clientComposer.ClientLevel.OnProxyRemoved -= HandleClientLevelProxyAddedOrRemoved;
            }
        }
        if (_localEscadreProxy != null) 
        {
            _localEscadreProxy.OnShopDesignsChanged -= ConditionalRefreshPurchaseItemsDisplay;
            _localEscadreProxy.OnFormationChanged -= ConditionalRefreshUpgradeItemsDisplay;
            _localEscadreProxy.OnResourcesChanged -= UpdateItemsAffordability;
        }
        _isShopPopulated = false;
    }

    private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
    {
        if (_localEscadreProxy != null)
        {
            _localEscadreProxy.OnShopDesignsChanged -= ConditionalRefreshPurchaseItemsDisplay;
            _localEscadreProxy.OnFormationChanged -= ConditionalRefreshUpgradeItemsDisplay;
            _localEscadreProxy.OnResourcesChanged -= UpdateItemsAffordability;
        }
        _localEscadreProxy = newProxy;
        if (_localEscadreProxy != null)
        {
            _localEscadreProxy.OnShopDesignsChanged += ConditionalRefreshPurchaseItemsDisplay;
            _localEscadreProxy.OnFormationChanged += ConditionalRefreshUpgradeItemsDisplay;
            _localEscadreProxy.OnResourcesChanged += UpdateItemsAffordability;
        }

        if (IsVisible)
        {
            RequestRefreshShopDisplay(); 
        }
    }
    
    // Consolidated handler for proxy add/remove from ClientLevel
    private void HandleClientLevelProxyAddedOrRemoved(IClientProxy proxy)
    {
        if (!IsVisible || _localEscadreProxy == null) return;

        if (proxy is ShipProxy.ClientProxy shipProxy)
        {
            // Check if this ship belongs to our current local escadre by checking current formation slots
            bool isOurShipInFormation = _localEscadreProxy.FormationSlots.Any(slot => slot.ShipEntityId.HasValue && slot.ShipEntityId.Value == shipProxy.EntityId);
            // Also check if it *was* in the UI (for removals where it might already be gone from formation slots)
            bool wasOurShipInUI = _activeUpgradeItemUIs.Any(uiItem => uiItem.gameObject.name.Contains($"ShipID_{shipProxy.EntityId}"));

            if (isOurShipInFormation || wasOurShipInUI)
            {
                // Logger.Log($"[ShopUI] Relevant ShipProxy (ID: {shipProxy.EntityId}) added/removed. Requesting refresh of upgrade items.");
                RequestRefreshUpgradeItemsDisplay(); // Use the debounced refresh
            }
        }
    }

    private void ConditionalRefreshPurchaseItemsDisplay()
    {
        if (IsVisible) RequestRefreshPurchaseItemsDisplay();
    }
    private void ConditionalRefreshUpgradeItemsDisplay()
    {
        if (IsVisible) RequestRefreshUpgradeItemsDisplay();
    }


    private void RequestRefreshShopDisplay()
    {
        if (_refreshPending || !IsVisible) return;
        StartCoroutine(DebouncedRefreshShopDisplay());
    }
    private void RequestRefreshPurchaseItemsDisplay()
    {
        if (_refreshPending || !IsVisible) return;
        StartCoroutine(DebouncedRefreshShopDisplay(true, false));
    }
    private void RequestRefreshUpgradeItemsDisplay()
    {
        if (_refreshPending || !IsVisible) return;
        StartCoroutine(DebouncedRefreshShopDisplay(false, true));
    }

    private IEnumerator DebouncedRefreshShopDisplay(bool refreshPurchases = true, bool refreshUpgrades = true)
    {
        _refreshPending = true;
        yield return null; // Wait one frame to allow other events (like proxy creation) to process

        if (IsVisible) // Double check if still visible after the frame delay
        {
            _isShopPopulated = false; 
            if (refreshPurchases) RefreshPurchaseItemsDisplayInternal();
            if (refreshUpgrades) RefreshUpgradeItemsDisplayInternal();
            _isShopPopulated = true;
            UpdateItemsAffordability(); // Update affordability after items are populated
        }
        _refreshPending = false;
    }


    private void UpdateItemsAffordability()
    {
        if (_localEscadreProxy == null || !_isShopPopulated) return;
        int playerResources = _localEscadreProxy.Resources;

        foreach (var itemUI in _activePurchaseItemUIs)
        {
            string[] nameParts = itemUI.gameObject.name.Split('_');
            if (nameParts.Length >= 3 && nameParts[1] == "ID" && int.TryParse(nameParts[2], out int designId))
            {
                var matchingDesign = _localEscadreProxy.AvailableShopDesigns.FirstOrDefault(d => d.DesignId == designId);
                if (matchingDesign != null)
                {
                    itemUI.UpdateAffordability(playerResources >= matchingDesign.Cost);
                }
            }
        }

        foreach (var upgradeUI in _activeUpgradeItemUIs)
        {
            string[] nameParts = upgradeUI.gameObject.name.Split('_');
             if (nameParts.Length >=3 && nameParts[1] == "ShipID" && int.TryParse(nameParts[2], out int shipId))
            {
                int upgradeCost = CalculateDynamicUpgradeCost(shipId);
                upgradeUI.UpdateAffordability(playerResources >= upgradeCost);
            }
        }
    }

    private IEnumerator ShowSequence()
    {
        while (!positionsInitialized)
        {
            yield return null;
        }
        AnimatePanelsIn();
        if (score != null) score.SetActive(true);
        RequestRefreshShopDisplay(); 
        yield break; 
    }

    public Sequence AnimatePanelsIn()
    {
        Sequence sequence = DOTween.Sequence();
        if (upLine != null)
        {
            sequence.Insert(0, upLine.DOAnchorPos(topPanelOnScreenPosition, animationDuration)
                .SetEase(easeType).SetUpdate(true)); 
        }
        if (downLine != null)
        {
            sequence.Insert(0, downLine.DOAnchorPos(bottomPanelOnScreenPosition, animationDuration)
                .SetEase(easeType).SetUpdate(true)); 
        }
        return sequence;
    }

    private void OnExitButtonClicked()
    {
        if (score != null) score.SetActive(false);
        StartCoroutine(ExitShopCoroutine());
    }

    private IEnumerator ExitShopCoroutine()
    {
        if (uiManager == null)
        {
            Logger.LogError("[ShopUI] UIManager is not available. Cannot switch screen.");
            yield break;
        }

        Sequence hideAnimation = AnimatePanelsOut();
        if (hideAnimation != null)
        {
            yield return hideAnimation.WaitForCompletion();
        }
        uiManager.SwitchToScreen(UIScreenType.GameUI);
    }

    public Sequence AnimatePanelsOut()
    {
        if (!positionsInitialized)
        {
            Logger.LogWarning("[ShopUI] Panel positions not initialized. Cannot animate out.");
            return DOTween.Sequence().SetUpdate(true);
        }
        
        Sequence sequence = DOTween.Sequence().SetUpdate(true); 

        if (upLine != null)
        {
            Vector2 topPanelOffScreenPosition = new Vector2(
                topPanelOnScreenPosition.x,
                topPanelOnScreenPosition.y + (upLine.rect.height > 0 ? upLine.rect.height : 300) 
            );
            sequence.Insert(0, upLine.DOAnchorPos(topPanelOffScreenPosition, animationDuration).SetEase(easeType));
        }

        if (downLine != null)
        {
            Vector2 bottomPanelOffScreenPosition = new Vector2(
                bottomPanelOnScreenPosition.x,
                bottomPanelOnScreenPosition.y - (downLine.rect.height > 0 ? downLine.rect.height : 300) 
            );
            sequence.Insert(0, downLine.DOAnchorPos(bottomPanelOffScreenPosition, animationDuration).SetEase(easeType));
        }
        return sequence;
    }

    private Sprite GetIconForPurchaseDesign(ShipDesign design)
    {
        return defaultShipPurchaseIcon;
    }

    private Sprite GetIconForUpgradeShip(ShipProxy.ClientProxy shipProxy)
    {
        return defaultShipUpgradeIcon;
    }


    private void RefreshPurchaseItemsDisplayInternal()
    {
        if (_localEscadreProxy == null)
        {
            ClearShopLine(contentUpLine, "UpLine_Purchases", _activePurchaseItemUIs);
            return;
        }
        List<ShipDesign> allDesigns = _localEscadreProxy.AvailableShopDesigns;
        if (allDesigns == null || !allDesigns.Any())
        {
            ClearShopLine(contentUpLine, "UpLine_Purchases", _activePurchaseItemUIs);
            return;
        }
        PopulatePurchaseItemsLine(contentUpLine, allDesigns, "UpLine_Purchases");
    }

    private void RefreshUpgradeItemsDisplayInternal()
    {
        if (_localEscadreProxy == null || _clientComposer == null || _clientComposer.ClientLevel == null)
        {
            ClearShopLine(contentDownLine, "DownLine_Upgrades", _activeUpgradeItemUIs);
            return;
        }
        List<ShipProxy.ClientProxy> playerShips = _localEscadreProxy.FormationSlots
            .Where(slot => slot.ShipEntityId.HasValue)
            .Select(slot => {
                _clientComposer.ClientLevel.TryGetProxy(slot.ShipEntityId.Value, out var proxy);
                return proxy as ShipProxy.ClientProxy;
            })
            .Where(shipProxy => shipProxy != null && !shipProxy.IsDestroyed)
            .ToList();

        if (!playerShips.Any())
        {
            ClearShopLine(contentDownLine, "DownLine_Upgrades", _activeUpgradeItemUIs);
            return;
        }
        PopulateUpgradeItemsLine(contentDownLine, playerShips, "DownLine_Upgrades");
    }

    
    void ClearShopLine<T>(Transform contentParent, string lineName, List<T> activeItemsList) where T : MonoBehaviour
    {
        if (contentParent == null) return;
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        activeItemsList.Clear();
    }

    void PopulatePurchaseItemsLine(Transform contentParent, List<ShipDesign> designs, string lineName)
    {
        if (contentParent == null || productButtonPrefab == null)
        {
            Logger.LogError($"[ShopUI] PopulatePurchaseItemsLine for {lineName}: Missing contentParent or productButtonPrefab.");
            return;
        }
        ClearShopLine(contentParent, lineName, _activePurchaseItemUIs); // Already cleared by internal caller

        if (!designs.Any()) return;

        int playerResources = _localEscadreProxy?.Resources ?? 0;

        foreach (var design in designs)
        {
            GameObject itemGO = Instantiate(productButtonPrefab, contentParent);
            itemGO.name = $"PurchaseItem_ID_{design.DesignId}_{design.Name.Replace(" ", "")}";

            ShopItemUI itemUI = itemGO.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                bool canAfford = playerResources >= design.Cost;
                itemUI.Setup(design, GetIconForPurchaseDesign(design), HandlePurchaseItemClick, canAfford);
                _activePurchaseItemUIs.Add(itemUI);
            }
            else
            {
                Logger.LogError($"[ShopUI] PopulatePurchaseItemsLine for {lineName}: ShopItemUI script not found on prefab for design ID: {design.DesignId}!");
            }
        }
    }
    
    void PopulateUpgradeItemsLine(Transform contentParent, List<ShipProxy.ClientProxy> ships, string lineName) 
    {
        if (contentParent == null || shipUpgradeItemPrefab == null)
        {
            Logger.LogError($"[ShopUI] PopulateUpgradeItemsLine for {lineName}: Missing contentParent or shipUpgradeItemPrefab.");
            return;
        }
        ClearShopLine(contentParent, lineName, _activeUpgradeItemUIs); // Already cleared by internal caller

        if (!ships.Any()) return;

        int playerResources = _localEscadreProxy?.Resources ?? 0;

        foreach (var shipProxy in ships)
        {
            GameObject itemGO = Instantiate(shipUpgradeItemPrefab, contentParent);
            itemGO.name = $"UpgradeItem_ShipID_{shipProxy.EntityId}";

            ShipUpgradeItemUI itemUI = itemGO.GetComponent<ShipUpgradeItemUI>();
            if (itemUI != null)
            {
                int upgradeCost = CalculateDynamicUpgradeCost(shipProxy.EntityId); 
                bool canAfford = playerResources >= upgradeCost;
                itemUI.Setup(shipProxy, GetIconForUpgradeShip(shipProxy), HandleUpgradeItemClick, canAfford, upgradeCost);
                _activeUpgradeItemUIs.Add(itemUI);
            }
            else
            {
                Logger.LogError($"[ShopUI] PopulateUpgradeItemsLine for {lineName}: ShipUpgradeItemUI script not found on prefab for Ship ID: {shipProxy.EntityId}!");
            }
        }
    }

    private int CalculateDynamicUpgradeCost(int shipEntityId)
    {
        return 750; 
    }

    void HandlePurchaseItemClick(string designIdString)
    {
        if (_clientComposer == null || _clientComposer.GameActions == null || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed)
        {
            Logger.LogError("[ShopUI] Cannot handle purchase: Critical components missing or proxy destroyed.");
            return;
        }
        if (int.TryParse(designIdString, out int designId))
        {
            Logger.Log($"[ShopUI] Attempting to purchase item with Design ID: {designId}");
            float yOffset = _localEscadreProxy.FormationSlots.Count * 2.5f;
            if (_localEscadreProxy.FormationSlots.Count % 2 == 1) yOffset *= -1;
            Core.Primitives.Vector2 preferredOffset = new Core.Primitives.Vector2(
                _localEscadreProxy.FormationSlots.Count * 1.0f, yOffset
            );
            _clientComposer.GameActions.RequestBuyShip(designId, preferredOffset);
        }
        else
        {
            Logger.LogError($"[ShopUI] Could not parse designIdString '{designIdString}' for purchase.");
        }
    }
    
    void HandleUpgradeItemClick(int shipEntityId) 
    {
        if (_clientComposer == null || _clientComposer.GameActions == null || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed)
        {
            Logger.LogError("[ShopUI] Cannot handle upgrade: Critical components missing or proxy destroyed.");
            return;
        }
        Logger.Log($"[ShopUI] Attempting to upgrade ship with Entity ID: {shipEntityId}");
        _clientComposer.GameActions.RequestUpgradeShip(shipEntityId);
    }
}