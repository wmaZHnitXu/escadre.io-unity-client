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
using System.Collections.Generic; // For List
// using System.IO; // Not directly needed here

using Logger = Core.Logging.Logger;
using Core.Network.Proxies;
using Core.Session;

public class ServerComposer : MonoBehaviour
{
    [Header("Core Components")]
    private CoreComposer _coreComposer;
    private MockNetworkLayer _mockNetworkLayer;
    private IVisibilityStrategy _visibilityStrategy;

    [Header("Debug Presentation")]
    [Tooltip("Assign the DebugPresentationManager component here.")]
    [SerializeField] private DebugPresentationManager debugPresentationManager;

    private DebugEntity lastCreatedDebugEntity;
    private class MockClientView : IClientView
    {
        public int ClientId { get; set; }
        public Core.Primitives.Vector3 Position { get; set; }
        public float RadiusOfInterest { get; set; } = 50f;
        public Action<int, MessageType, System.IO.BinaryReader> S2CHandler { get; set; }
    }
    private List<MockClientView> _mockClientViews = new List<MockClientView>();

    // Expose interfaces for testing / potential client connection
    public IClientNetworkLayer ClientNetworkAccess => _mockNetworkLayer;
    // ServerNetworkAccess might not be needed externally if interaction is through core commands
    // public IServerNetworkLayer ServerNetworkAccess => _mockNetworkLayer;


    void Awake()
    {
        Logger.Log("[ServerComposer] Awake: Initializing Server...");

        // 1. Create Visibility Strategy
        _visibilityStrategy = new DummyVisibilityStrategy();
        Logger.Log("[ServerComposer] DummyVisibilityStrategy created.");

        // 2. Create Network Layer
        _mockNetworkLayer = new MockNetworkLayer();
        Logger.Log("[ServerComposer] MockNetworkLayer created.");

        // 3. Create Core Composer, injecting network layer and visibility strategy
        try
        {
            _coreComposer = new CoreComposer(_mockNetworkLayer, _visibilityStrategy);
            Logger.Log("[ServerComposer] CoreComposer created successfully.");

            // 4. OPTIONAL: Provide VisibilityManager to MockNetworkLayer if it needs to query visibility
            // This is a bit of a setup detail for the mock. A real network layer
            // wouldn't typically need the VisibilityManager injected this way;
            // it would get client lists from a higher-level system that uses VisibilityManager.
            _mockNetworkLayer.SetVisibilityManager(_coreComposer.VisibilityManager);

        }
        catch (Exception ex)
        {
            Logger.LogError($"[ServerComposer] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}");
            enabled = false;
            return;
        }

        // Initialize Debug Presentation Manager
        if (debugPresentationManager == null) { debugPresentationManager = GetComponent<DebugPresentationManager>(); }
        if (debugPresentationManager != null && _coreComposer != null)
        {
            debugPresentationManager.Initialize(_coreComposer.ServerLevel);
            Logger.Log("[ServerComposer] DebugPresentationManager initialized.");
        }
        else if(debugPresentationManager == null)
        {
            Logger.LogWarning("[ServerComposer] DebugPresentationManager not assigned or found. Debug visuals disabled.");
        }


        // Setup Mock Client(s)
        var client1View = new MockClientView { ClientId = 1, Position = Core.Primitives.Vector3.Zero };
        // Client-side handler (what the "client application" would do with messages)
        client1View.S2CHandler = (entityId, messageType, reader) =>
        {
            long payloadLength = reader.BaseStream.Length - reader.BaseStream.Position;
            // This log simulates what the *client's* ClientEntityManager would receive via IClientNetworkLayer.OnMessageReceived
            Logger.Log($"[MockClient S2C Handler CId={client1View.ClientId}] Received from Server: EntityID={entityId}, Type={messageType}, PayloadLen={payloadLength}");
            // Here, a real client would deserialize and update its local state/proxies.
        };
        _mockClientViews.Add(client1View);
        _coreComposer.RegisterClient(client1View.ClientId, client1View.RadiusOfInterest, client1View.Position); // CoreComposer now handles VM registration
        _mockNetworkLayer.RegisterClientS2CHandler(client1View.ClientId, client1View.S2CHandler); // Mock specific handler for server->client
        // Also subscribe the generic IClientNetworkLayer.OnMessageReceived if the client app is using it
        // This might be confusing as MockNetworkLayer implements both IServer and IClient interfaces.
        // Let's assume the above RegisterClientS2CHandler is how this mock specifically routes S->C to a mock client.
        // If a client app was using `ClientNetworkAccess.OnMessageReceived`, it would subscribe to that.

        Logger.Log("[ServerComposer] Mock client setup complete.");
        Logger.Log("[ServerComposer] Initialization complete in Awake.");
    }

    void Update()
    {
        if (_coreComposer != null && enabled)
        {
            // Simulate mock client view movement
            foreach (var view in _mockClientViews)
            {
                if (view.ClientId == 1 && _coreComposer.ClientConnections.TryGetValue(1, out var clientConn) && clientConn.CurrentState == ClientState.InSea)
                {
                    // If PVS is Escadre-centered, the ClientConnection.Position (IClientView.Position)
                    // will update automatically based on its Escadre's ships.
                    // So, we don't need to manually set clientConnection.Position here.
                    // We just need to ensure CoreComposer.Update calls VisibilityManager.AddOrUpdateClientView
                    // for active clients, which it does.
                }
            }
            _coreComposer.Update(Time.deltaTime);
        }
    }

    void OnDestroy()
    {
        Logger.Log("[ServerComposer] OnDestroy: Cleaning up...");
        if (_mockNetworkLayer != null) {
            foreach (var view in _mockClientViews) {
                _mockNetworkLayer.UnregisterClientS2CHandler(view.ClientId);
            }
        }
        // CoreComposer.UnregisterClient handles removing from VisibilityManager
        _coreComposer?.Dispose(); // This handles disposing VM, Level, RepMan, Strategy
        _coreComposer = null;

        _mockNetworkLayer = null; // Mock Layer is managed here
        _visibilityStrategy = null; // Strategy is managed here, but passed to CoreComposer who disposes it.
                                     // No, CoreComposer disposes the strategy it was given.
        _mockClientViews.Clear();
        Logger.Log("[ServerComposer] Cleanup complete.");
    }

    // --- ContextMenu Methods (remain the same logic) ---
    [ContextMenu("1. Create Debug Entity (At Origin)")] public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }
    [ContextMenu("1b. Create Debug Entity (Far Away)")] public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }
    private void CreateDebugEntityAt(Core.Primitives.Vector3 position) { if (_coreComposer?.ServerLevel != null) { Logger.Log($"[ServerComposer Action] Requesting DebugEntity creation at {position}..."); lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel); lastCreatedDebugEntity.Position = position; Logger.Log($"[ServerComposer Action] DebugEntity (ID pending: {lastCreatedDebugEntity.Id}) added to Level's add queue. Position: {lastCreatedDebugEntity.Position}"); } else { Logger.LogWarning("[ServerComposer Action] Cannot create entity, CoreComposer or Level not initialized!"); } }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")]
    public void SimulateClientSyncCorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        int hash = HashCode.Combine(lastCreatedDebugEntity.Position.GetHashCode(), lastCreatedDebugEntity.Rotation.GetHashCode(), ((DebugEntity)lastCreatedDebugEntity).Hydration.GetHashCode(), ((DebugEntity)lastCreatedDebugEntity).Guilt.GetHashCode());
        float checksum = (float)hash;
        Logger.Log($"[ServerComposer Action] Simulating _ClientSyncState for Entity {lastCreatedDebugEntity.Id} with CORRECT checksum: {checksum}");
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType._ClientSyncState, writer => writer.Write(checksum));
    }

    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum)")]
    public void SimulateClientSyncIncorrect() {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        float incorrectChecksum = 9876.54f;
        Logger.Log($"[ServerComposer Action] Simulating _ClientSyncState for Entity {lastCreatedDebugEntity.Id} with INCORRECT checksum: {incorrectChecksum}");
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType._ClientSyncState, writer => writer.Write(incorrectChecksum));
    }

    [ContextMenu("4. Kill Last Debug Entity")]
    public void KillLastDebugEntity() {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) { Logger.Log($"[ServerComposer Action] Killing entity {lastCreatedDebugEntity.Id}"); lastCreatedDebugEntity.Kill(); }
        else { Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill."); }
    }

    [ContextMenu("5. Test Escadre Command (_SetCourse for Client 1)")]
    public void TestEscadreSetCourse() {
        ClientConnection clientConn = null;
        if (_coreComposer?.ClientConnections.TryGetValue(1, out clientConn) == true && clientConn.EscadreInstance != null) {
            Core.Primitives.Vector2 newDest = new Core.Primitives.Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
            Logger.Log($"[ServerComposer Action] Simulating Client 1 _SetCourse to {newDest}");
            _mockNetworkLayer.SendToServer(0, MessageType._SetCourse, writer => SerializationUtils.WriteVector2(writer, newDest)); // entityId can be 0 for escadre commands
        } else { Logger.LogWarning("[ServerComposer Action] Client 1 or its escadre not found."); }
    }
}