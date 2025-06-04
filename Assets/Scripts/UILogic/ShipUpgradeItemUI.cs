// Scripts/UI/ShipUpgradeItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Core.Network.Proxies; // For ShipProxy

public class ShipUpgradeItemUI : MonoBehaviour
{
    [SerializeField] private Image shipIconImage; // Optional: if you want an icon for the ship type
    [SerializeField] private TMP_Text shipIdentifierText; // To display Ship ID or a more friendly name
    [SerializeField] private TMP_Text upgradeCostText; // To display the cost of the next upgrade
    [SerializeField] private Button upgradeButton;

    private int _shipEntityId;
    private Action<int> _onUpgradeClicked_Callback; // Passes ShipEntityId

    // A simple way to get a "next upgrade cost". In a real game, this would come from game balance data.
    // For now, let's make it configurable or a simple formula.
    [SerializeField] private int baseUpgradeCost = 500; // Example, can be adjusted or made dynamic

    protected virtual void Awake()
    {
        if (upgradeButton == null)
        {
            upgradeButton = GetComponentInChildren<Button>();
        }
        upgradeButton?.onClick.AddListener(HandleClick);
    }

    /// <summary>
    /// Sets up the ship upgrade item UI.
    /// </summary>
    /// <param name="shipProxy">The client proxy of the ship to be upgraded.</param>
    /// <param name="icon">Optional icon for the ship.</param>
    /// <param name="upgradeCallback">Callback action when upgrade is attempted.</param>
    /// <param name="isAffordable">Whether the player can currently afford this upgrade.</param>
    /// <param name="currentUpgradeCost">The actual cost for this specific upgrade.</param>
    public void Setup(ShipProxy.ClientProxy shipProxy, Sprite icon, Action<int> upgradeCallback, bool isAffordable, int currentUpgradeCost)
    {
        if (shipProxy == null)
        {
            Debug.LogError("ShipUpgradeItemUI.Setup: shipProxy is null. Cannot setup item.");
            gameObject.SetActive(false);
            return;
        }

        _shipEntityId = shipProxy.EntityId;
        _onUpgradeClicked_Callback = upgradeCallback;

        if (shipIconImage != null) shipIconImage.sprite = icon;
        // else Debug.LogWarning($"ShipUpgradeItemUI (ShipID:{_shipEntityId}): shipIconImage is null.");

        if (shipIdentifierText != null)
        {
            // You might want to get a more descriptive name if available, e.g., from shipProxy.ShipTypeName
            shipIdentifierText.text = $"Ship ID: {shipProxy.EntityId}";
        }
        // else Debug.LogWarning($"ShipUpgradeItemUI (ShipID:{_shipEntityId}): shipIdentifierText is null.");

        if (upgradeCostText != null)
        {
            upgradeCostText.text = currentUpgradeCost.ToString("N0");
        }
        // else Debug.LogWarning($"ShipUpgradeItemUI (ShipID:{_shipEntityId}): upgradeCostText is null.");
        
        if (upgradeButton != null)
        {
            upgradeButton.interactable = isAffordable;
        }
    }

    private void HandleClick()
    {
        if (_shipEntityId == 0) // Or some other invalid ID marker
        {
            Debug.LogError("ShipUpgradeItemUI: HandleClick called, but _shipEntityId is not set!");
            return;
        }
        Debug.Log($"ShipUpgradeItemUI: Upgrade button clicked for ShipEntityID: {_shipEntityId}");
        _onUpgradeClicked_Callback?.Invoke(_shipEntityId);
    }

    public void UpdateAffordability(bool isAffordable)
    {
        if (upgradeButton != null)
        {
            upgradeButton.interactable = isAffordable;
        }
    }

    // Call this method if the ship this UI represents is destroyed or removed from formation
    public void Dispose()
    {
        Destroy(gameObject);
    }
}