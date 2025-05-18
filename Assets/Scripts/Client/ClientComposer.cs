// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client;
using Core.Logging;
using Logger = Core.Logging.Logger;
// Assuming ClientPresentationManager is in the global namespace or its own correct client namespace
// using Client.Presentation; // If you created one

public class ClientComposer : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private MockNetworkLayer mockNetworkLayer;

    [Header("Presentation")] // New Section
    [SerializeField]
    [Tooltip("Assign the ClientPresentationManager GameObject/Component from the scene here.")]
    private ClientPresentationManager clientPresentationManager;

    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess;

    void Awake()
    {
        Logger.Log("[ClientComposer] Awake: Initializing Client Logic...");

        if (mockNetworkLayer == null) { /* ... error handling ... */
            Logger.LogError("[ClientComposer] MockNetworkLayer not assigned!");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null) { enabled = false; return; }
        }
        _clientNetworkAccess = mockNetworkLayer;

        _clientLevel = new ClientLevel();
        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel);

        // Initialize Presentation System
        if (clientPresentationManager == null)
        {
            clientPresentationManager = GetComponent<ClientPresentationManager>(); // Attempt to get if on same GO
            if (clientPresentationManager == null)
            {
                Logger.LogWarning("[ClientComposer] ClientPresentationManager not assigned and not found on this GameObject. Visuals will not be created.");
            }
        }

        if (clientPresentationManager != null)
        {
            clientPresentationManager.Initialize(_clientLevel);
            Logger.Log("[ClientComposer] ClientPresentationManager initialized.");
        }

        // _clientLevel.OnProxyAdded += HandleProxyAddedToClientLevel; // Logging moved to PresentationManager or specific views
        // _clientLevel.OnProxyRemoved += HandleProxyRemovedFromClientLevel;

        if (mockNetworkLayer != null) {
            mockNetworkLayer.RegisterMockClient(mockNetworkLayer.defaultSendingClientId);
        }

        Logger.Log("[ClientComposer] Client Core & Presentation Initialization complete.");
    }

    // private void HandleProxyAddedToClientLevel(IClientProxy proxy) { /* ... logging moved ... */ }
    // private void HandleProxyRemovedFromClientLevel(IClientProxy proxy) { /* ... logging moved ... */ }

    public void SimulateSendSetCourse(Core.Primitives.Vector2 destination) { /* ... as before ... */
        if (_clientNetworkAccess == null) { return; }
        Logger.Log($"[ClientComposer] Simulating SendToServer: _SetCourse to {destination}");
        _clientNetworkAccess.SendToServer(0, MessageType._SetCourse, writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, destination));
    }

    void OnDestroy()
    {
        Logger.Log("[ClientComposer] OnDestroy: Cleaning up...");
        // Unsubscribe from ClientLevel events if ClientComposer was directly subscribed
        // if (_clientLevel != null)
        // {
        //     _clientLevel.OnProxyAdded -= HandleProxyAddedToClientLevel;
        //     _clientLevel.OnProxyRemoved -= HandleProxyRemovedFromClientLevel;
        // }

        _entityManager?.Dispose();
        _entityManager = null;
        _clientLevel?.Dispose();
        _clientLevel = null;

        if (mockNetworkLayer != null) {
            mockNetworkLayer.UnregisterMockClient(mockNetworkLayer.defaultSendingClientId);
        }
        _clientNetworkAccess = null;
        // ClientPresentationManager will handle its own OnDestroy and unsubscriptions.
        Logger.Log("[ClientComposer] Client Core Cleanup complete.");
    }
}