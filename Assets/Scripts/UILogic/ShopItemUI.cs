// Scripts/UI/ShopItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Core.Model; // Added for ShipDesign

public class ShopItemUI : MonoBehaviour
{
    [SerializeField] private Image productImage;
    [SerializeField] private TMP_Text productNameText; // Assuming you want to display the name
    [SerializeField] private TMP_Text productCostText;
    [SerializeField] private Button purchaseButton;

    private string itemDesignId_Internal; // Store DesignId as string for the callback
    private Action<string> onPurchaseClicked_Callback;

    protected virtual void Awake() // Changed to protected virtual for consistency if extended
    {
        if (purchaseButton == null)
        {
            // Try to get it from children if not directly on this GO
            purchaseButton = GetComponentInChildren<Button>(); 
            if (purchaseButton == null)
            {
                Debug.LogWarning($"ShopItemUI on {gameObject.name}: PurchaseButton not found. Click functionality will be disabled.");
            }
        }
        purchaseButton?.onClick.AddListener(HandleClick);
    }

    /// <summary>
    /// Sets up the shop item UI element with data from a ShipDesign.
    /// </summary>
    /// <param name="design">The ShipDesign data.</param>
    /// <param name="icon">The sprite icon for the ship.</param>
    /// <param name="purchaseCallback">Callback action when purchase is attempted, passing the DesignId as a string.</param>
    /// <param name="isAffordable">Whether the player can currently afford this item.</param>
    public void Setup(ShipDesign design, Sprite icon, Action<string> purchaseCallback, bool isAffordable)
    {
        if (design == null)
        {
            Debug.LogError("ShopItemUI.Setup: ShipDesign is null. Cannot setup item.");
            gameObject.SetActive(false);
            return;
        }

        itemDesignId_Internal = design.DesignId.ToString();
        onPurchaseClicked_Callback = purchaseCallback;

        if (productImage != null) productImage.sprite = icon;
        else Debug.LogWarning($"ShopItemUI ({design.Name}): productImage is null.");

        if (productNameText != null) productNameText.text = design.Name;
        // else Debug.LogWarning($"ShopItemUI ({design.Name}): productNameText is null."); // Optional: uncomment if name is critical

        if (productCostText != null) productCostText.text = design.Cost.ToString("N0"); // "N0" for number format with commas
        else Debug.LogWarning($"ShopItemUI ({design.Name}): productCostText is null.");
        
        if (purchaseButton != null)
        {
            purchaseButton.interactable = isAffordable;
        }
    }
    
    // Kept original simple string-based setup in case it's used elsewhere,
    // but recommend transitioning to the ShipDesign based one.
    public void Setup(string id, Sprite icon, string itemName, string cost, Action<string> purchaseCallback)
    {
        itemDesignId_Internal = id;
        onPurchaseClicked_Callback = purchaseCallback;

        if (productImage != null) productImage.sprite = icon;
        if (productNameText != null) productNameText.text = itemName;
        if (productCostText != null) productCostText.text = cost;
    }
    
    public void Setup(string id, Sprite icon, string cost, Action<string> purchaseCallback)
    {
        itemDesignId_Internal = id;
        onPurchaseClicked_Callback = purchaseCallback;

        if (productImage != null) productImage.sprite = icon;
        if (productCostText != null) productCostText.text = cost;
    }


    private void HandleClick()
    {
        if (string.IsNullOrEmpty(itemDesignId_Internal))
        {
            Debug.LogError("ShopItemUI: HandleClick called, but itemDesignId_Internal is not set!");
            return;
        }
        Debug.Log($"ShopItemUI: Purchase button clicked for item Design ID (string): {itemDesignId_Internal}");
        onPurchaseClicked_Callback?.Invoke(itemDesignId_Internal);
    }

    public void UpdateAffordability(bool isAffordable)
    {
        if (purchaseButton != null)
        {
            purchaseButton.interactable = isAffordable;
        }
    }
}