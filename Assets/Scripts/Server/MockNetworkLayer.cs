// File: Scripts/Server/Network/MockNetworkLayer.cs (or a shared location if client/server are separate projects later)
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // Inherits from MonoBehaviour
using Core.Network;
using Core.Logging;
using Core.Visibility;
using Logger = Core.Logging.Logger; // For VisibilityManager in BroadcastRelevant

// No longer ServerSpecific namespace if it's meant to be shared or a general mock
// namespace ServerSpecific.Network
// {

public class MockNetworkLayer : MonoBehaviour, IServerNetworkLayer, IClientNetworkLayer
{
    // Event for Server to listen to Client messages (C->S)
    public event Action<int /*sendingClientId*/, int /*entityId (target/context)*/, MessageType, BinaryReader> C2S_OnMessageReceived;

    // Event for Client to listen to Server messages (S->C)
    // Parameters: entityId, messageType, payloadReader
    public event Action<int, MessageType, BinaryReader> S2C_OnMessageReceived;

    [Header("Mock Settings")]
    [Tooltip("Default Client ID to use when SendToServer is called from a context without a specific client (e.g., test runner).")]
    public int defaultSendingClientId = 1;


    private VisibilityManager _visibilityManager; // Optional for BroadcastRelevant
    private MemoryStream _reusableMemoryStream = new MemoryStream(1024);

    // Keep track of which "mock clients" are "connected" to receive S2C messages.
    // This is a simple way to simulate multiple clients if needed later for BroadcastRelevant.
    // For now, S2C_OnMessageReceived is a single event, implying one client listener.
    // If multiple clients, S2C_OnMessageReceived would need a clientId param or separate events.
    private HashSet<int> _mockConnectedClientIds = new HashSet<int>();


    void Awake()
    {
        Logger.Log("[MockNetworkLayer MB] Awake. Instance created.");
        // Automatically "connect" the default client ID if used by a ClientComposer.
        // This is more for conceptual clarity. A real client would explicitly connect.
        // RegisterMockClient(defaultSendingClientId); // ClientComposer will register itself by subscribing.
    }

    public void SetVisibilityManager(VisibilityManager visibilityManager)
    {
        _visibilityManager = visibilityManager;
        Logger.Log($"[MockNetworkLayer MB] VisibilityManager {(visibilityManager != null ? "set" : "cleared")}.");
    }

    public void RegisterMockClient(int clientId)
    {
        _mockConnectedClientIds.Add(clientId);
        Logger.Log($"[MockNetworkLayer MB] Mock Client {clientId} 'connected' (registered to potentially receive broadcasts).");
    }
    public void UnregisterMockClient(int clientId)
    {
        _mockConnectedClientIds.Remove(clientId);
         Logger.Log($"[MockNetworkLayer MB] Mock Client {clientId} 'disconnected'.");
    }


    // --- IClientNetworkLayer Implementation (Client calls these) ---
    event Action<int, MessageType, BinaryReader> IClientNetworkLayer.OnMessageReceived
    {
        add { S2C_OnMessageReceived += value; Logger.Log("[MockNetworkLayer MB] Client subscribed to S2C_OnMessageReceived."); }
        remove { S2C_OnMessageReceived -= value; Logger.Log("[MockNetworkLayer MB] Client unsubscribed from S2C_OnMessageReceived."); }
    }

    void IClientNetworkLayer.SendToServer(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        // Logger.Log($"[MockNetworkLayer MB C->S SEND] From Client {defaultSendingClientId}, EntityCtx: {entityId}, Type: {messageType}");
        _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
        using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            serializePayloadAction(writer);
        }
        _reusableMemoryStream.Position = 0;

        using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            try
            {
                C2S_OnMessageReceived?.Invoke(defaultSendingClientId, entityId, messageType, reader);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MockNetworkLayer MB] Error invoking C2S_OnMessageReceived: {ex.Message}\nPayload Type: {messageType}, Entity: {entityId}\n{ex.StackTrace}");
            }
        }
    }


    // --- IServerNetworkLayer Implementation (Server calls these) ---
    event Action<int, int, MessageType, BinaryReader> IServerNetworkLayer.OnClientMessageReceived
    {
        add { C2S_OnMessageReceived += value; Logger.Log("[MockNetworkLayer MB] Server subscribed to C2S_OnMessageReceived."); }
        remove { C2S_OnMessageReceived -= value; Logger.Log("[MockNetworkLayer MB] Server unsubscribed from C2S_OnMessageReceived.");}
    }

    void IServerNetworkLayer.BroadcastRelevant(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
        using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            serializePayloadAction(writer);
        }

        IEnumerable<int> targetClientIds;
        if (_visibilityManager != null)
        {
            targetClientIds = _visibilityManager.GetClientsSeeingEntity(entityId).ToList();
            if (!targetClientIds.Any() && messageType != MessageType.DestroyEntity && messageType != MessageType.VanishEntity) // Allow vital lifecycle messages
            {
                // Logger.Log($"[MockNetworkLayer MB S->C BROADCAST] Entity {entityId} Type {messageType} - no relevant clients.");
                return;
            }
            // Logger.Log($"[MockNetworkLayer MB S->C BROADCAST-RELEVANT] Entity: {entityId}, Type: {messageType}. Targets based on VM: [{string.Join(",", targetClientIds)}]");
        }
        else
        {
            // If no VM, broadcast to all "connected" mock clients.
            targetClientIds = new List<int>(_mockConnectedClientIds);
            // Logger.LogWarning($"[MockNetworkLayer MB S->C BROADCAST-ALL (VM not set)] Entity: {entityId}, Type: {messageType}. Targets: All registered mock clients.");
        }

        foreach (int clientId in targetClientIds) // For each client who should receive it
        {
            // For this simple mock, we assume S2C_OnMessageReceived is for THE client.
            // If we had per-client handlers on the S2C side, we'd route here.
            // For now, just invoke the single S2C_OnMessageReceived.
            // The 'clientId' in targetClientIds is effectively ignored by THIS mock's S2C_OnMessageReceived invocation.
            // A more complex mock would use it.
            _reusableMemoryStream.Position = 0;
            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                try
                {
                    // Logger.Log($"[MockNetworkLayer MB] Invoking S2C_OnMessageReceived for Entity {entityId}, Type {messageType} (intended for client {clientId})");
                    S2C_OnMessageReceived?.Invoke(entityId, messageType, reader);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[MockNetworkLayer MB] Error invoking S2C_OnMessageReceived (Broadcast): {ex.Message}\nPayload Type: {messageType}, Entity: {entityId}\n{ex.StackTrace}");
                }
            }
        }
    }

    void IServerNetworkLayer.SendToClient(int clientId, int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        // Logger.Log($"[MockNetworkLayer MB S->C UNICAST] To ClientId={clientId}, Entity: {entityId}, Type: {messageType}");
        _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
        using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            serializePayloadAction(writer);
        }
        _reusableMemoryStream.Position = 0;

        using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            try
            {
                // The 'clientId' here is who the server INTENDS to send to.
                // Our S2C_OnMessageReceived event doesn't take clientId, so it's implicitly for "the" client.
                S2C_OnMessageReceived?.Invoke(entityId, messageType, reader);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MockNetworkLayer MB] Error invoking S2C_OnMessageReceived (Unicast to {clientId}): {ex.Message}\nPayload Type: {messageType}, Entity: {entityId}\n{ex.StackTrace}");
            }
        }
    }

    void IServerNetworkLayer.SendVanishCommand(int entityId, IEnumerable<int> targetClientIds)
    {
        if (targetClientIds == null || !targetClientIds.Any()) return;
        // Logger.Log($"[MockNetworkLayer MB S->C VANISH] Entity: {entityId} to Clients: [{string.Join(",", targetClientIds)}]");
        Action<BinaryWriter> emptyPayloadAction = writer => { };
        var serverLayer = (IServerNetworkLayer)this; // Explicit interface call
        foreach (int clientId in targetClientIds)
        {
            serverLayer.SendToClient(clientId, entityId, MessageType.VanishEntity, emptyPayloadAction);
        }
    }
}

// } // End of potential namespace