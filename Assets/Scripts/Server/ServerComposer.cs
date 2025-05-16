// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using Core.Visibility;
using ServerSpecific.Network;
using ServerSpecific.Debug;
using Core.Logging;
using Core.Model;
using Core.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

using Logger = Core.Logging.Logger;
using Core.Network.Proxies;
using Core.Session;

public class ServerComposer : MonoBehaviour
{
    [Header("Core Components")]
    private CoreComposer _coreComposer;
    // ... (other fields remain same) ...

    [Header("Entity Debug Presentation")]
    [SerializeField] private DebugPresentationManager entityDebugPresentationManager;

    [Header("Client Debug Presentation")]
    [SerializeField] private ClientDebugPresentationManager clientDebugPresentationManager;

    // ... (MockClientView and _mockClientViews remain same) ...
    private class MockClientView : IClientView {
        public int ClientId { get; set; }
        public Core.Primitives.Vector3 Position { get; set; }
        public float RadiusOfInterest { get; set; } = 50f;
        public Action<int, MessageType, System.IO.BinaryReader> S2CHandler { get; set; }
    }
    private List<MockClientView> _mockClientViews = new List<MockClientView>();
    public IClientNetworkLayer ClientNetworkAccess => _mockNetworkLayer;
    private MockNetworkLayer _mockNetworkLayer; // Ensure this is initialized
    private IVisibilityStrategy _visibilityStrategy; // Ensure this is initialized
    private DebugEntity lastCreatedDebugEntity; // Ensure this is initialized if used


    void Awake()
    {
        Logger.Log("[ServerComposer] Awake: Initializing Server...");

        _visibilityStrategy = new DummyVisibilityStrategy(); // Initialize
        _mockNetworkLayer = new MockNetworkLayer();         // Initialize

        try {
            _coreComposer = new CoreComposer(_mockNetworkLayer, _visibilityStrategy);
            _mockNetworkLayer.SetVisibilityManager(_coreComposer.VisibilityManager);
        }
        catch (Exception ex) { Logger.LogError($"[ServerComposer] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}"); enabled = false; return; }

        if (entityDebugPresentationManager == null) entityDebugPresentationManager = GetComponent<DebugPresentationManager>();
        if (entityDebugPresentationManager != null && _coreComposer != null) {
            entityDebugPresentationManager.Initialize(_coreComposer.ServerLevel);
            Logger.Log("[ServerComposer] Entity DebugPresentationManager initialized.");
        } else if (entityDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer] Entity DebugPresentationManager not assigned/found. Entity debug visuals disabled.");
        }

        if (clientDebugPresentationManager == null) clientDebugPresentationManager = GetComponent<ClientDebugPresentationManager>();
        if (clientDebugPresentationManager != null && _coreComposer != null && entityDebugPresentationManager != null) {
            // Pass CoreComposer to ClientDebugPresentationManager
            clientDebugPresentationManager.Initialize(_coreComposer, entityDebugPresentationManager);
            Logger.Log("[ServerComposer] ClientDebugPresentationManager initialized.");
        } else if (clientDebugPresentationManager == null) {
            Logger.LogWarning("[ServerComposer] ClientDebugPresentationManager not assigned/found. ClientConnection debug visuals disabled.");
        } else if (entityDebugPresentationManager == null && clientDebugPresentationManager != null) {
             Logger.LogWarning("[ServerComposer] ClientDebugPresentationManager requires EntityDebugPresentationManager. PVS lines might not work.");
             // Consider if it should still init, or throw error, or handle entityDebugManager being null in ClientConnectionDebugBehaviour
             clientDebugPresentationManager.Initialize(_coreComposer, null); // Pass null if entity manager is missing
        }

        var client1View = new MockClientView { ClientId = 1, Position = Core.Primitives.Vector3.Zero, RadiusOfInterest = 150f };
        client1View.S2CHandler = (entityId, messageType, reader) => { /* ... */ };
        _mockClientViews.Add(client1View);
        _coreComposer.RegisterClient(client1View.ClientId, client1View.RadiusOfInterest, client1View.Position);
        _mockNetworkLayer.RegisterClientS2CHandler(client1View.ClientId, client1View.S2CHandler);

        Logger.Log("[ServerComposer] Mock client setup complete.");
        Logger.Log("[ServerComposer] Initialization complete in Awake.");
    }

    // Update, OnDestroy, and other ContextMenu methods remain the same
    void Update() { if (_coreComposer != null && enabled) { _coreComposer.Update(Time.deltaTime); } }
    void OnDestroy() {
        Logger.Log("[ServerComposer] OnDestroy: Cleaning up...");
        if (_mockNetworkLayer != null) { foreach (var view in _mockClientViews) { _mockNetworkLayer.UnregisterClientS2CHandler(view.ClientId); } }
        _coreComposer?.Dispose(); _coreComposer = null; _mockClientViews.Clear();
        Logger.Log("[ServerComposer] Cleanup complete.");
    }
    [ContextMenu("1. Create Debug Entity (At Origin)")] public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }
    [ContextMenu("1b. Create Debug Entity (Far Away)")] public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }
    private void CreateDebugEntityAt(Core.Primitives.Vector3 position) { if (_coreComposer?.ServerLevel != null) { Logger.Log($"[ServerComposer Action] Requesting DebugEntity creation at {position}..."); lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel); lastCreatedDebugEntity.Position = position; } else { Logger.LogWarning("[ServerComposer Action] Cannot create entity, CoreComposer or Level not initialized!"); } }
    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")]
    public void SimulateClientSyncCorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        int hash = HashCode.Combine(lastCreatedDebugEntity.Position.GetHashCode(), lastCreatedDebugEntity.Rotation.GetHashCode(), ((DebugEntity)lastCreatedDebugEntity).Hydration.GetHashCode(), ((DebugEntity)lastCreatedDebugEntity).Guilt.GetHashCode());
        float checksum = (float)hash;
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType._ClientSyncState, writer => writer.Write(checksum));
    }
    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum)")]
    public void SimulateClientSyncIncorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        float incorrectChecksum = 9876.54f;
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType._ClientSyncState, writer => writer.Write(incorrectChecksum));
    }
    [ContextMenu("4. Kill Last Debug Entity")]
    public void KillLastDebugEntity() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) { lastCreatedDebugEntity.Kill(); }
        else { Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill."); }
    }
    [ContextMenu("5. Test Escadre Command (_SetCourse for Client 1)")]
    public void TestEscadreSetCourse() {
        if (_coreComposer == null) { Logger.LogWarning("CoreComposer not initialized."); return; }
        _coreComposer.ClientConnections.TryGetValue(1, out ClientConnection clientConn);
        if (clientConn != null && clientConn.EscadreInstance != null) {
            Core.Primitives.Vector2 newDest = new Core.Primitives.Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
            // Note: This directly sends a network message, simulating client input.
            // The ClientConnectionDebugBehaviour would call clientConn.RequestSetCourse directly.
            _mockNetworkLayer.SendToServer(0, MessageType._SetCourse, writer => SerializationUtils.WriteVector2(writer, newDest));
        } else { Logger.LogWarning("[ServerComposer Action] Client 1 or its escadre not found."); }
    }
    [ContextMenu("6. Register New Mock Client")]
    public void RegisterNewMockClient() {
        if (_coreComposer == null) { Logger.LogWarning("CoreComposer not initialized."); return; }
        int newClientId = _coreComposer.ClientConnections.Keys.Any() ? _coreComposer.ClientConnections.Keys.Max() + 1 : 1;
        while(_coreComposer.ClientConnections.ContainsKey(newClientId)) { newClientId++; }

        var clientNewView = new MockClientView { ClientId = newClientId, Position = new Core.Primitives.Vector3(UnityEngine.Random.Range(-20f,20f),0,UnityEngine.Random.Range(-20f,20f)), RadiusOfInterest = 120f };
        clientNewView.S2CHandler = (entityId, messageType, reader) => { /* ... */ };
        _mockClientViews.Add(clientNewView);
        _coreComposer.RegisterClient(clientNewView.ClientId, clientNewView.RadiusOfInterest, clientNewView.Position);
        if (_mockNetworkLayer != null) _mockNetworkLayer.RegisterClientS2CHandler(clientNewView.ClientId, clientNewView.S2CHandler);
        Logger.Log($"[ServerComposer Action] Registered new mock client with ID {newClientId}.");
    }
    [ContextMenu("7. Unregister First Mock Client")]
    public void UnregisterFirstMockClient() {
        if (_coreComposer == null || !_coreComposer.ClientConnections.Any()) { Logger.LogWarning("CoreComposer not initialized or no clients to unregister."); return; }
        int clientIdToUnregister = _coreComposer.ClientConnections.Keys.First();
        _coreComposer.UnregisterClient(clientIdToUnregister);
        var mockViewToRemove = _mockClientViews.FirstOrDefault(mcv => mcv.ClientId == clientIdToUnregister);
        if(mockViewToRemove != null) _mockClientViews.Remove(mockViewToRemove);
        if (_mockNetworkLayer != null) _mockNetworkLayer.UnregisterClientS2CHandler(clientIdToUnregister);
        Logger.Log($"[ServerComposer Action] Unregistered mock client with ID {clientIdToUnregister}.");
    }
}