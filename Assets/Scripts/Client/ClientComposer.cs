// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client; 
using Core.Logging;
using Core.Time; 
using Logger = Core.Logging.Logger;
using System.IO;
using System.Linq;
using System.Collections.Generic; // For List, Tuple
using Core.Primitives; // For Vector2
using System;
using Vector2 = Core.Primitives.Vector2;
using Core.Model;
using Core.Network.Proxies; // For Tuple

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
    private bool isSessionFullyActive = false; // Renamed for clarity


    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess; 
    private IClock _clientClock; 
    private ClientGameActions _gameActions; 
    public ClientEscadreState LocalEscadreState { get; private set; } 

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
        mockNetworkLayer.defaultSendingClientId = thisClientInstanceId; 


        _clientLevel = new ClientLevel(_clientClock); 
        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel, _clientClock);
        _gameActions = new ClientGameActions(_clientNetworkAccess, thisClientInstanceId);

        var localEscadreProxy = _entityManager.GetOrCreateEscadreProxy(thisClientInstanceId);
        LocalEscadreState = localEscadreProxy.State; 
        
        // Optional: Subscribe to events for logging confirmation when state arrives
        LocalEscadreState.OnShopDesignsChanged += HandleShopDesignsChanged_Debug;
        LocalEscadreState.OnFormationChanged += HandleFormationChanged_Debug;
        LocalEscadreState.OnResourcesChanged += HandleResourcesChanged_Debug;


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

    // --- Debug Event Handlers ---
    private void HandleShopDesignsChanged_Debug() { Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Shop designs updated. Count: {LocalEscadreState.AvailableShopDesigns.Count}"); }
    private void HandleFormationChanged_Debug() { Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Formation updated. Slot Count: {LocalEscadreState.FormationSlots.Count}"); }
    private void HandleResourcesChanged_Debug() { Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Resources updated. Amount: {LocalEscadreState.Resources}"); }
    // ---------------------------


    void OnEnable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnEnable called.");
        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.RegisterMockClientS2CRouting(thisClientInstanceId);
        }
        if (!isConnectionAttempted && !isSessionFullyActive) 
        {
            RequestConnection();
        }
        else if (isConnectionAttempted && !isSessionFullyActive)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Connection previously attempted but session not fully active. Waiting for server data.");
        }
    }

    private void RequestConnection()
    {
        if (_clientNetworkAccess == null) { Logger.LogError($"[ClientComposer {thisClientInstanceId}] Network layer unavailable for connection request."); return; }
        
        isConnectionAttempted = true; 
        isSessionFullyActive = false; 

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

        if (!isSessionFullyActive && isConnectionAttempted)
        {
            bool entitiesExist = _clientLevel != null && _clientLevel.ActiveProxies.Any();
            bool shopInfoExists = LocalEscadreState != null && LocalEscadreState.AvailableShopDesigns.Any();
            bool formationInfoExists = LocalEscadreState != null && LocalEscadreState.FormationSlots.Any(s => s.ShipEntityId.HasValue);

            if (entitiesExist && shopInfoExists && formationInfoExists)
            {
                isSessionFullyActive = true; 
                Logger.Log($"[ClientComposer {thisClientInstanceId}] Game session is now FULLY active (initial entity, shop designs, and formation info received).");
            }
            else
            {
                // Log periodically (e.g., every 2 seconds = 120 frames at 60fps) which conditions are failing
                if (Time.frameCount > 60 && Time.frameCount % 120 == 0) // Start logging after 1s, then every 2s
                {
                    string reasons = "";
                    if (!entitiesExist) reasons += "Waiting for initial entity proxy. ";
                    if (!shopInfoExists) reasons += "Waiting for shop designs info. ";
                    if (!formationInfoExists) reasons += "Waiting for initial formation info (with ship ID). ";
                    Logger.Log($"[ClientComposer {thisClientInstanceId}] Session not fully active yet. Reason(s): {reasons}");
                }
            }
        }


        if (_clientLevel != null)
        {
            _clientLevel.DoUpdate(Time.deltaTime); 
        }
    }

    void OnDisable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDisable called.");
        if (_clientNetworkAccess != null && isConnectionAttempted)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Sending _ClientDisconnect message.");
            _clientNetworkAccess.SendToServer(0, MessageType._ClientDisconnect, writer => {});
        }
        isSessionFullyActive = false; 
        isConnectionAttempted = false; 

        if (mockNetworkLayer != null)
        {
            mockNetworkLayer.UnregisterMockClientS2CRouting(thisClientInstanceId);
        }
    }


    [ContextMenu("Shop: Buy DefaultShip (Slot near last)")]
    public void MockBuyDefaultShip()
    {
        if (!isSessionFullyActive) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not fully active. Cannot buy ship yet. Please wait for initial server data."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }
        if (LocalEscadreState == null || !LocalEscadreState.AvailableShopDesigns.Any())
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Shop designs not yet available on client or LocalEscadreState is null. This should be covered by 'isSessionFullyActive'.");
            return;
        }

        var design = LocalEscadreState.AvailableShopDesigns.FirstOrDefault(d => d.ShipEntityType == Entity.EntityTypeEnum.DefaultShip);
        if (design == null)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] DefaultShip design not found in shop info. Available: {string.Join(", ", LocalEscadreState.AvailableShopDesigns.Select(x => x.Name))}");
            return;
        }
        
        float newX = 0;
        float newY = LocalEscadreState.FormationSlots.Count * 2.5f; 
        _gameActions.RequestBuyShip(design.DesignId, new Vector2(newX, newY)); 
    }

    [ContextMenu("Shop: Upgrade First Ship")]
    public void MockUpgradeFirstShip()
    {
        if (!isSessionFullyActive) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not fully active. Cannot upgrade ship yet. Please wait for initial server data."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }
        if (LocalEscadreState == null || !LocalEscadreState.FormationSlots.Any(s => s.ShipEntityId.HasValue)) 
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Client formation data not yet available, no ships in formation, or LocalEscadreState is null. This should be covered by 'isSessionFullyActive'.");
            return; 
        }

        var firstShipSlot = LocalEscadreState.FormationSlots.FirstOrDefault(s => s.ShipEntityId.HasValue);
        if (firstShipSlot == null || !firstShipSlot.ShipEntityId.HasValue) 
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ship with an ID found in the current formation slots to upgrade (unexpected if 'isSessionFullyActive' is true and this path is reached).");
            return;
        }
        _gameActions.RequestUpgradeShip(firstShipSlot.ShipEntityId.Value);
    }

    [ContextMenu("Formation: Set Random Valid Formation")]
    public void MockSetRandomFormation()
    {
        if (!isSessionFullyActive) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not fully active. Cannot set formation yet. Please wait for initial server data."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }

        var shipsInFormation = LocalEscadreState.FormationSlots.Where(s => s.ShipEntityId.HasValue).ToList();
        if (LocalEscadreState == null || !shipsInFormation.Any()) 
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Client formation data not yet available or no ships in formation to rearrange. This should be covered by 'isSessionFullyActive'.");
            return; 
        }
        
        var newLayout = new List<Tuple<int, Vector2>>();
        float angleStep = 360f / shipsInFormation.Count;
        float radius = 3f + (shipsInFormation.Count * 0.5f); 

        for(int i=0; i < shipsInFormation.Count; i++)
        {
            var slot = shipsInFormation[i];
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector2 newOffset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            newLayout.Add(new Tuple<int, Vector2>(slot.ShipEntityId.Value, newOffset));
        }

        if (newLayout.Any())
        {
            _gameActions.RequestSetFormation(newLayout);
        } else { 
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ships with IDs found in local formation state to set (unexpected).");
        }
    }

    void OnDestroy()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDestroy: Cleaning up...");
        if (LocalEscadreState != null)
        {
            LocalEscadreState.OnShopDesignsChanged -= HandleShopDesignsChanged_Debug;
            LocalEscadreState.OnFormationChanged -= HandleFormationChanged_Debug;
            LocalEscadreState.OnResourcesChanged -= HandleResourcesChanged_Debug;
        }

        _entityManager?.Dispose(); 
        _entityManager = null;
        _gameActions = null;
        LocalEscadreState = null; 
        _clientNetworkAccess = null;
        _clientClock = null; 
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Cleanup complete.");
    }
}