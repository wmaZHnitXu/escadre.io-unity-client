// Scripts/UI/ShopUI.cs
using UnityEngine;
using System.Collections.Generic; // Для List
using Assets.Scripts.UILogic; // Для UIScreen

// Временная структура данных для товара, замените на свою реальную
public class ShopItemData
{
    public string Id;
    public string Name;
    public int Cost;
    public Sprite Icon; // Или путь к спрайту
    // Другие данные о товаре
}


public class ShopUI : UIScreen
{
    [SerializeField] private Transform contentUpLine;
    [SerializeField] private Transform contentDownLine;
    [SerializeField] private GameObject productButtonPrefab;
 // TODO: Заменить это на реальное получение данных о товарах
    private List<ShopItemData> upLineItems = new List<ShopItemData>();
    private List<ShopItemData> downLineItems = new List<ShopItemData>();

    protected override void Awake()
    {
        base.Awake();
// TODO: Получить ссылки на UIManager, если он нужен для чего-то еще
        Debug.Log("ShopUI Awake: Checking references...");
        if (contentUpLine == null) Debug.LogError("ShopUI Error: contentUpLine is NOT assigned!");
        if (contentDownLine == null) Debug.LogError("ShopUI Error: contentDownLine is NOT assigned!");
        if (productButtonPrefab == null) Debug.LogError("ShopUI Error: productButtonPrefab is NOT assigned!");
    }

    protected override void OnShow()
    {
        base.OnShow();
        Debug.Log("ShopUI OnShow: Loading and populating items...");
        LoadShopItems(); 
        PopulateShopLine(contentUpLine, upLineItems, "UpLine"); // Добавим имя для логов
        PopulateShopLine(contentDownLine, downLineItems, "DownLine"); // Добавим имя для логов
    }
    
    void LoadShopItems()
    {
/* TODO: Загрузить спрайты */ 
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

    void PopulateShopLine(Transform contentParent, List<ShopItemData> items, string lineName) // Добавили lineName
    {
        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Checking prerequisites...");
        if (contentParent == null || productButtonPrefab == null)
        {
            Debug.LogError($"ShopUI PopulateShopLine for {lineName}: Content parent or product button prefab is not assigned! Aborting population for this line.");
            return;
        }
        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Parent '{contentParent.name}' is active in hierarchy: {contentParent.gameObject.activeInHierarchy}");
        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Prefab '{productButtonPrefab.name}' is assigned.");

        // Очищаем предыдущие элементы
        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Clearing {contentParent.childCount} existing children from {contentParent.name}...");
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        // Даем один кадр на удаление объектов, чтобы LayoutGroup успел обновиться перед добавлением новых (иногда помогает)
        // yield return null; // Для этого PopulateShopLine должен быть корутиной, пока уберем

        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Populating with {items.Count} items...");
        if (items.Count == 0)
        {
            Debug.LogWarning($"ShopUI PopulateShopLine for {lineName}: No items to populate.");
            return;
        }

        foreach (var itemData in items)
        {
            Debug.Log($"ShopUI PopulateShopLine for {lineName}: Instantiating prefab for item ID: {itemData.Id}");
            GameObject itemGO = Instantiate(productButtonPrefab, contentParent);
            if (itemGO == null)
            {
                Debug.LogError($"ShopUI PopulateShopLine for {lineName}: Failed to instantiate prefab for item ID: {itemData.Id}!");
                continue;
            }
            itemGO.name = $"ShopItem_{itemData.Id}"; // Даем осмысленное имя для отладки в иерархии
            Debug.Log($"ShopUI PopulateShopLine for {lineName}: Instantiated '{itemGO.name}', parent: '{itemGO.transform.parent?.name}', active: {itemGO.activeSelf}");


            ShopItemUI itemUI = itemGO.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                Debug.Log($"ShopUI PopulateShopLine for {lineName}: Setting up UI for item ID: {itemData.Id}");
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
        Debug.Log($"ShopUI PopulateShopLine for {lineName}: Finished populating.");
    }

    void HandleItemPurchase(string itemId)
    {
        Debug.Log($"ShopUI: Attempting to purchase item with ID: {itemId}");
        // TODO: Реализовать логику покупки
        // Например, вызов MasterServerApiService.Instance.PurchaseItemAsync(itemId);
        // И обработка ответа (показ сообщения об успехе/ошибке, обновление баланса и т.д.)
        // Может быть, показать диалог подтверждения покупки.
    }
}