// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client; // For ClientEntityManager and ClientLevel
using Core.Logging;
using Logger = Core.Logging.Logger;

public class ClientComposer : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    [Tooltip("Assign the MockNetworkLayer GameObject from the scene here.")]
    private MockNetworkLayer mockNetworkLayer;

    [Header("Presentation")]
    [SerializeField]
    [Tooltip("Assign the ClientPresentationManager GameObject/Component from the scene here.")]
    private ClientPresentationManager clientPresentationManager;

    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess;

    void Awake()
    {
        Logger.Log("[ClientComposer] Awake: Initializing Client Logic...");

        if (mockNetworkLayer == null)
        {
            Logger.LogError("[ClientComposer] MockNetworkLayer not assigned in Inspector! Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError("[ClientComposer] MockNetworkLayer not found. Client cannot function.");
                enabled = false;
                return;
            }
            Logger.LogWarning("[ClientComposer] MockNetworkLayer was found in scene. Please assign it in the Inspector for robustness.");
        }
        _clientNetworkAccess = mockNetworkLayer;

        _clientLevel = new ClientLevel();
        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel);

        if (clientPresentationManager == null)
        {
            clientPresentationManager = GetComponent<ClientPresentationManager>();
            if (clientPresentationManager == null)
            {
                Logger.LogWarning("[ClientComposer] ClientPresentationManager not assigned and not found on this GameObject. Visuals will not be created.");
            }
        }

        if (clientPresentationManager != null)
        {
            clientPresentationManager.Initialize(_clientLevel); // PresentationManager subscribes to ClientLevel events
            Logger.Log("[ClientComposer] ClientPresentationManager initialized.");
        }

        if (mockNetworkLayer != null)
        {
            // Let the mock network layer know this client "exists" for broadcast purposes, if it uses such a list
            mockNetworkLayer.RegisterMockClient(mockNetworkLayer.defaultSendingClientId);
        }

        Logger.Log("[ClientComposer] Client Core & Presentation Initialization complete.");
    }

    /// <summary>
    /// Unity's Update method, called every frame.
    /// This is where the client-side core logic loop should be driven.
    /// </summary>
    void Update()
    {
        if (_clientLevel != null)
        {
            _clientLevel.DoUpdate(Time.deltaTime); // <<< CRITICAL ADDITION: Drive the client-side simulation
        }
        // Other client-side frame updates can go here (e.g., input processing)
    }


    // Example method to simulate sending a command from client
    public void SimulateSendSetCourse(Core.Primitives.Vector2 destination)
    {
        if (_clientNetworkAccess == null)
        {
            Logger.LogWarning("[ClientComposer] Cannot send command, network layer not available.");
            return;
        }
        Logger.Log($"[ClientComposer] Simulating SendToServer: _SetCourse to {destination}");
        _clientNetworkAccess.SendToServer(
            0, // Escadre commands often use 0 or a player ID as entity context
            MessageType._SetCourse,
            writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, destination)
        );
    }

    void OnDestroy()
    {
        Logger.Log("[ClientComposer] OnDestroy: Cleaning up...");
        _entityManager?.Dispose(); // This should ideally handle disposing ClientLevel too if owned
        _entityManager = null;
        _clientLevel?.Dispose(); // Dispose explicitly if not handled by entityManager or if owned here
        _clientLevel = null;

        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.UnregisterMockClient(mockNetworkLayer.defaultSendingClientId);
        }
        _clientNetworkAccess = null;
        Logger.Log("[ClientComposer] Client Core Cleanup complete.");
    }
}