// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using Core.Visibility;
using ServerSpecific.Debug; 
using Core.Logging;
using Core.Model;
using Core.Primitives;
using Core.Time; 
using System;
using System.Collections.Generic;
using System.Linq;

using Logger = Core.Logging.Logger;
using Core.Network.Proxies; 
using Core.Session;
// No specific using needed for ReadOnlyAttribute if it's in global scope or same assembly

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

    [Header("Entity Debug Presentation")]
    [SerializeField] private DebugPresentationManager entityDebugPresentationManager;

    [Header("Client Debug Presentation")]
    [SerializeField] private ClientDebugPresentationManager clientDebugPresentationManager;
    
    [Header("Debug Info")]
    [SerializeField, ReadOnly] 
    private float currentTime_Display;


    private DebugEntity lastCreatedDebugEntity;
    private class MockClientView : IClientView {
        public int ClientId { get; set; }
        public Core.Primitives.Vector3 Position { get; set; }
        public float RadiusOfInterest { get; set; } = 50f;
    }
    private List<MockClientView> _mockClientViews = new List<MockClientView>(); 


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
            Logger.LogError("[ServerComposer MB] MockNetworkLayer not assigned in Inspector! Server cannot function correctly with client.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError("[ServerComposer MB] Could not find MockNetworkLayer in scene. Aborting server setup.");
                enabled = false;
                return;
            }
            else
            {
                Logger.LogWarning("[ServerComposer MB] MockNetworkLayer was found in scene. Please assign it in the Inspector for robustness.");
            }
        }

        _visibilityStrategy = new DummyVisibilityStrategy();

        try {
            _coreComposer = new Core.CoreComposer(mockNetworkLayer, _visibilityStrategy, _serverClock);
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
            Logger.Log("[ServerComposer MB] Entity DebugPresentationManager initialized.");
        } else if (entityDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer MB] Entity DebugPresentationManager not assigned/found. Entity debug visuals disabled.");
        }

        if (clientDebugPresentationManager == null) clientDebugPresentationManager = GetComponent<ClientDebugPresentationManager>();
        if (clientDebugPresentationManager != null && _coreComposer != null && entityDebugPresentationManager != null) {
            clientDebugPresentationManager.Initialize(_coreComposer, entityDebugPresentationManager);
            Logger.Log("[ServerComposer MB] ClientDebugPresentationManager initialized.");
        } else if (clientDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer MB] ClientDebugPresentationManager not assigned/found. ClientConnection debug visuals disabled.");
        } else if (entityDebugPresentationManager == null && clientDebugPresentationManager != null) {
             Logger.LogWarning("[ServerComposer MB] ClientDebugPresentationManager needs EntityDebugPresentationManager for full PVS visualization.");
             clientDebugPresentationManager.Initialize(_coreComposer, null); 
        }

        var client1Data = new MockClientView { ClientId = 1, Position = Core.Primitives.Vector3.Zero, RadiusOfInterest = 150f };
        _mockClientViews.Add(client1Data); 

        _coreComposer.RegisterClient(client1Data.ClientId, client1Data.RadiusOfInterest, new Core.Primitives.Vector3(10,0,10));

        if (mockNetworkLayer != null) 
        {
            mockNetworkLayer.RegisterMockClient(client1Data.ClientId); 
        }


        Logger.Log("[ServerComposer MB] Mock client setup complete on server side.");
        Logger.Log("[ServerComposer MB] Initialization complete in Awake.");
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
        if (mockNetworkLayer != null)
        {
            foreach (var viewData in _mockClientViews)
            {
                mockNetworkLayer.UnregisterMockClient(viewData.ClientId);
            }
        }
        _coreComposer?.Dispose();
        _coreComposer = null;
        _mockClientViews.Clear();
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
            lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel);
            lastCreatedDebugEntity.Position = position; 
        } else {
            Logger.LogWarning("[ServerComposer MB Action] Cannot create entity, CoreComposer or Level not initialized!");
        }
    }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")]
    public void SimulateClientSyncCorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        int hash = HashCode.Combine(lastCreatedDebugEntity.Position.GetHashCode(), lastCreatedDebugEntity.Rotation.GetHashCode(), ((DebugEntity)lastCreatedDebugEntity).Hydration.GetHashCode(), ((DebugEntity)lastCreatedDebugEntity).Guilt.GetHashCode());
        float checksum = (float)hash;
        clientNetwork.SendToServer(lastCreatedDebugEntity.Id, MessageType._ClientSyncState, writer => writer.Write(checksum));
    }

    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum)")]
    public void SimulateClientSyncIncorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        float incorrectChecksum = 9876.54f;
        clientNetwork.SendToServer(lastCreatedDebugEntity.Id, MessageType._ClientSyncState, writer => writer.Write(incorrectChecksum));
    }

    [ContextMenu("4. Kill Last Debug Entity (Loud)")]
    public void KillLastDebugEntityLoud() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) {
            Logger.Log($"[ServerComposer MB Action] Killing entity {lastCreatedDebugEntity.Id} (Loudly)");
            lastCreatedDebugEntity.Kill(false); 
        } else {
            Logger.LogWarning("[ServerComposer MB Action] No living DebugEntity tracked to kill.");
        }
    }
    [ContextMenu("4b. Kill Last Debug Entity (Silent)")]
    public void KillLastDebugEntitySilent() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) {
            Logger.Log($"[ServerComposer MB Action] Killing entity {lastCreatedDebugEntity.Id} (Silently)");
            lastCreatedDebugEntity.Kill(true); 
        } else {
            Logger.LogWarning("[ServerComposer MB Action] No living DebugEntity tracked to kill.");
        }
    }


    [ContextMenu("5. Test Escadre Command (_SetCourse for Client 1)")]
    public void TestEscadreSetCourse() {
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        Core.Primitives.Vector2 newDest = new Core.Primitives.Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
        Logger.Log($"[ServerComposer MB Action] Simulating Client 1 sending _SetCourse to {newDest}");
        clientNetwork.SendToServer(0, MessageType._SetCourse, writer => SerializationUtils.WriteVector2(writer, newDest));
    }

    [ContextMenu("6. Register New Mock Client (Server-Side State)")]
    public void RegisterNewMockClient() {
        if (_coreComposer == null) { Logger.LogWarning("CoreComposer not initialized."); return; }
        int newClientId = _coreComposer.ClientConnections.Keys.Any() ? _coreComposer.ClientConnections.Keys.Max() + 1 : 1;
        while(_coreComposer.ClientConnections.ContainsKey(newClientId)) { newClientId++; }

        Core.Primitives.Vector3 spawnPos = new Core.Primitives.Vector3(UnityEngine.Random.Range(-20f,20f),0,UnityEngine.Random.Range(-20f,20f));
        var clientNewData = new MockClientView { ClientId = newClientId, Position = spawnPos, RadiusOfInterest = 120f };
        _mockClientViews.Add(clientNewData);
        _coreComposer.RegisterClient(clientNewData.ClientId, clientNewData.RadiusOfInterest, clientNewData.Position);
        if (mockNetworkLayer != null) mockNetworkLayer.RegisterMockClient(clientNewData.ClientId); 
        Logger.Log($"[ServerComposer MB Action] Registered new mock client (server state) with ID {newClientId}.");
        Logger.LogWarning("NOTE: This only creates server-side state. A ClientComposer instance needs to be running to 'be' this client.");
    }

    [ContextMenu("7. Unregister First Mock Client (Server-Side State)")]
    public void UnregisterFirstMockClient() {
        if (_coreComposer == null || !_coreComposer.ClientConnections.Any()) { Logger.LogWarning("CoreComposer not initialized or no clients to unregister."); return; }
        int clientIdToUnregister = _coreComposer.ClientConnections.Keys.First();
        _coreComposer.UnregisterClient(clientIdToUnregister);

        var mockViewToRemove = _mockClientViews.FirstOrDefault(mcv => mcv.ClientId == clientIdToUnregister);
        if(mockViewToRemove != null) _mockClientViews.Remove(mockViewToRemove);
        if (mockNetworkLayer != null) mockNetworkLayer.UnregisterMockClient(clientIdToUnregister);
        Logger.Log($"[ServerComposer MB Action] Unregistered mock client (server state) with ID {clientIdToUnregister}.");
    }
}