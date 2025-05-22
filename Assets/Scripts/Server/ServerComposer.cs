// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using Core.Visibility;
using ServerSpecific.Debug; 
using ServerSpecific.Session; 
using Core.Logging;
// using Core.Model; // Not directly used here anymore for entity creation
// using Core.Primitives; // Not directly used here anymore
using Core.Time; 
using System;
// using System.Collections.Generic; // Not directly used here anymore
// using System.Linq; // Not directly used here anymore

using Logger = Core.Logging.Logger;
using System.Linq;
using Core.Session; // For IClientConnectionValidator, ClientIdentity etc.
// using Core.Network.Proxies; // Not directly used here anymore, except for SerializationUtils in one action
// using Core.Session; // Namespace Core.Session is used for IClientConnectionValidator, ClientIdentity etc.

public class ServerComposer : MonoBehaviour
{
    [Header("Core Components")]
    private Core.CoreComposer _coreComposer; 

    [Header("Network Layer (Shared)")] 
    [SerializeField]
    [Tooltip("Assign the MockNetworkLayer GameObject/Component from the scene here.")]
    private MockNetworkLayer mockNetworkLayer; 

    private IVisibilityStrategy _visibilityStrategy; 
    private IClock _serverClock; 
    private IClientConnectionValidator _connectionValidator; 

    [Header("Entity Debug Presentation")]
    [SerializeField] private DebugPresentationManager entityDebugPresentationManager;

    [Header("Client Debug Presentation")]
    [SerializeField] private ClientDebugPresentationManager clientDebugPresentationManager;
    
    [Header("Debug Info")]
    [SerializeField, ReadOnly] 
    private float currentTime_Display;

    private Core.Model.DebugEntity lastCreatedDebugEntity; // Keep for debug actions targeting an entity


    void Awake()
    {
        Logger.Log("[ServerComposer MB] Awake: Initializing Server...");

        #if UNITY_SERVER || UNITY_EDITOR 
        _serverClock = new UnityClock();
        Logger.Log("[ServerComposer MB] Using UnityClock for server time.");
        #else
        _serverClock = new SystemClock();
        Logger.Log("[ServerComposer MB] Using SystemClock for server time.");
        #endif


        if (mockNetworkLayer == null)
        {
            Logger.LogError("[ServerComposer MB] MockNetworkLayer not assigned! Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError("[ServerComposer MB] MockNetworkLayer not found. Aborting server setup.");
                enabled = false;
                return;
            }
            Logger.LogWarning("[ServerComposer MB] MockNetworkLayer found in scene. Assign in Inspector for robustness.");
        }

        _visibilityStrategy = new DummyVisibilityStrategy();
        _connectionValidator = new MockClientConnectionValidator(); 

        try {
            _coreComposer = new Core.CoreComposer(mockNetworkLayer, _visibilityStrategy, _serverClock, _connectionValidator);
            mockNetworkLayer.SetVisibilityManager(_coreComposer.VisibilityManager);
        }
        catch (Exception ex) {
            Logger.LogError($"[ServerComposer MB] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}");
            enabled = false;
            return;
        }

        if (entityDebugPresentationManager == null) entityDebugPresentationManager = GetComponent<DebugPresentationManager>();
        if (entityDebugPresentationManager != null && _coreComposer != null) {
            entityDebugPresentationManager.Initialize(_coreComposer.ServerLevel);
        } else if (entityDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer MB] Entity DebugPresentationManager not assigned/found.");
        }

        if (clientDebugPresentationManager == null) clientDebugPresentationManager = GetComponent<ClientDebugPresentationManager>();
        if (clientDebugPresentationManager != null && _coreComposer != null ) {
            clientDebugPresentationManager.Initialize(_coreComposer, entityDebugPresentationManager);
        } else if (clientDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer MB] ClientDebugPresentationManager not assigned/found.");
        }
        
        Logger.Log("[ServerComposer MB] Initialization complete. Server is ready to accept client connections.");
    }

    void Update() {
        if (_serverClock != null)
        {
            currentTime_Display = _serverClock.CurrentTime;
        }

        if (_coreComposer != null && enabled) {
            _coreComposer.Update(Time.deltaTime);
        }
    }
    void OnDestroy() {
        Logger.Log("[ServerComposer MB] OnDestroy: Cleaning up...");
        _coreComposer?.Dispose();
        _coreComposer = null;
        _serverClock = null; 
        Logger.Log("[ServerComposer MB] Cleanup complete.");
    }

    [ContextMenu("1. Create Debug Entity (At Origin)")]
    public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }

    [ContextMenu("1b. Create Debug Entity (Far Away)")]
    public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }

    private void CreateDebugEntityAt(Core.Primitives.Vector3 position) {
        if (_coreComposer?.ServerLevel != null) {
            Logger.Log($"[ServerComposer MB Action] Requesting DebugEntity creation at {position}...");
            lastCreatedDebugEntity = new Core.Model.DebugEntity(_coreComposer.ServerLevel); // Use full namespace
            lastCreatedDebugEntity.Position = position; 
        } else {
            Logger.LogWarning("[ServerComposer MB Action] CoreComposer or Level not initialized!");
        }
    }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum - needs Client running)")]
    public void SimulateClientSyncCorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer; 
        Logger.Log($"[ServerComposer MB Action] Simulating Client Sync (Correct) from NetworkSourceID: {mockNetworkLayer.defaultSendingClientId} for Entity: {lastCreatedDebugEntity.Id}");
        
        // Calculate checksum as the server would for DebugEntity
        int hash = HashCode.Combine(lastCreatedDebugEntity.Position.GetHashCode(), 
                                    lastCreatedDebugEntity.Rotation.GetHashCode(), 
                                    lastCreatedDebugEntity.Hydration.GetHashCode(), 
                                    lastCreatedDebugEntity.Guilt.GetHashCode());
        float checksum = (float)hash;

        clientNetwork.SendToServer(
            mockNetworkLayer.defaultSendingClientId, // sendingNetworkSourceId
            lastCreatedDebugEntity.Id,               // contextEntityId
            MessageType._ClientSyncState,            // messageType
            writer => writer.Write(checksum)         // serializePayloadAction
        );
    }

    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum - needs Client running)")]
    public void SimulateClientSyncIncorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        Logger.Log($"[ServerComposer MB Action] Simulating Client Sync (Incorrect) from NetworkSourceID: {mockNetworkLayer.defaultSendingClientId} for Entity: {lastCreatedDebugEntity.Id}");
        float incorrectChecksum = 9876.54f;

        clientNetwork.SendToServer(
            mockNetworkLayer.defaultSendingClientId, // sendingNetworkSourceId
            lastCreatedDebugEntity.Id,               // contextEntityId
            MessageType._ClientSyncState,            // messageType
            writer => writer.Write(incorrectChecksum) // serializePayloadAction
        );
    }

    [ContextMenu("4. Kill Last Debug Entity (Loud)")]
    public void KillLastDebugEntityLoud() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) {
            lastCreatedDebugEntity.Kill(false); 
        } else {
            Logger.LogWarning("[ServerComposer MB Action] No living DebugEntity tracked to kill.");
        }
    }
    [ContextMenu("4b. Kill Last Debug Entity (Silent)")]
    public void KillLastDebugEntitySilent() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) {
            lastCreatedDebugEntity.Kill(true); 
        } else {
            Logger.LogWarning("[ServerComposer MB Action] No living DebugEntity tracked to kill.");
        }
    }

    [ContextMenu("5. Test Escadre Command (_SetCourse - needs Client running & connected)")]
    public void TestEscadreSetCourse() {
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        if (_coreComposer == null || !_coreComposer.ClientConnections.Any()) { Logger.LogWarning("No clients connected to send command for."); return; }
        
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer; 
        Core.Primitives.Vector2 newDest = new Core.Primitives.Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
        Logger.Log($"[ServerComposer MB Action] Simulating C->S _SetCourse to {newDest} from NetworkSourceID: {mockNetworkLayer.defaultSendingClientId}");
        
        clientNetwork.SendToServer(
            mockNetworkLayer.defaultSendingClientId,                 // sendingNetworkSourceId
            0,                                                       // contextEntityId (0 for client's own escadre commands)
            MessageType._SetCourse,                                  // messageType
            writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, newDest) // serializePayloadAction
        );
    }

    [ContextMenu("DEBUG: Force Unregister First Connected Client (if any)")]
    public void DebugUnregisterFirstClient() {
        if (_coreComposer == null || !_coreComposer.ClientConnections.Any()) { Logger.LogWarning("CoreComposer not initialized or no clients to unregister."); return; }
        int clientIdToUnregister = _coreComposer.ClientConnections.Keys.First();
        Logger.Log($"[ServerComposer MB Action] Forcibly unregistering client with game ID {clientIdToUnregister}.");
        _coreComposer.UnregisterClient(clientIdToUnregister);
    }
}