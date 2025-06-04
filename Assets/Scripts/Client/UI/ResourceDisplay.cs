// File: Scripts/Client/UI/ResourceDisplay.cs
using UnityEngine;
using TMPro; // For TextMeshProUGUI
using Core.Network.Proxies; // For EscadreProxy
using Logger = Core.Logging.Logger; // Alias
using Client.UI; // For IUIContext

namespace Client.UI
{
    public interface IUIContext
    {
        ClientComposer GetClientComposer();
    }

    public class ResourceDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI resourcesText;
        [SerializeField] private TextMeshProUGUI resourcesText2;

        private EscadreProxy.ClientProxy _localEscadreProxy;
        private ClientComposer _clientComposer;
        private bool _isInitialized = false;

        public void Initialize(IUIContext uiContext)
        {
            if (_isInitialized)
            {
                Logger.LogWarning("[ResourceDisplay] Already initialized.");
                return;
            }

            if (resourcesText == null)
            {
                Logger.LogError("[ResourceDisplay] ResourcesText (TextMeshProUGUI) not assigned in Inspector! Cannot display resources.");
                enabled = false; // Disable component if critical UI element is missing
                return;
            }

            _clientComposer = uiContext?.GetClientComposer();
            if (_clientComposer == null)
            {
                Logger.LogError("[ResourceDisplay] ClientComposer could not be retrieved from IUIContext. Cannot function.");
                enabled = false;
                return;
            }

            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
            HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy); // Initial check

            _isInitialized = true;
            Logger.Log("[ResourceDisplay] Initialized.");
        }

        private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
        {
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnResourcesChanged -= UpdateDisplay;
            }

            _localEscadreProxy = newProxy;

            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.OnResourcesChanged += UpdateDisplay;
                UpdateDisplay(); // Initial display update
            }
            else
            {
                // Handle case where there's no local escadre (e.g., show "N/A" or hide)
                if (resourcesText != null) resourcesText.text = "N/A";
            }
        }

        private void UpdateDisplay()
        {
            if (!_isInitialized || resourcesText == null) return;

            if (_localEscadreProxy != null)
            {
                resourcesText.text = _localEscadreProxy.Resources.ToString();
                resourcesText2.text = _localEscadreProxy.Resources.ToString();
            }
            else
            {
                resourcesText.text = "N/A";
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
                _localEscadreProxy.OnResourcesChanged -= UpdateDisplay;
            }
            _isInitialized = false;
        }
    }
}