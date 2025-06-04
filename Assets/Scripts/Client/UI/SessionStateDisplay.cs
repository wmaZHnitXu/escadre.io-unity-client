// File: Scripts/Client/UI/SessionStateDisplay.cs
using UnityEngine;
using TMPro; // For TextMeshProUGUI if game over text needs updating
using Logger = Core.Logging.Logger;
using Core.Network.Proxies; // For EscadreProxy to check if it's defeated

namespace Client.UI
{
    public class SessionStateDisplay : MonoBehaviour
    {
        [Header("UI Group References")]
        [SerializeField] private GameObject sessionLoadingUIGroup;
        [SerializeField] private GameObject gameplayUIGroup;
        [SerializeField] private GameObject gameOverUIGroup;
        [SerializeField] private TextMeshProUGUI gameOverTextElement; // Optional, if you want to customize game over message

        private ClientComposer _clientComposer;
        private EscadreProxy.ClientProxy _localEscadreProxy;
        private bool _isInitialized = false;

        public void Initialize(IUIContext uiContext)
        {
            if (_isInitialized)
            {
                Logger.LogWarning("[SessionStateDisplay] Already initialized.");
                return;
            }

            _clientComposer = uiContext?.GetClientComposer();
            if (_clientComposer == null)
            {
                Logger.LogError("[SessionStateDisplay] ClientComposer could not be retrieved from IUIContext. Cannot function.");
                enabled = false;
                return;
            }

            // Validate UI group references
            if (sessionLoadingUIGroup == null) Logger.LogWarning("[SessionStateDisplay] Session Loading UI Group not assigned.");
            if (gameplayUIGroup == null) Logger.LogWarning("[SessionStateDisplay] Gameplay UI Group not assigned.");
            if (gameOverUIGroup == null) Logger.LogWarning("[SessionStateDisplay] Game Over UI Group not assigned.");


            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
            HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy); // Initial check

            // Initial UI state based on composer's readiness
            UpdateDisplay();

            _isInitialized = true;
            Logger.Log("[SessionStateDisplay] Initialized.");
        }

        private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
        {
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnDestroyed -= OnLocalEscadreDefeated;
            }

            _localEscadreProxy = newProxy;

            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnDestroyed += OnLocalEscadreDefeated;
            }
            UpdateDisplay(); // State might have changed
        }

        private void OnLocalEscadreDefeated()
        {
            Logger.Log("[SessionStateDisplay] Heard that local escadre was defeated/destroyed.");
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (!_isInitialized || _clientComposer == null) return;

            bool sessionFullyActive = _clientComposer.IsSessionFullyActive;
            bool escadreExistsAndNotDestroyed = (_localEscadreProxy != null && !_localEscadreProxy.IsDestroyed);
            bool connectionAttempted = _clientComposer.isConnectionAttempted;

            bool showLoading = false;
            bool showGameplay = false;
            bool showGameOver = false;

            if (!connectionAttempted) // Not even tried to connect yet
            {
                showLoading = true;
            }
            else if (!sessionFullyActive && !escadreExistsAndNotDestroyed) // Tried connecting, but not active and no escadre (or escadre is destroyed)
            {
                // If connection was attempted but we don't have an active session AND no valid escadre,
                // it could be loading, or it could be game over if an escadre *was* present and then destroyed.
                // The key is distinguishing initial loading from post-defeat.
                // We can assume if an escadre proxy existed and is NOW destroyed, it's game over.
                // If an escadre proxy *never* existed for this client, and session isn't active, it's still loading.
                if (_localEscadreProxy != null && _localEscadreProxy.IsDestroyed) {
                    showGameOver = true;
                     if (gameOverTextElement != null) gameOverTextElement.text = "DEFEATED";
                } else {
                    showLoading = true; // Still waiting for session to become fully active or for the escadre
                }
            }
            else if (sessionFullyActive && escadreExistsAndNotDestroyed) // Session is active and player is in game
            {
                showGameplay = true;
            }
            else if (escadreExistsAndNotDestroyed && !sessionFullyActive) // Escadre exists but session not fully ready (e.g. waiting on ocean)
            {
                showLoading = true; // Still consider it loading until all components are ready
            }
            else if (!escadreExistsAndNotDestroyed && connectionAttempted) // No valid escadre, but connection was made (implies defeat or never got one)
            {
                 showGameOver = true; // Likely game over
                 if (gameOverTextElement != null) gameOverTextElement.text = "DEFEATED";
            }


            if (sessionLoadingUIGroup != null) sessionLoadingUIGroup.SetActive(showLoading);
            if (gameplayUIGroup != null) gameplayUIGroup.SetActive(showGameplay);
            if (gameOverUIGroup != null) gameOverUIGroup.SetActive(showGameOver);
        }

        // Call UpdateDisplay periodically or on specific composer events if IsSessionFullyActive can change
        void Update()
        {
            if (_isInitialized && _clientComposer != null && Time.frameCount % 30 == 0) // Check every 30 frames
            {
                UpdateDisplay();
            }
        }

        void OnDestroy()
        {
            if (_clientComposer != null)
            {
                _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            }
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnDestroyed -= OnLocalEscadreDefeated;
            }
            _isInitialized = false;
        }
    }
}