// File: Scripts/Client/UI/CancelAttackButtonHandler.cs
using Logger = Core.Logging.Logger;
using System.Linq; // For .Any()
using Core.Network.Proxies; // For EscadreProxy

namespace Client.UI
{
    public class CancelAttackButtonHandler : BaseActionButtonHandler
    {
        protected override void OnButtonClicked()
        {
            _clientComposer.GameActions.SendCancelAttack();
            Logger.Log($"[{GetType().Name}] Cancel Attack action sent.");
        }

        // Override to subscribe/unsubscribe to specific proxy events
        protected override void OnLocalEscadreProxyChanged(EscadreProxy.ClientProxy oldProxy, EscadreProxy.ClientProxy newProxy)
        {
            // Note: _localEscadreProxy in the base class is already updated to newProxy by the time this is called.
            // The base class will call UpdateInteractableState() after this method returns.

            if (oldProxy != null)
            {
                oldProxy.OnTargetEscadreEntityIdsChanged -= UpdateInteractableState;
            }

            if (newProxy != null)
            {
                newProxy.OnTargetEscadreEntityIdsChanged += UpdateInteractableState;
            }
            // No need to call UpdateInteractableState() here, base class does it.
        }

        // Override to provide specific conditions for this button's interactability
        protected override void UpdateInteractableState()
        {
            base.UpdateInteractableState(); // Apply base conditions (session active, escadre ok)

            if (!_button.interactable) // If base conditions already made it not interactable, stop here
            {
                return;
            }

            // Specific condition for "Cancel Attack":
            // Button should be interactable only if the local escadre is currently targeting something.
            if (_localEscadreProxy != null) // _localEscadreProxy is up-to-date here
            {
                _button.interactable = _localEscadreProxy.TargetEscadreEntityIds.Any();
            }
            else
            {
                _button.interactable = false; // No local escadre, so cannot be attacking
            }
        }

        // OnDestroy in the base class will handle unsubscribing from ClientComposer.
        // We need to ensure our specific subscription is cleaned up if the handler is destroyed
        // while a proxy is still active. The base OnDestroy calls OnLocalEscadreProxyChanged(currentProxy, null)
        // which will trigger our override above to unsubscribe from the currentProxy.
    }
}