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

public class ShopUI : UIScreen // Наследуем от UIScreen, если это отдельный экран
{
    [SerializeField] private Transform contentUpLine;   // Ссылка на "ContentUp"
    [SerializeField] private Transform contentDownLine; // Ссылка на "ContentDown"
    [SerializeField] private GameObject productButtonPrefab; // Ссылка на ваш префаб

    // TODO: Заменить это на реальное получение данных о товарах
    private List<ShopItemData> upLineItems = new List<ShopItemData>();
    private List<ShopItemData> downLineItems = new List<ShopItemData>();

    protected override void Awake()
    {
        base.Awake();
        // TODO: Получить ссылки на UIManager, если он нужен для чего-то еще
    }

    protected override void OnShow() // Или в Start, если данные статичны
    {
        base.OnShow();
        LoadShopItems(); // Загружаем или получаем данные о товарах
        PopulateShopLine(contentUpLine, upLineItems);
        PopulateShopLine(contentDownLine, downLineItems);
    }
    
    void LoadShopItems()
    {
        // ЗАГЛУШКА: Заполните эти списки реальными данными
        // Например, загрузка из ScriptableObject, JSON, или запрос к серверу
        // Для примера создадим несколько фейковых товаров
        upLineItems.Clear();
        downLineItems.Clear();

        for (int i = 0; i < 8; i++) // По 8 товаров в каждом ряду, как на макете
        {
            upLineItems.Add(new ShopItemData { 
                Id = $"up_ship_{i+1}", 
                Name = $"Корабль Верхний {i+1}", 
                Cost = 1000 + i * 100, 
                Icon = null /* TODO: Загрузить спрайт */ 
            });
            downLineItems.Add(new ShopItemData { 
                Id = $"down_ship_{i+1}", 
                Name = $"Корабль Нижний {i+1}", 
                Cost = 1500 + i * 150, 
                Icon = null /* TODO: Загрузить спрайт */ 
            });
        }
         // Для теста, чтобы цена 1337 была видна
        if (upLineItems.Count > 0) upLineItems[0].Cost = 1337;
        if (downLineItems.Count > 0) downLineItems[0].Cost = 1337;
    }

    void PopulateShopLine(Transform contentParent, List<ShopItemData> items)
    {
        if (contentParent == null || productButtonPrefab == null)
        {
            Debug.LogError("Content parent or product button prefab is not assigned!");
            return;
        }

        // Очищаем предыдущие элементы (если нужно обновлять)
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var itemData in items)
        {
            GameObject itemGO = Instantiate(productButtonPrefab, contentParent);
            ShopItemUI itemUI = itemGO.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(
                    itemData.Id, 
                    itemData.Icon, 
                    itemData.Name, 
                    itemData.Cost.ToString("N0"), // Форматируем цену, N0 - с разделителями тысяч
                    HandleItemPurchase // Передаем метод обратного вызова
                );
            }
        }
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