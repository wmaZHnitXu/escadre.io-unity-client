// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using Core.Visibility;
// Assuming MockNetworkLayer is no longer in ServerSpecific if it's a MonoBehaviour meant for broader use
// using ServerSpecific.Network;
using ServerSpecific.Debug; // For DebugPresentationManagers
using Core.Logging;
using Core.Model;
using Core.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

using Logger = Core.Logging.Logger;
using Core.Network.Proxies; // For SerializationUtils
using Core.Session;

public class ServerComposer : MonoBehaviour
{
    [Header("Core Components")]
    private CoreComposer _coreComposer;

    [Header("Network Layer (Shared)")] // Emphasize it's shared
    [SerializeField]
    [Tooltip("Assign the MockNetworkLayer GameObject/Component from the scene here.")]
    private MockNetworkLayer mockNetworkLayer; // Corrected field name

    private IVisibilityStrategy _visibilityStrategy; // Keep this non-serialized, created in Awake

    [Header("Entity Debug Presentation")]
    [SerializeField] private DebugPresentationManager entityDebugPresentationManager;

    [Header("Client Debug Presentation")]
    [SerializeField] private ClientDebugPresentationManager clientDebugPresentationManager;


    private DebugEntity lastCreatedDebugEntity;
    private class MockClientView : IClientView {
        public int ClientId { get; set; }
        public Core.Primitives.Vector3 Position { get; set; }
        public float RadiusOfInterest { get; set; } = 50f;
        // S2CHandler is no longer needed here if ClientComposer directly subscribes to MockNetworkLayer
    }
    private List<MockClientView> _mockClientViews = new List<MockClientView>(); // Still useful for C->S simulation logic if needed

    // No longer exposing ClientNetworkAccess here, ClientComposer will get it from the same shared MockNetworkLayer
    // public IClientNetworkLayer ClientNetworkAccess => mockNetworkLayer;


    void Awake()
    {
        Logger.Log("[ServerComposer] Awake: Initializing Server...");

        if (mockNetworkLayer == null)
        {
            Logger.LogError("[ServerComposer] MockNetworkLayer not assigned in Inspector! Server cannot function correctly with client.");
            // Attempt to find it if not assigned, for convenience during setup
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError("[ServerComposer] Could not find MockNetworkLayer in scene. Aborting server setup.");
                enabled = false;
                return;
            }
            else
            {
                Logger.LogWarning("[ServerComposer] MockNetworkLayer was found in scene. Please assign it in the Inspector for robustness.");
            }
        }

        _visibilityStrategy = new DummyVisibilityStrategy();

        try {
            // Pass the IServerNetworkLayer interface of the mockNetworkLayer
            _coreComposer = new CoreComposer(mockNetworkLayer, _visibilityStrategy);
            // SetVisibilityManager is still relevant if the mock layer uses it for BroadcastRelevant
            mockNetworkLayer.SetVisibilityManager(_coreComposer.VisibilityManager);
        }
        catch (Exception ex) {
            Logger.LogError($"[ServerComposer] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}");
            enabled = false;
            return;
        }

        // Initialize Entity Debug Presentation Manager
        if (entityDebugPresentationManager == null) entityDebugPresentationManager = GetComponent<DebugPresentationManager>();
        if (entityDebugPresentationManager != null && _coreComposer != null) {
            entityDebugPresentationManager.Initialize(_coreComposer.ServerLevel);
            Logger.Log("[ServerComposer] Entity DebugPresentationManager initialized.");
        } else if (entityDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer] Entity DebugPresentationManager not assigned/found. Entity debug visuals disabled.");
        }

        // Initialize Client Debug Presentation Manager
        if (clientDebugPresentationManager == null) clientDebugPresentationManager = GetComponent<ClientDebugPresentationManager>();
        if (clientDebugPresentationManager != null && _coreComposer != null && entityDebugPresentationManager != null) {
            clientDebugPresentationManager.Initialize(_coreComposer, entityDebugPresentationManager);
            Logger.Log("[ServerComposer] ClientDebugPresentationManager initialized.");
        } else if (clientDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer] ClientDebugPresentationManager not assigned/found. ClientConnection debug visuals disabled.");
        } else if (entityDebugPresentationManager == null && clientDebugPresentationManager != null) {
             Logger.LogWarning("[ServerComposer] ClientDebugPresentationManager needs EntityDebugPresentationManager for full PVS visualization.");
             clientDebugPresentationManager.Initialize(_coreComposer, null); // Can still init, but PVS lines will be limited
        }


        // Setup Mock Client(s) for C->S simulation
        // The ClientComposer instance in the scene will handle S->C messages for "itself"
        // This MockClientView is now more about tracking data for C->S simulation logic
        // than for receiving S->C messages, as ClientComposer does that.
        var client1Data = new MockClientView { ClientId = 1, Position = Core.Primitives.Vector3.Zero, RadiusOfInterest = 150f };
        _mockClientViews.Add(client1Data); // Store data for simulating C->S from client 1

        // CoreComposer registers the client connection, which will lead to server-side state.
        // The actual S->C message handling for this client ID 1 will be done by ClientComposer
        // if it's configured to represent client 1.
        _coreComposer.RegisterClient(client1Data.ClientId, client1Data.RadiusOfInterest, client1Data.Position);

        // Server's ServerReplicationManager subscribes to mockNetworkLayer.C2S_OnMessageReceived.
        // Client's ClientEntityManager (via ClientComposer) subscribes to mockNetworkLayer.S2C_OnMessageReceived.

        // No need for _mockNetworkLayer.RegisterClientS2CHandler here anymore if ClientComposer
        // directly subscribes to the S2C_OnMessageReceived event.
        // The mockNetworkLayer.RegisterMockClient() can be used if the MockNetworkLayer
        // itself needs to know about distinct client IDs for more complex broadcast filtering
        // (if not relying solely on VisibilityManager).
        if (mockNetworkLayer != null) // Ensure it's not null before calling
        {
            mockNetworkLayer.RegisterMockClient(client1Data.ClientId); // Let mock layer know this client conceptually "exists"
        }


        Logger.Log("[ServerComposer] Mock client setup complete on server side.");
        Logger.Log("[ServerComposer] Initialization complete in Awake.");
    }

    void Update() {
        if (_coreComposer != null && enabled) {
            _coreComposer.Update(Time.deltaTime);
        }
    }
    void OnDestroy() {
        Logger.Log("[ServerComposer] OnDestroy: Cleaning up...");
        // Unregister mock clients from the mock network layer itself if they were registered
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
        Logger.Log("[ServerComposer] Cleanup complete.");
    }

    // ContextMenu Methods - using mockNetworkLayer for C->S simulation
    [ContextMenu("1. Create Debug Entity (At Origin)")]
    public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }

    [ContextMenu("1b. Create Debug Entity (Far Away)")]
    public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }

    private void CreateDebugEntityAt(Core.Primitives.Vector3 position) {
        if (_coreComposer?.ServerLevel != null) {
            Logger.Log($"[ServerComposer Action] Requesting DebugEntity creation at {position}...");
            lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel);
            lastCreatedDebugEntity.Position = position;
        } else {
            Logger.LogWarning("[ServerComposer Action] Cannot create entity, CoreComposer or Level not initialized!");
        }
    }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")]
    public void SimulateClientSyncCorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        // Simulate client 1 sending this
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
            Logger.Log($"[ServerComposer Action] Killing entity {lastCreatedDebugEntity.Id} (Loudly)");
            lastCreatedDebugEntity.Kill(false); // Loud kill
        } else {
            Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill.");
        }
    }
    [ContextMenu("4b. Kill Last Debug Entity (Silent)")]
    public void KillLastDebugEntitySilent() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) {
            Logger.Log($"[ServerComposer Action] Killing entity {lastCreatedDebugEntity.Id} (Silently)");
            lastCreatedDebugEntity.Kill(true); // Silent kill
        } else {
            Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill.");
        }
    }


    [ContextMenu("5. Test Escadre Command (_SetCourse for Client 1)")]
    public void TestEscadreSetCourse() {
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        // Simulate client 1 sending this command
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        Core.Primitives.Vector2 newDest = new Core.Primitives.Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
        Logger.Log($"[ServerComposer Action] Simulating Client 1 sending _SetCourse to {newDest}");
        clientNetwork.SendToServer(0, MessageType._SetCourse, writer => SerializationUtils.WriteVector2(writer, newDest));
    }

    [ContextMenu("6. Register New Mock Client (Server-Side State)")]
    public void RegisterNewMockClient() {
        if (_coreComposer == null) { Logger.LogWarning("CoreComposer not initialized."); return; }
        int newClientId = _coreComposer.ClientConnections.Keys.Any() ? _coreComposer.ClientConnections.Keys.Max() + 1 : 1;
        while(_coreComposer.ClientConnections.ContainsKey(newClientId)) { newClientId++; }

        var clientNewData = new MockClientView { ClientId = newClientId, Position = new Core.Primitives.Vector3(UnityEngine.Random.Range(-20f,20f),0,UnityEngine.Random.Range(-20f,20f)), RadiusOfInterest = 120f };
        _mockClientViews.Add(clientNewData);
        _coreComposer.RegisterClient(clientNewData.ClientId, clientNewData.RadiusOfInterest, clientNewData.Position);
        if (mockNetworkLayer != null) mockNetworkLayer.RegisterMockClient(clientNewData.ClientId); // Let mock layer know
        Logger.Log($"[ServerComposer Action] Registered new mock client (server state) with ID {newClientId}.");
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
        Logger.Log($"[ServerComposer Action] Unregistered mock client (server state) with ID {clientIdToUnregister}.");
    }
}