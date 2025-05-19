// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client; 
using Core.Logging;
using Core.Time; 
using Logger = Core.Logging.Logger;
using System.IO; 
using System.Linq; // Added for ActiveProxies.Any()

public class ClientComposer : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    [Tooltip("Assign the MockNetworkLayer GameObject from the scene here.")]
    private MockNetworkLayer mockNetworkLayer;
    [SerializeField]
    [Tooltip("Client ID to use for this instance (for JWT generation in mock). This will be used as the Network Source ID.")]
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
    private bool isSessionActive = false; 


    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess;
    private IClock _clientClock; 

    void Awake()
    {
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Awake: Initializing Client Logic...");
        thisClientNickname = $"{thisClientNickname}_{thisClientInstanceId}"; 

        _clientClock = new UnityClock(); 

        if (mockNetworkLayer == null)
        {
            Logger.LogError($"[ClientComposer Instance {thisClientInstanceId}] MockNetworkLayer not assigned! Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError($"[ClientComposer Instance {thisClientInstanceId}] MockNetworkLayer not found. Client cannot function.");
                enabled = false; // Disable self if critical dependency is missing
                return;
            }
            Logger.LogWarning($"[ClientComposer Instance {thisClientInstanceId}] MockNetworkLayer found in scene. Assign in Inspector for robustness.");
        }
        _clientNetworkAccess = mockNetworkLayer;
        mockNetworkLayer.defaultSendingClientId = thisClientInstanceId; // Set the network source ID for C->S messages

        // Initialize core client logic components
        // These will be further managed (disposed/recreated or reset) in OnEnable/OnDisable
        InitializeCoreLogic();
        
        if (clientPresentationManager == null)
        {
            clientPresentationManager = GetComponent<ClientPresentationManager>();
        }
        // PresentationManager is initialized here and re-initialized if core logic is reset.
        // It subscribes to ClientLevel events, so it needs a valid ClientLevel.
        if (clientPresentationManager != null && _clientLevel != null)
        {
            clientPresentationManager.Initialize(_clientLevel); 
            Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] ClientPresentationManager initialized.");
        }
        
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Client Core Components Initialized in Awake.");
    }

    private void InitializeCoreLogic()
    {
        // Dispose existing if they exist (e.g., during re-enable)
        _entityManager?.Dispose();
        _clientLevel?.Dispose();

        _clientLevel = new ClientLevel(_clientClock);
        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel, _clientClock);
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Core logic (ClientLevel, ClientEntityManager) (re)initialized.");

        // If PresentationManager was already set up, it might need to re-subscribe or be re-initialized
        // if ClientLevel instance changes. Current ClientPresentationManager.Initialize handles this.
        if (clientPresentationManager != null && clientPresentationManager.gameObject.activeInHierarchy) // Check if PM is active
        {
            clientPresentationManager.Initialize(_clientLevel); // Re-initialize with the new ClientLevel
        }
    }

    void OnEnable()
    {
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] OnEnable called.");
        // Ensure core logic components are fresh if re-enabled after disable
        if (_clientLevel == null || _entityManager == null) // Could happen if Awake didn't complete or after OnDisable
        {
            InitializeCoreLogic();
        }
        
        // Register this client instance for S->C message routing with MockNetworkLayer
        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.RegisterMockClientS2CRouting(thisClientInstanceId);
        }

        // Attempt to connect to the server
        RequestConnection();
    }

    void OnDisable()
    {
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] OnDisable called.");
        if (isSessionActive)
        {
            Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Session was active. Simulating disconnect.");
            // In a real system, send a disconnect message to the server here.
            // e.g., _clientNetworkAccess.SendToServer(0, MessageType._ClientDisconnect, writer => {});
        }

        // Clean up client-side state
        _entityManager?.Dispose();
        _entityManager = null;
        _clientLevel?.Dispose(); 
        _clientLevel = null;
        
        // Unregister this client instance from MockNetworkLayer's S->C routing
        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.UnregisterMockClientS2CRouting(thisClientInstanceId);
        }

        // Reset flags for a clean state if re-enabled
        isConnectionAttempted = false;
        isSessionActive = false;
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Client-side session state cleaned up.");
    }


    private void RequestConnection()
    {
        if (_clientNetworkAccess == null) { Logger.LogError($"[ClientComposer Instance {thisClientInstanceId}] Network layer unavailable for connection request."); return; }
        if (isConnectionAttempted) { Logger.LogWarning($"[ClientComposer Instance {thisClientInstanceId}] Connection already attempted or in progress for this enable cycle."); return; }

        isConnectionAttempted = true;
        string mockJwt = $"{thisClientInstanceId};{thisClientNickname};{thisClientIsAdmin.ToString().ToLowerInvariant()};{thisClientAuthTypeString}";
        
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Sending _ClientConnectRequest with Mock JWT: '{mockJwt}'");
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

        // Check for session activity based on receiving entities
        if (isConnectionAttempted && !isSessionActive && _clientLevel != null && _clientLevel.ActiveProxies.Any())
        {
            isSessionActive = true; 
            Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Detected active proxies. Game session is now considered active.");
        }


        if (_clientLevel != null)
        {
            _clientLevel.DoUpdate(Time.deltaTime); 
        }
    }


    public void SimulateSendSetCourse(Core.Primitives.Vector2 destination)
    {
        if (!isSessionActive) { Logger.LogWarning($"[ClientComposer Instance {thisClientInstanceId}] Cannot send command, session not active."); return; }
        if (_clientNetworkAccess == null) { Logger.LogWarning($"[ClientComposer Instance {thisClientInstanceId}] Network layer unavailable."); return; }

        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Simulating SendToServer: _SetCourse to {destination}");
        _clientNetworkAccess.SendToServer(
            0, 
            MessageType._SetCourse,
            writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, destination)
        );
    }

    void OnDestroy()
    {
        // OnDisable will have already done most of the cleanup if the component was disabled before destruction.
        // This ensures cleanup if destroyed while enabled.
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] OnDestroy: Cleaning up remaining resources...");
        if (_entityManager != null || _clientLevel != null) // If OnDisable wasn't called or didn't fully clean up
        {
            _entityManager?.Dispose();
            _entityManager = null;
            _clientLevel?.Dispose(); 
            _clientLevel = null;
        }
        
        if (mockNetworkLayer != null && Application.isPlaying) // Check Application.isPlaying to avoid issues during editor shutdown
        {
            mockNetworkLayer.UnregisterMockClientS2CRouting(thisClientInstanceId);
        }
        _clientNetworkAccess = null;
        _clientClock = null; 
        Logger.Log($"[ClientComposer Instance {thisClientInstanceId}] Client Core Cleanup complete in OnDestroy.");
    }
}