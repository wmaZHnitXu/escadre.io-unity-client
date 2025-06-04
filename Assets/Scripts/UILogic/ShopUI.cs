// Scripts/UI/ShopUI.cs
using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts.UILogic; // Убедитесь, что это пространство имен существует или замените его
using DG.Tweening;
using System.Collections;
using UnityEngine.UI;

// Если UIScreenType находится в Assets.Scripts.UILogic, можно убрать using Assets.Scripts.UILogic;
// и использовать полное имя Assets.Scripts.UILogic.UIScreenType.GameUI

public class ShopItemData
{
    public string Id;
    public string Name;
    public int Cost;
    public Sprite Icon;
}

public class ShopUI : UIScreen
{
    [SerializeField] private Transform contentUpLine;
    [SerializeField] private RectTransform upLine;
    [SerializeField] private Transform contentDownLine;
    [SerializeField] private RectTransform downLine;
    [SerializeField] private GameObject productButtonPrefab;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject score;

    private List<ShopItemData> upLineItems = new List<ShopItemData>();
    private List<ShopItemData> downLineItems = new List<ShopItemData>();

    [Header("Animation Settings")]
    public float animationDuration;
    public Ease easeType = Ease.OutExpo;


    private Vector2 topPanelOnScreenPosition;
    private Vector2 bottomPanelOnScreenPosition;
    private bool positionsInitialized = false;

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = UIManager.Instance;

        Debug.Log("ShopUI Awake: Checking references...");
        if (contentUpLine == null) Debug.LogError("ShopUI Error: contentUpLine is NOT assigned!");
        if (upLine == null) Debug.LogError("ShopUI Error: upLine RectTransform is NOT assigned!");
        if (contentDownLine == null) Debug.LogError("ShopUI Error: contentDownLine is NOT assigned!");
        if (downLine == null) Debug.LogError("ShopUI Error: downLine RectTransform is NOT assigned!");
        if (productButtonPrefab == null) Debug.LogError("ShopUI Error: productButtonPrefab is NOT assigned!");
        if (exitButton == null) Debug.LogError("ShopUI Error: exitButton is NOT assigned!");


        exitButton?.onClick.AddListener(OnExitButtonClicked);

        StartCoroutine(InitializePanelPositions());
    }

    private IEnumerator InitializePanelPositions()
    {
        yield return new WaitForEndOfFrame();

        if (upLine != null)
        {
            topPanelOnScreenPosition = upLine.anchoredPosition;
            upLine.anchoredPosition = new Vector2(
                topPanelOnScreenPosition.x,
                topPanelOnScreenPosition.y + upLine.rect.height
            );
            Debug.Log($"UpLine initial off-screen Y: {upLine.anchoredPosition.y}, height: {upLine.rect.height}");
        }

        if (downLine != null)
        {
            bottomPanelOnScreenPosition = downLine.anchoredPosition;
            downLine.anchoredPosition = new Vector2(
                bottomPanelOnScreenPosition.x,
                bottomPanelOnScreenPosition.y - downLine.rect.height
            );
            Debug.Log($"DownLine initial off-screen Y: {downLine.anchoredPosition.y}, height: {downLine.rect.height}");
        }
        positionsInitialized = true;
    }


    protected override void OnShow()
    {
        base.OnShow();
        StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        while (!positionsInitialized)
        {
            yield return null;
        }

        Debug.Log("Current Time.timeScale: " + Time.timeScale);
        AnimatePanelsIn();

        Debug.Log("ShopUI OnShow: Loading and populating items...");
        LoadShopItems();
        PopulateShopLine(contentUpLine, upLineItems, "UpLine");
        PopulateShopLine(contentDownLine, downLineItems, "DownLine");
        score.SetActive(true);
    }

    public Sequence AnimatePanelsIn()
    {
        Sequence sequence = DOTween.Sequence();
        if (upLine != null)
        {
            sequence.Insert(0, upLine.DOAnchorPos(topPanelOnScreenPosition, animationDuration)
                .SetEase(easeType));
        }
        if (downLine != null)
        {
            sequence.Insert(0, downLine.DOAnchorPos(bottomPanelOnScreenPosition, animationDuration)
                .SetEase(easeType));
        }
        return sequence;
    }

    private void OnExitButtonClicked()
    {   
        score.SetActive(false);
        StartCoroutine(ExitShopCoroutine());
    }

    private IEnumerator ExitShopCoroutine()
    {
        if (uiManager == null)
        {
            Debug.LogError("UIManager is not available. Cannot switch screen.");
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
            Debug.LogWarning("Panel positions not yet initialized. Cannot animate out.");

            return DOTween.Sequence();
        }
        
        Sequence sequence = DOTween.Sequence();

        if (upLine != null)
        {
            Vector2 topPanelOffScreenPosition = new Vector2(
                topPanelOnScreenPosition.x,
                topPanelOnScreenPosition.y + upLine.rect.height 
            );
            sequence.Insert(0, upLine.DOAnchorPos(topPanelOffScreenPosition, 1)); 
        }

        if (downLine != null)
        {
            Vector2 bottomPanelOffScreenPosition = new Vector2(
                bottomPanelOnScreenPosition.x,
                bottomPanelOnScreenPosition.y - downLine.rect.height 
            );

            sequence.Insert(0, downLine.DOAnchorPos(bottomPanelOffScreenPosition, 1));
        }
        return sequence;
    }

    void LoadShopItems()
    {
        /* TODO: Upload sprites */
        Debug.Log("ShopUI LoadShopItems: Starting to load items...");
        upLineItems.Clear();
        downLineItems.Clear();

        for (int i = 0; i < 8; i++)
        {
            upLineItems.Add(new ShopItemData { 
                Id = $"up_ship_{i+1}", Name = $"Корабль Верхний {i+1}", Cost = 1000 + i * 100, Icon = null 
            });
            downLineItems.Add(new ShopItemData { 
                Id = $"down_ship_{i+1}", Name = $"Корабль Нижний {i+1}", Cost = 1500 + i * 150, Icon = null
            });
        }
        if (upLineItems.Count > 0) upLineItems[0].Cost = 1337;
        if (downLineItems.Count > 0) downLineItems[0].Cost = 1337;
        Debug.Log($"ShopUI LoadShopItems: Loaded {upLineItems.Count} items for up line, {downLineItems.Count} for down line.");
    }

    void PopulateShopLine(Transform contentParent, List<ShopItemData> items, string lineName)
    {
        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Checking prerequisites...");
        if (contentParent == null || productButtonPrefab == null)
        {
            Debug.LogError($"ShopUI PopulateShopLine for {lineName}: Content parent or product button prefab is not assigned! Aborting population for this line.");
            return;
        }
        // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Parent '{contentParent.name}' is active in hierarchy: {contentParent.gameObject.activeInHierarchy}");
        // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Prefab '{productButtonPrefab.name}' is assigned.");

        // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Clearing {contentParent.childCount} existing children from {contentParent.name}...");
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Populating with {items.Count} items...");
        if (items.Count == 0)
        {
            Debug.LogWarning($"ShopUI PopulateShopLine for {lineName}: No items to populate.");
            return;
        }

        foreach (var itemData in items)
        {
            // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Instantiating prefab for item ID: {itemData.Id}");
            GameObject itemGO = Instantiate(productButtonPrefab, contentParent);
            if (itemGO == null)
            {
                Debug.LogError($"ShopUI PopulateShopLine for {lineName}: Failed to instantiate prefab for item ID: {itemData.Id}!");
                continue;
            }
            itemGO.name = $"ShopItem_{itemData.Id}";
            // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Instantiated '{itemGO.name}', parent: '{itemGO.transform.parent?.name}', active: {itemGO.activeSelf}");


            ShopItemUI itemUI = itemGO.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Setting up UI for item ID: {itemData.Id}");
                itemUI.Setup(
                    itemData.Id, itemData.Icon, itemData.Name, 
                    itemData.Cost.ToString("N0"), HandleItemPurchase
                );
            }
            else
            {
                Debug.LogError($"ShopUI PopulateShopLine for {lineName}: ShopItemUI script not found on instantiated prefab for item ID: {itemData.Id}!");
            }
        }
        // Debug.Log($"ShopUI PopulateShopLine for {lineName}: Finished populating.");
    }
    void HandleItemPurchase(string itemId)
    {
        
        Debug.Log($"ShopUI: Attempting to purchase item with ID: {itemId}");
        // TODO: Implement the purchase logic
    }
}