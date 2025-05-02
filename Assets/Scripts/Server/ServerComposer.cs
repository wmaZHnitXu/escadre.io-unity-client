// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using ServerSpecific.Network; // MockNetworkLayer namespace
using Core.Logging;
using Server.Core.Model;
using Server.Core.Primitives;
using System;
using Logger = Core.Logging.Logger; // For HashCode in example method

/// <summary>
/// Unity MonoBehaviour for setting up and running the server-side core logic.
/// </summary>
public class ServerComposer : MonoBehaviour
{
    private CoreComposer _coreComposer;
    private MockNetworkLayer _mockNetworkLayer;
    private DebugEntity lastCreatedDebugEntity; // Keep track for testing

    // Expose interfaces for testing / potential client connection
    public IClientNetworkLayer ClientNetworkAccess => _mockNetworkLayer;
    public IServerNetworkLayer ServerNetworkAccess => _mockNetworkLayer;

    void Awake()
    {
        Logger.Log("[ServerComposer] Awake: Initializing Server...");
        _mockNetworkLayer = new MockNetworkLayer();
        Logger.Log("[ServerComposer] MockNetworkLayer created.");
        try {
            _coreComposer = new CoreComposer(_mockNetworkLayer); // Manager subscribes to C->S events here
            Logger.Log("[ServerComposer] CoreComposer created successfully.");
        } catch (Exception ex) { Logger.LogError($"[ServerComposer] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}"); enabled = false; return; }
        Logger.Log("[ServerComposer] Initialization complete in Awake.");
    }

    void Update()
    {
        // Only drive the model update loop
        if (_coreComposer != null && enabled) { _coreComposer.Update(Time.deltaTime); }
    }

    void OnDestroy()
    {
        Logger.Log("[ServerComposer] OnDestroy: Cleaning up...");
        _coreComposer?.Dispose();
        _coreComposer = null; _mockNetworkLayer = null;
        Logger.Log("[ServerComposer] Cleanup complete.");
    }

    // --- Example Test Methods ---

    [ContextMenu("1. Create Debug Entity")]
    public void CreateDebugEntityOnServer()
    {
        if (_coreComposer?.ServerLevel != null) { Logger.Log("[ServerComposer Action] Requesting DebugEntity creation..."); lastCreatedDebugEntity = new DebugEntity(_coreComposer.ServerLevel); Logger.Log("[ServerComposer Action] DebugEntity added to Level's add queue."); }
        else { Logger.LogWarning("[ServerComposer Action] Cannot create entity, CoreComposer or Level not initialized!"); }
    }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum)")]
    public void SimulateClientSyncCorrect()
    {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }

        // Calculate correct checksum (must match DebugEntityProxy.ServerProxy calculation)
        int hash = HashCode.Combine( lastCreatedDebugEntity.Position.GetHashCode(), lastCreatedDebugEntity.Rotation.GetHashCode(), lastCreatedDebugEntity.Hydration.GetHashCode(), lastCreatedDebugEntity.Guilt.GetHashCode());
        float checksum = (float)hash;
        Logger.Log($"[ServerComposer Action] Simulating ClientSyncState for Entity {lastCreatedDebugEntity.Id} with CORRECT checksum: {checksum}");

        // Simulate client sending the message via the Client interface side
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType.ClientSyncState, writer => writer.Write(checksum));
        // Expectation: Server manager receives, calls proxy check -> returns false, NO UpdateState sent via SendToClient.
    }

     [ContextMenu("3. Simulate Client Sync (Incorrect Checksum)")]
    public void SimulateClientSyncIncorrect()
    {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (_mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }

        float incorrectChecksum = 9876.54f;
        Logger.Log($"[ServerComposer Action] Simulating ClientSyncState for Entity {lastCreatedDebugEntity.Id} with INCORRECT checksum: {incorrectChecksum}");

        // Simulate client sending the message
        _mockNetworkLayer.SendToServer(lastCreatedDebugEntity.Id, MessageType.ClientSyncState, writer => writer.Write(incorrectChecksum));
        // Expectation: Server manager receives, calls proxy check -> returns true, manager calls SendToClient with UpdateState. MockNetworkLayer logs S->C UNICAST.
    }


     [ContextMenu("4. Kill Last Debug Entity")]
     public void KillLastDebugEntity()
     {
         if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead) { Logger.Log($"[ServerComposer Action] Killing entity {lastCreatedDebugEntity.Id}"); lastCreatedDebugEntity.Kill(); }
         else { Logger.LogWarning("[ServerComposer Action] No living DebugEntity tracked to kill."); }
     }
}