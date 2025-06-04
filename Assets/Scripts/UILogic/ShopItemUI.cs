// Scripts/UI/ShopItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Для Action

public class ShopItemUI : MonoBehaviour
{
    [SerializeField] private Image productImage;
    //[SerializeField] private TMP_Text productNameText; // Если есть имя на кнопке
    [SerializeField] private TMP_Text productCostText;
    [SerializeField] private Button purchaseButton; // Сама кнопка, на которой висит скрипт

    private string itemId; // ID товара/корабля
    private Action<string> onPurchaseClicked; // Событие для обработки покупки

    private void Awake()
    {
        if (purchaseButton == null)
        {
            purchaseButton = GetComponent<Button>();
        }
        purchaseButton?.onClick.AddListener(HandleClick);
    }

    public void Setup(string id, Sprite icon, string itemName, string cost, Action<string> purchaseCallback)
    {
        itemId = id;
        onPurchaseClicked = purchaseCallback;

        if (productImage != null) productImage.sprite = icon;
        //if (productNameText != null) productNameText.text = itemName;
        if (productCostText != null) productCostText.text = cost;
    }

    public void Setup(string id, Sprite icon, string cost, Action<string> purchaseCallback)
    {
        itemId = id;
        onPurchaseClicked = purchaseCallback;

        if (productImage != null) productImage.sprite = icon;
        //if (productNameText != null) productNameText.text = itemName;
        if (productCostText != null) productCostText.text = cost;
    }

    private void HandleClick()
    {
        Debug.Log($"Purchase button clicked for item: {itemId}");
        onPurchaseClicked?.Invoke(itemId);
    }
}