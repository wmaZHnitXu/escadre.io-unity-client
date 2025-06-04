// File: Scripts/Client/UI/BaseActionButtonHandler.cs
using UnityEngine;
using UnityEngine.UI; // For Button
using Logger = Core.Logging.Logger;
using Core.Network.Proxies; // For EscadreProxy

namespace Client.UI
{
    [RequireComponent(typeof(Button))]
    public abstract class BaseActionButtonHandler : MonoBehaviour
    {
        protected Button _button;
        protected ClientComposer _clientComposer;
        protected EscadreProxy.ClientProxy _localEscadreProxy; // Made protected for access by derived
        private bool _isHandlerInitialized = false;

        protected virtual void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);
        }

        public virtual void Initialize(IUIContext uiContext)
        {
            if (_isHandlerInitialized) return;

            _clientComposer = uiContext?.GetClientComposer();
            if (_clientComposer == null)
            {
                Logger.LogError($"[{GetType().Name}] ClientComposer could not be retrieved from IUIContext. Button will be disabled.");
                _button.interactable = false;
                return;
            }

            _clientComposer.OnLocalEscadreProxyChanged += OnLocalEscadreProxyChangedInternal; // Use internal handler
            OnLocalEscadreProxyChangedInternal(_clientComposer.LocalEscadreProxy); // Initial check with current proxy

            _isHandlerInitialized = true;
            // UpdateInteractableState(); // Called by OnLocalEscadreProxyChangedInternal
            Logger.Log($"[{GetType().Name}] Initialized.");
        }

        // Internal handler that calls the virtual one
        private void OnLocalEscadreProxyChangedInternal(EscadreProxy.ClientProxy newProxy)
        {
            EscadreProxy.ClientProxy oldProxy = _localEscadreProxy;
            _localEscadreProxy = newProxy;
            OnLocalEscadreProxyChanged(oldProxy, newProxy); // Call the virtual method for derived classes
            UpdateInteractableState(); // Always update state after proxy changes
        }

        /// <summary>
        /// Called when the ClientComposer's LocalEscadreProxy changes.
        /// Derived classes can override this to subscribe/unsubscribe to events on the new/old proxy.
        /// </summary>
        /// <param name="oldProxy">The previous local escadre proxy (can be null).</param>
        /// <param name="newProxy">The new local escadre proxy (can be null).</param>
        protected virtual void OnLocalEscadreProxyChanged(EscadreProxy.ClientProxy oldProxy, EscadreProxy.ClientProxy newProxy)
        {
            // Base implementation does nothing specific with subscriptions here,
            // but derived classes can override.
            // _localEscadreProxy is already updated before this is called.
        }


        protected virtual void UpdateInteractableState()
        {
            if (!_isHandlerInitialized || _button == null || _clientComposer == null)
            {
                if(_button != null) _button.interactable = false;
                return;
            }

            bool canPerformAction = _clientComposer.IsSessionFullyActive &&
                                    _localEscadreProxy != null &&
                                    !_localEscadreProxy.IsDestroyed;
            _button.interactable = canPerformAction;
        }

        private void HandleClick()
        {
            if (!_isHandlerInitialized || _clientComposer == null || _clientComposer.GameActions == null)
            {
                Logger.LogWarning($"[{GetType().Name}] Cannot execute action: Handler not initialized or GameActions unavailable.");
                return;
            }
            if (_localEscadreProxy == null || _localEscadreProxy.IsDestroyed)
            {
                Logger.LogWarning($"[{GetType().Name}] Cannot execute action: Local Escadre not available or destroyed.");
                return;
            }
             if (!_clientComposer.IsSessionFullyActive)
            {
                 Logger.LogWarning($"[{GetType().Name}] Cannot execute action: Session not fully active.");
                return;
            }

            OnButtonClicked();
        }

        protected abstract void OnButtonClicked(); // To be implemented by derived classes

        protected virtual void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
            if (_clientComposer != null)
            {
                _clientComposer.OnLocalEscadreProxyChanged -= OnLocalEscadreProxyChangedInternal;
            }
            // Ensure derived classes also clean up their specific subscriptions if they overrode OnLocalEscadreProxyChanged
            if (_localEscadreProxy != null)
            {
                OnLocalEscadreProxyChanged(_localEscadreProxy, null); // Simulate proxy becoming null for cleanup in overrides
            }
            _isHandlerInitialized = false;
        }
    }
}