// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client; 
using Core.Logging;
using Core.Time; 
using Logger = Core.Logging.Logger;
// No specific using needed for ReadOnlyAttribute if it's in global scope or same assembly

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

    [Header("Debug Info")]
    [SerializeField, ReadOnly] 
    private float currentTime_Display;


    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess;
    private IClock _clientClock; 

    void Awake()
    {
        Logger.Log("[ClientComposer] Awake: Initializing Client Logic...");

        _clientClock = new UnityClock(); 

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

        _clientLevel = new ClientLevel(_clientClock); 
        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel, _clientClock);

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
            clientPresentationManager.Initialize(_clientLevel); 
            Logger.Log("[ClientComposer] ClientPresentationManager initialized.");
        }

        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.RegisterMockClient(mockNetworkLayer.defaultSendingClientId);
        }

        Logger.Log("[ClientComposer] Client Core & Presentation Initialization complete.");
    }

    void Update()
    {
        if (_clientClock != null)
        {
            currentTime_Display = _clientClock.CurrentTime;
        }

        if (_clientLevel != null)
        {
            _clientLevel.DoUpdate(Time.deltaTime); 
        }
    }


    public void SimulateSendSetCourse(Core.Primitives.Vector2 destination)
    {
        if (_clientNetworkAccess == null)
        {
            Logger.LogWarning("[ClientComposer] Cannot send command, network layer not available.");
            return;
        }
        Logger.Log($"[ClientComposer] Simulating SendToServer: _SetCourse to {destination}");
        _clientNetworkAccess.SendToServer(
            0, 
            MessageType._SetCourse,
            writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, destination)
        );
    }

    void OnDestroy()
    {
        Logger.Log("[ClientComposer] OnDestroy: Cleaning up...");
        _entityManager?.Dispose(); 
        _entityManager = null;
        _clientLevel?.Dispose(); 
        _clientLevel = null;

        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.UnregisterMockClient(mockNetworkLayer.defaultSendingClientId);
        }
        _clientNetworkAccess = null;
        _clientClock = null; 
        Logger.Log("[ClientComposer] Client Core Cleanup complete.");
    }
}