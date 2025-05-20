// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client; 
using Core.Logging;
using Core.Time; 
using Logger = Core.Logging.Logger;
using System.IO;
using System.Linq;

public class ClientComposer : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    [Tooltip("Assign the MockNetworkLayer GameObject from the scene here.")]
    private MockNetworkLayer mockNetworkLayer;
    [SerializeField]
    [Tooltip("Client ID to use for this instance (for JWT generation in mock). This acts as the Network Source ID for the mock layer.")]
    private int thisClientInstanceId = 1; 
    [SerializeField]
    [Tooltip("Nickname for this client instance (for JWT generation in mock).")]
    private string thisClientNickname = "Player";
     [SerializeField]
    [Tooltip("AuthType for this client instance (for JWT generation in mock). Options: NoAccount, Account")]
    private string thisClientAuthTypeString = "Account"; 
    [SerializeField]
    private bool thisClientIsAdmin = false;


    [Header("Presentation")]
    [SerializeField]
    [Tooltip("Assign the ClientPresentationManager GameObject/Component from the scene here.")]
    private ClientPresentationManager clientPresentationManager;

    [Header("Debug Info")]
    [SerializeField, ReadOnly] 
    private float currentTime_Display;
    [SerializeField, ReadOnly]
    private bool isConnectionAttempted = false;
    [SerializeField, ReadOnly]
    private bool isSessionActive = false; // True after successful application-level handshake


    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess;
    private IClock _clientClock; 

    void Awake()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Awake: Initializing Client Logic...");
        thisClientNickname = $"{thisClientNickname}_{thisClientInstanceId}"; 

        _clientClock = new UnityClock(); 

        if (mockNetworkLayer == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer not assigned! Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer not found. Client cannot function.");
                enabled = false;
                return;
            }
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer found in scene. Assign in Inspector for robustness.");
        }
        _clientNetworkAccess = mockNetworkLayer;
        mockNetworkLayer.defaultSendingClientId = thisClientInstanceId; // Inform mock layer of this instance's network source ID


        _clientLevel = new ClientLevel(_clientClock); 
        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel, _clientClock);

        if (clientPresentationManager == null)
        {
            clientPresentationManager = GetComponent<ClientPresentationManager>();
        }
        if (clientPresentationManager != null)
        {
            clientPresentationManager.Initialize(_clientLevel); 
            Logger.Log($"[ClientComposer {thisClientInstanceId}] ClientPresentationManager initialized.");
        }
        
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Initialization complete. Will attempt connection in OnEnable.");
    }

    void OnEnable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnEnable called.");
        // Register S2C routing with the mock layer when this component becomes active
        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.RegisterMockClientS2CRouting(thisClientInstanceId);
        }
        // Attempt connection if not already attempted or active
        if (!isConnectionAttempted && !isSessionActive)
        {
            RequestConnection();
        }
        else if (isConnectionAttempted && !isSessionActive)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Connection previously attempted but session not active. Consider re-attempt logic or server status check.");
        }
    }

    private void RequestConnection()
    {
        if (_clientNetworkAccess == null) { Logger.LogError($"[ClientComposer {thisClientInstanceId}] Network layer unavailable for connection request."); return; }
        
        isConnectionAttempted = true; // Mark that an attempt is being made
        isSessionActive = false; // Reset session status on new attempt

        string mockJwt = $"{thisClientInstanceId};{thisClientNickname};{thisClientIsAdmin.ToString().ToLowerInvariant()};{thisClientAuthTypeString}";
        
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Sending _ClientConnectRequest with Mock JWT: '{mockJwt}'");
        _clientNetworkAccess.SendToServer(0, MessageType._ClientConnectRequest, writer => 
        {
            writer.Write(mockJwt); 
        });
    }


    void Update()
    {
        if (_clientClock != null)
        {
            currentTime_Display = _clientClock.CurrentTime;
        }

        // Check if session became active (e.g., first proxy created)
        if (!isSessionActive && isConnectionAttempted && _clientLevel != null && _clientLevel.ActiveProxies.Any())
        {
            isSessionActive = true; 
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Detected active proxies. Game session is now active.");
        }


        if (_clientLevel != null)
        {
            _clientLevel.DoUpdate(Time.deltaTime); 
        }
    }

    void OnDisable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDisable called.");
        if (isSessionActive && _clientNetworkAccess != null)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Session was active. Sending _ClientDisconnect message.");
            // The payload for disconnect might be empty or could include the gameClientId if needed by a generic server handler.
            // For our mock, the server can derive gameClientId from the sourceNetworkId.
            _clientNetworkAccess.SendToServer(0, MessageType._ClientDisconnect, writer => {
                // Optionally, send game client ID if JWT isn't implicitly tied to network source ID
                // writer.Write(thisClientInstanceId); // Assuming thisClientInstanceId is also the game session ID after validation
            });
        }
        isSessionActive = false; // Session is no longer active when disabled
        isConnectionAttempted = false; // Allow re-connection attempt if re-enabled

        // Unregister S2C routing with the mock layer
        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.UnregisterMockClientS2CRouting(thisClientInstanceId);
        }
    }


    public void SimulateSendSetCourse(Core.Primitives.Vector2 destination)
    {
        if (!isSessionActive) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Cannot send command, session not active."); return; }
        if (_clientNetworkAccess == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Network layer unavailable."); return; }

        Logger.Log($"[ClientComposer {thisClientInstanceId}] Simulating SendToServer: _SetCourse to {destination}");
        _clientNetworkAccess.SendToServer(
            0, 
            MessageType._SetCourse,
            writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, destination)
        );
    }

    void OnDestroy()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDestroy: Cleaning up...");
        // OnDisable already handles sending disconnect and unregistering S2C routing.
        // Ensure EntityManager and ClientLevel are disposed.
        _entityManager?.Dispose(); 
        _entityManager = null;
        _clientLevel?.Dispose(); 
        _clientLevel = null;

        _clientNetworkAccess = null;
        _clientClock = null; 
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Cleanup complete.");
    }
}