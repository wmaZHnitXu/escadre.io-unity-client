// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using Core.Visibility;
using ServerSpecific.Network;
using ServerSpecific.Debug; // Added for DebugPresentationManager
using Core.Logging;
using Core.Model;
using Core.Primitives;
using System;
using System.Collections.Generic;
using System.IO;

using Logger = Core.Logging.Logger;

public class ServerComposer : MonoBehaviour
{
    [Header("Core Components")]
    private CoreComposer _coreComposer;
    private MockNetworkLayer _mockNetworkLayer;
    private IVisibilityStrategy _visibilityStrategy;
    private VisibilityManager _visibilityManager;

    [Header("Debug Presentation")]
    [Tooltip("Assign the DebugPresentationManager component here (can be on the same GameObject).")]
    [SerializeField] private DebugPresentationManager debugPresentationManager; // Reference to the manager

    // --- Mock Client Views and other fields ---
    private DebugEntity lastCreatedDebugEntity;
    private class MockClientView : IClientView { /* ... same as before ... */ public int ClientId { get; set; } public Core.Primitives.Vector3 Position { get; set; } public float RadiusOfInterest { get; set; } = 50f; public Action<int, MessageType, BinaryReader> S2CHandler { get; set; } }
    private List<MockClientView> _mockClientViews = new List<MockClientView>();
    public IClientNetworkLayer ClientNetworkAccess => _mockNetworkLayer;
    public IServerNetworkLayer ServerNetworkAccess => _mockNetworkLayer;

    void Awake()
    {
        Logger.Log("[ServerComposer] Awake: Initializing Server with Visibility & Debug Presentation...");

        // --- Initialize Core ---
        _visibilityStrategy = new DummyVisibilityStrategy();
        _visibilityManager = new VisibilityManager(_visibilityStrategy);
        _mockNetworkLayer = new MockNetworkLayer(_visibilityManager); // Pass Visibility Manager
        try
        {
            _coreComposer = new CoreComposer(_mockNetworkLayer, _visibilityManager); // Pass Visibility Manager
        }
        catch (Exception ex) { Logger.LogError($"[ServerComposer] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}"); enabled = false; return; }

        // --- Initialize Debug Presentation ---
        if (debugPresentationManager == null)
        {
            // Attempt to find it on the same GameObject if not assigned
            debugPresentationManager = GetComponent<DebugPresentationManager>();
            if (debugPresentationManager == null)
            {
                Logger.LogError("[ServerComposer] DebugPresentationManager is not assigned or found on this GameObject! Debug visuals will not work.");
            }
        }

        if (debugPresentationManager != null && _coreComposer != null)
        {
            debugPresentationManager.Initialize(_coreComposer.ServerLevel); // Pass the Level instance
            Logger.Log("[ServerComposer] DebugPresentationManager initialized.");
        }
        // ---------------------------------

        // --- Add Mock Client Views (remains the same) ---
        var client1View = new MockClientView { ClientId = 1, Position = Core.Primitives.Vector3.Zero };
        client1View.S2CHandler = (entityId, messageType, reader) => { long len = reader.BaseStream.Length - reader.BaseStream.Position; Logger.Log($"[MockClient {client1View.ClientId} RECV] Entity: {entityId}, Type: {messageType}, PayloadLen: {len}"); };
        _mockClientViews.Add(client1View);
        _visibilityManager.AddOrUpdateClientView(client1View);
        _mockNetworkLayer.RegisterClientS2CHandler(client1View.ClientId, client1View.S2CHandler);
        // ... Add other mock clients if needed ...
        Logger.Log("[ServerComposer] Mock clients setup complete.");

        Logger.Log("[ServerComposer] Initialization complete in Awake.");
    }

    // Update remains the same (updates client views, calls coreComposer.Update)
    void Update() { if (_coreComposer != null && enabled) { foreach (var view in _mockClientViews) { if (view.ClientId == 1) { view.Position = view.Position + new Core.Primitives.Vector3(1.0f, 0f, 0.5f) * Time.deltaTime * 2f; _visibilityManager.AddOrUpdateClientView(view); } } _coreComposer.Update(Time.deltaTime); } }
    // OnDestroy remains the same (cleans up clients, coreComposer, visibility, strategy)
    void OnDestroy() { Logger.Log("[ServerComposer] OnDestroy: Cleaning up..."); if (_mockNetworkLayer != null) { foreach (var view in _mockClientViews) { _mockNetworkLayer.UnregisterClientS2CHandler(view.ClientId); } } if (_visibilityManager != null) { foreach (var view in _mockClientViews) { _visibilityManager.RemoveClientView(view.ClientId); } } _mockClientViews.Clear(); _coreComposer?.Dispose(); _coreComposer = null; _mockNetworkLayer = null; _visibilityManager?.Dispose(); _visibilityManager = null; _visibilityStrategy?.Dispose(); _visibilityStrategy = null; Logger.Log("[ServerComposer] Cleanup complete."); }

    // ContextMenu methods remain the same
    [ContextMenu("1. Create Debug Entity (At Origin)")] public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }
    [ContextMenu("1b. Create Debug Entity (Far Away)")] public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }
    private void CreateDebugEntityAt(Core.Primitives.Vector3 position) { if (_coreComposer?.ServerLevel != null) { Logger.Log($"[ServerComposer Action] Requesting DebugEntity creation at {position}..."); lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel); lastCreatedDebugEntity.Position = position; Logger.Log($"[ServerComposer Action] DebugEntity (ID pending) added to Level's add queue. Position: {lastCreatedDebugEntity.Position}"); } else { Logger.LogWarning("[ServerComposer Action] Cannot create entity, CoreComposer or Level not initialized!"); } }
    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")] public void SimulateClientSyncCorrect() { /* ... */ if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; } if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; } int hash = HashCode.Combine(lastCreatedDebugEntity.Position.GetHashCode(), lastCreatedDebugEntity.Rotation.GetHashCode(), lastCreatedDebugEntity.Hydration.GetHashCode(), lastCreatedDebugEntity.Guilt.GetHashCode()); float checksum = (float)hash; Logger.Log($"[ServerComposer Action] Simulating ClientSyncState for Entity {lastCreatedDebugEntity.Id} with CORRECT checksum: {checksum}"); _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType.ClientSyncState, writer => writer.Write(checksum)); }
    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum)")] public void SimulateClientSyncIncorrect() { /* ... */ if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; } if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; } float incorrectChecksum = 9876.54f; Logger.Log($"[ServerComposer Action] Simulating ClientSyncState for Entity {lastCreatedDebugEntity.Id} with INCORRECT checksum: {incorrectChecksum}"); _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType.ClientSyncState, writer => writer.Write(incorrectChecksum)); }
    [ContextMenu("4. Kill Last Debug Entity")] public void KillLastDebugEntity() { /* ... */ if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) { Logger.Log($"[ServerComposer Action] Killing entity {lastCreatedDebugEntity.Id}"); lastCreatedDebugEntity.Kill(); } else { Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill."); } }
    [ContextMenu("5. Test Entity Event (SetRestPosition for last entity)")] public void TestEntityEventSetRestPosition() { /* ... */ if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) { Core.Primitives.Vector3 newPos = lastCreatedDebugEntity.Position + new Core.Primitives.Vector3(5f, 0, 5f); Logger.Log($"[ServerComposer Action] Triggering SetRestPosition for Entity {lastCreatedDebugEntity.Id} to {newPos}"); lastCreatedDebugEntity.SetRestPosition(newPos); } else { Logger.LogWarning("[ServerComposer Action] No living DebugEntity to trigger event on."); } }

} // End Class