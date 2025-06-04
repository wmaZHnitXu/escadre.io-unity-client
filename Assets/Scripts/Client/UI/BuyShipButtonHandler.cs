// File: Scripts/Client/UI/BuyShipButtonHandler.cs
using UnityEngine;
using Logger = Core.Logging.Logger;
using Core.Model; // For Entity.EntityTypeEnum
using System.Linq; // For FirstOrDefault

namespace Client.UI
{
    public class BuyShipButtonHandler : BaseActionButtonHandler
    {
        [Header("Ship Purchase Config")]
        [Tooltip("The Design ID of the ship to purchase when this button is clicked. Set this in the Inspector.")]
        [SerializeField] private int shipDesignIdToBuy = -1; // Example: Set to a valid DefaultShip design ID

        // Alternative: Use EntityType if Design IDs are not stable or easy to get in Inspector
        // [SerializeField] private Entity.EntityTypeEnum shipEntityTypeToBuy = Entity.EntityTypeEnum.DefaultShip;

        protected override void OnButtonClicked()
        {
            if (shipDesignIdToBuy == -1)
            {
                Logger.LogError($"[{GetType().Name}] shipDesignIdToBuy is not set. Cannot purchase ship.");
                return;
            }

            var design = _localEscadreProxy.AvailableShopDesigns.FirstOrDefault(d => d.DesignId == shipDesignIdToBuy);
            // if using EntityType:
            // var design = _localEscadreProxy.AvailableShopDesigns.FirstOrDefault(d => d.ShipEntityType == shipEntityTypeToBuy);

            if (design == null)
            {
                Logger.LogWarning($"[{GetType().Name}] Ship design with ID {shipDesignIdToBuy} not found in shop info.");
                // if using EntityType:
                // Logger.LogWarning($"[{GetType().Name}] Ship design for type {shipEntityTypeToBuy} not found.");
                return;
            }

            // Simple formation offset logic, can be made more sophisticated
            float yOffset = _localEscadreProxy.FormationSlots.Count * 2.5f;
            if (_localEscadreProxy.FormationSlots.Count % 2 == 1) yOffset *= -1;
            Core.Primitives.Vector2 preferredOffset = new Core.Primitives.Vector2(
                _localEscadreProxy.FormationSlots.Count * 1.0f,
                yOffset
            );

            _clientComposer.GameActions.RequestBuyShip(design.DesignId, preferredOffset);
            Logger.Log($"[{GetType().Name}] Buy Ship action sent for DesignID: {design.DesignId}.");
        }

        // Optionally, update interactable state based on resource availability
        protected override void UpdateInteractableState()
        {
            base.UpdateInteractableState(); // Handle base conditions (session active, escadre exists)
            if (!_button.interactable) return; // If already not interactable by base logic, no need to check cost

            if (_localEscadreProxy != null && shipDesignIdToBuy != -1)
            {
                var design = _localEscadreProxy.AvailableShopDesigns.FirstOrDefault(d => d.DesignId == shipDesignIdToBuy);
                if (design != null)
                {
                    _button.interactable = _localEscadreProxy.Resources >= design.Cost;
                }
                else
                {
                    _button.interactable = false; // Design not found, cannot buy
                }
            }
        }

        // Subscribe to resource changes to update button interactability
        public override void Initialize(IUIContext uiContext)
        {
            base.Initialize(uiContext);
            if (_clientComposer != null && _clientComposer.LocalEscadreProxy != null) // Check if proxy already exists
            {
                _clientComposer.LocalEscadreProxy.OnResourcesChanged += UpdateInteractableState;
            }
        }

        // Override OnDestroy to unsubscribe from resource changes
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_clientComposer != null && _clientComposer.LocalEscadreProxy != null)
            {
                 // Safely try to unsubscribe, proxy might be null if composer's event fired first
                var proxy = _clientComposer.LocalEscadreProxy;
                if(proxy != null) proxy.OnResourcesChanged -= UpdateInteractableState;
            }
             else if (_localEscadreProxy != null) // Fallback if composer is gone but we still have proxy ref
            {
                _localEscadreProxy.OnResourcesChanged -= UpdateInteractableState;
            }
        }
    }
}