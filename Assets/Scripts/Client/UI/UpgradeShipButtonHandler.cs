// File: Scripts/Client/UI/UpgradeShipButtonHandler.cs
using Logger = Core.Logging.Logger;
using System.Linq; // For FirstOrDefault

namespace Client.UI
{
    public class UpgradeShipButtonHandler : BaseActionButtonHandler
    {
        // For simplicity, this upgrades the first available ship.
        // Could be extended to allow specifying which ship or cycling through ships.
        protected override void OnButtonClicked()
        {
            var firstShipSlot = _localEscadreProxy.FormationSlots.FirstOrDefault(s => s.ShipEntityId.HasValue);
            if (firstShipSlot == null || !firstShipSlot.ShipEntityId.HasValue)
            {
                Logger.LogWarning($"[{GetType().Name}] No ship in formation to upgrade.");
                return;
            }

            // Here, we'd ideally check if the ship can be upgraded (e.g., based on its current level or type)
            // and if the player has enough resources for THAT specific upgrade.
            // This requires more detailed shop/upgrade info from the server, which is not currently in EscadreProxy.
            // For now, we just send the request. The server will validate.

            _clientComposer.GameActions.RequestUpgradeShip(firstShipSlot.ShipEntityId.Value);
            Logger.Log($"[{GetType().Name}] Upgrade Ship action sent for ShipID: {firstShipSlot.ShipEntityId.Value}.");
        }

        // Optionally, update interactable state based on resource availability for the *cheapest* upgrade
        // or if any ship is upgradable. This is complex without detailed upgrade cost info on client.
        protected override void UpdateInteractableState()
        {
            base.UpdateInteractableState();
            if (!_button.interactable) return;

            if (_localEscadreProxy != null)
            {
                bool canUpgradeAnyShip = _localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue);
                // Add resource check here if upgrade costs were available on the client for specific ships
                // For example:
                // var firstShip = _localEscadreProxy.FormationSlots.FirstOrDefault(s => s.ShipEntityId.HasValue);
                // if (firstShip != null && firstShip.ShipEntityId.HasValue) {
                //    int upgradeCost = GetUpgradeCostForShip(firstShip.ShipEntityId.Value); // Imaginary method
                //    _button.interactable = _localEscadreProxy.Resources >= upgradeCost;
                // } else {
                //    _button.interactable = false;
                // }
                _button.interactable = canUpgradeAnyShip; // Simplistic: enable if any ship exists
            }
        }
    }
}