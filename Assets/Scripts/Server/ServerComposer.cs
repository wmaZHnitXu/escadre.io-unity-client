// File: Scripts/Server/ServerComposer.cs
using UnityEngine; // Still needed for MonoBehaviour and Time.deltaTime
using Core;
using Core.Network;
using Core.Visibility;
using ServerSpecific.Network;
using Core.Logging;
using Core.Model;
using Core.Primitives; // For Core.Primitives.Vector3
using System;
using System.Collections.Generic;
using System.IO; // For BinaryReader in mock client handler

// Explicitly use Logger from Core.Logging to avoid conflict if Unity has its own Logger
using Logger = Core.Logging.Logger;

public class ServerComposer : MonoBehaviour
{
    private CoreComposer _coreComposer;
    private MockNetworkLayer _mockNetworkLayer;
    private IVisibilityStrategy _visibilityStrategy;
    private VisibilityManager _visibilityManager; // Field declaration for the shared instance
    private DebugEntity lastCreatedDebugEntity;

    private class MockClientView : IClientView
    {
        public int ClientId { get; set; }
        public Core.Primitives.Vector3 Position { get; set; } // Explicitly Core.Primitives.Vector3
        public float RadiusOfInterest { get; set; } = 50f;
        // Action to handle S->C messages for this mock client
        public Action<int, MessageType, BinaryReader> S2CHandler { get; set; }
    }
    private List<MockClientView> _mockClientViews = new List<MockClientView>();

    public IClientNetworkLayer ClientNetworkAccess => _mockNetworkLayer;
    public IServerNetworkLayer ServerNetworkAccess => _mockNetworkLayer;

    void Awake()
    {
        Logger.Log("[ServerComposer] Awake: Initializing Server with Visibility...");

        // 1. Create Visibility Strategy
        _visibilityStrategy = new DummyVisibilityStrategy();
        Logger.Log("[ServerComposer] DummyVisibilityStrategy created.");

        // 2. Create the shared VisibilityManager instance
        _visibilityManager = new VisibilityManager(_visibilityStrategy);
        Logger.Log("[ServerComposer] Shared VisibilityManager created.");

        // 3. Create Network Layer, passing the shared VisibilityManager instance
        _mockNetworkLayer = new MockNetworkLayer(_visibilityManager);
        Logger.Log("[ServerComposer] MockNetworkLayer created.");

        // 4. Create Core Composer, passing network and the shared VisibilityManager instance
        try
        {
            // Assumes CoreComposer constructor is: CoreComposer(IServerNetworkLayer, VisibilityManager)
            _coreComposer = new CoreComposer(_mockNetworkLayer, _visibilityManager);
            Logger.Log("[ServerComposer] CoreComposer created successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ServerComposer] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}");
            enabled = false; // Disable this component if core setup fails
            return;
        }

        // --- Add Mock Client Views for Testing ---
        var client1View = new MockClientView { ClientId = 1, Position = Core.Primitives.Vector3.Zero };
        client1View.S2CHandler = (entityId, messageType, reader) =>
        {
            long payloadLength = reader.BaseStream.Length - reader.BaseStream.Position;
            Logger.Log($"[MockClient {client1View.ClientId} RECV] Entity: {entityId}, Type: {messageType}, PayloadLength: {payloadLength}");
            // In a real client, you'd read the payload here based on messageType
            // For example: if (messageType == MessageType.CreateEntity) { var type = reader.ReadByte(); ... }
        };
        _mockClientViews.Add(client1View);
        _visibilityManager.AddOrUpdateClientView(client1View); // Use the shared _visibilityManager
        _mockNetworkLayer.RegisterClientS2CHandler(client1View.ClientId, client1View.S2CHandler);
        Logger.Log($"[ServerComposer] Added Mock Client View {client1View.ClientId} at {client1View.Position} and registered S2C handler.");

        var client2View = new MockClientView { ClientId = 2, Position = new Core.Primitives.Vector3(10f, 0f, 10f) };
        client2View.S2CHandler = (entityId, messageType, reader) =>
        {
            long payloadLength = reader.BaseStream.Length - reader.BaseStream.Position;
            Logger.Log($"[MockClient {client2View.ClientId} RECV] Entity: {entityId}, Type: {messageType}, PayloadLength: {payloadLength}");
        };
        _mockClientViews.Add(client2View);
        _visibilityManager.AddOrUpdateClientView(client2View);
        _mockNetworkLayer.RegisterClientS2CHandler(client2View.ClientId, client2View.S2CHandler);
        Logger.Log($"[ServerComposer] Added Mock Client View {client2View.ClientId} at {client2View.Position} and registered S2C handler.");

        Logger.Log("[ServerComposer] Initialization complete in Awake.");
    }

    void Update()
    {
        if (_coreComposer != null && enabled)
        {
            // --- Update Client View Positions (Example: Simulate movement) ---
            foreach (var view in _mockClientViews)
            {
                if (view.ClientId == 1)
                {
                    // Explicitly use Core.Primitives.Vector3 for arithmetic
                    view.Position = view.Position + new Core.Primitives.Vector3(1.0f, 0f, 0.5f) * Time.deltaTime * 2f;
                    _visibilityManager.AddOrUpdateClientView(view); // Update position in the shared visibility system
                }
            }

            _coreComposer.Update(Time.deltaTime);
        }
    }

    void OnDestroy()
    {
        Logger.Log("[ServerComposer] OnDestroy: Cleaning up...");

        // Unregister mock clients from network layer and visibility manager
        if (_mockNetworkLayer != null) // Check if _mockNetworkLayer was successfully created
        {
            foreach (var view in _mockClientViews)
            {
                _mockNetworkLayer.UnregisterClientS2CHandler(view.ClientId);
            }
        }
        if (_visibilityManager != null) // Check if _visibilityManager was successfully created
        {
            foreach (var view in _mockClientViews)
            {
                 _visibilityManager.RemoveClientView(view.ClientId);
            }
        }
        _mockClientViews.Clear();

        _coreComposer?.Dispose();
        _coreComposer = null;

        // _mockNetworkLayer does not have a Dispose method in this example.
        _mockNetworkLayer = null;

        _visibilityManager?.Dispose(); // Dispose the shared VisibilityManager
        _visibilityManager = null;

        _visibilityStrategy?.Dispose(); // Dispose the strategy
        _visibilityStrategy = null;

        Logger.Log("[ServerComposer] Cleanup complete.");
    }

    // --- Example Test Methods ---

    [ContextMenu("1. Create Debug Entity (At Origin)")]
    public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }

    [ContextMenu("1b. Create Debug Entity (Far Away)")]
    public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }

    private void CreateDebugEntityAt(Core.Primitives.Vector3 position)
    {
        if (_coreComposer?.ServerLevel != null)
        {
            Logger.Log($"[ServerComposer Action] Requesting DebugEntity creation at {position}...");
            lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel);
            lastCreatedDebugEntity.Position = position; // Set initial position using Core.Primitives.Vector3
            Logger.Log($"[ServerComposer Action] DebugEntity (ID pending) added to Level's add queue. Position: {lastCreatedDebugEntity.Position}");
        }
        else
        {
            Logger.LogWarning("[ServerComposer Action] Cannot create entity, CoreComposer or Level not initialized!");
        }
    }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")]
    public void SimulateClientSyncCorrect()
    {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        // Ensure Position and Rotation are Core.Primitives types for GetHashCode
        int hash = HashCode.Combine(
            lastCreatedDebugEntity.Position.GetHashCode(),
            lastCreatedDebugEntity.Rotation.GetHashCode(),
            lastCreatedDebugEntity.Hydration.GetHashCode(),
            lastCreatedDebugEntity.Guilt.GetHashCode()
        );
        float checksum = (float)hash;
        Logger.Log($"[ServerComposer Action] Simulating ClientSyncState for Entity {lastCreatedDebugEntity.Id} with CORRECT checksum: {checksum}");
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType.ClientSyncState, writer => writer.Write(checksum));
    }

    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum)")]
    public void SimulateClientSyncIncorrect()
    {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        float incorrectChecksum = 9876.54f;
        Logger.Log($"[ServerComposer Action] Simulating ClientSyncState for Entity {lastCreatedDebugEntity.Id} with INCORRECT checksum: {incorrectChecksum}");
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType.ClientSyncState, writer => writer.Write(incorrectChecksum));
    }

    [ContextMenu("4. Kill Last Debug Entity")]
    public void KillLastDebugEntity()
    {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead)
        {
            Logger.Log($"[ServerComposer Action] Killing entity {lastCreatedDebugEntity.Id}");
            lastCreatedDebugEntity.Kill();
        }
        else
        {
            Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill.");
        }
    }
     [ContextMenu("5. Test Entity Event (SetRestPosition for last entity)")]
    public void TestEntityEventSetRestPosition()
    {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead)
        {
            Core.Primitives.Vector3 newPos = lastCreatedDebugEntity.Position + new Core.Primitives.Vector3(5f, 0, 5f);
            Logger.Log($"[ServerComposer Action] Triggering SetRestPosition for Entity {lastCreatedDebugEntity.Id} to {newPos}");
            lastCreatedDebugEntity.SetRestPosition(newPos);
        }
        else
        {
            Logger.LogWarning("[ServerComposer Action] No living DebugEntity to trigger event on.");
        }
    }
}