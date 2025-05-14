// File: Scripts/Server/Network/MockNetworkLayer.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Core.Network;
using Core.Logging;
using Core.Visibility; // Required if we use it for BroadcastRelevant

namespace ServerSpecific.Network
{
    public class MockNetworkLayer : IServerNetworkLayer, IClientNetworkLayer
    {
        public event Action<int /*clientId*/, int /*entityId*/, MessageType, BinaryReader /*payloadReader*/> OnClientMessageReceived;
        private readonly Dictionary<int, Action<int, MessageType, BinaryReader>> _clientSpecificS2CHandlers = new();
        private VisibilityManager _visibilityManager; // Optional, can be set later
        private MemoryStream _reusableMemoryStream = new MemoryStream(1024);
        private Action<int, MessageType, BinaryReader> _genericOnMessageReceivedForClientInterface;

        // Constructor no longer requires VisibilityManager
        public MockNetworkLayer()
        {
            Logger.Log("[MockNetworkLayer] Created.");
        }

        /// <summary>
        /// Optional: Sets the VisibilityManager. If set, BroadcastRelevant will use it.
        /// Otherwise, BroadcastRelevant will send to all registered client handlers.
        /// </summary>
        public void SetVisibilityManager(VisibilityManager visibilityManager)
        {
            _visibilityManager = visibilityManager;
            Logger.Log($"[MockNetworkLayer] VisibilityManager {(visibilityManager != null ? "set" : "cleared")}.");
        }

        // --- IClientNetworkLayer Implementation ---
        public event Action<int, MessageType, BinaryReader> OnMessageReceived // S->C listener on client side
        {
            add { _genericOnMessageReceivedForClientInterface += value; Logger.Log($"[MockNetworkLayer ClientInterface] Generic S->C Handler Added."); }
            remove { _genericOnMessageReceivedForClientInterface -= value; Logger.Log($"[MockNetworkLayer ClientInterface] Generic S->C Handler Removed."); }
        }

        public void SendToServer(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
        {
            int mockClientId = 1; // Default mock client ID
            // Find if any registered mock client view has this specific mockClientId to simulate "who" is sending
            // For simplicity, just use 1 for now.
            Logger.Log($"[MockNetwork C->S SEND] From MockClientId={mockClientId}, Entity: {entityId}, Type: {messageType}");
            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { serializePayloadAction(writer); }
            _reusableMemoryStream.Position = 0;
            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                try { OnClientMessageReceived?.Invoke(mockClientId, entityId, messageType, reader); } // Notifies ServerReplicationManager
                catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error invoking server C->S handler for Entity {entityId}, Type {messageType}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
            }
        }


        // --- IServerNetworkLayer Implementation ---
        public void RegisterClientS2CHandler(int clientId, Action<int, MessageType, BinaryReader> handler)
        {
            if (handler == null) return;
            _clientSpecificS2CHandlers[clientId] = handler;
            Logger.Log($"[MockNetworkLayer ServerInterface] Registered S2C handler for ClientId={clientId}");
        }
        public void UnregisterClientS2CHandler(int clientId)
        {
            if (_clientSpecificS2CHandlers.Remove(clientId))
                Logger.Log($"[MockNetworkLayer ServerInterface] Unregistered S2C handler for ClientId={clientId}");
        }


        public void BroadcastRelevant(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
        {
            IEnumerable<int> relevantClientIds;
            if (_visibilityManager != null)
            {
                relevantClientIds = _visibilityManager.GetClientsSeeingEntity(entityId).ToList(); // ToList to prevent issues if collection changes
                if (!relevantClientIds.Any()) return; // No one sees it
                Logger.Log($"[MockNetwork S->C BROADCAST-RELEVANT] Entity: {entityId}, Type: {messageType}. Targets: [{string.Join(",", relevantClientIds)}]");
            }
            else
            {
                // Fallback: If no visibility manager, send to all specifically registered S2C handlers + generic IClientNetworkLayer handler
                relevantClientIds = _clientSpecificS2CHandlers.Keys.ToList(); // All known client handlers
                 if(!relevantClientIds.Any() && _genericOnMessageReceivedForClientInterface == null) return;
                Logger.LogWarning($"[MockNetwork S->C BROADCAST-ALL (VM not set)] Entity: {entityId}, Type: {messageType}. Targets: All registered handlers.");
            }

            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { serializePayloadAction(writer); }

            foreach (int clientId in relevantClientIds)
            {
                _reusableMemoryStream.Position = 0;
                using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
                {
                    if (_clientSpecificS2CHandlers.TryGetValue(clientId, out var specificHandler))
                    {
                        try { specificHandler.Invoke(entityId, messageType, reader); }
                        catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing specific S2C handler (Broadcast) for ClientId={clientId}, Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                    }
                    else if (_visibilityManager == null && _genericOnMessageReceivedForClientInterface != null) // Only use generic if VM not set (meaning it's a broad mock broadcast)
                    {
                        // This case is tricky: if _visibilityManager *is* set, we only target specific relevant clients.
                        // If a relevant client doesn't have a *specific* S2C handler, it might miss the message.
                        // The generic IClientNetworkLayer.OnMessageReceived is for the *client app* to hook into,
                        // not for the mock to selectively dispatch to if a specific handler is missing for a "relevant" client.
                        // For simplicity with mock: if VM is active, we only send to specific handlers of relevant clients.
                        // If VM is *not* active, we send to all specific handlers AND the generic client interface handler.
                    }
                }
            }
            // If VM is NOT set, and we have a generic IClientNetworkLayer handler, send to it as well.
            if(_visibilityManager == null && _genericOnMessageReceivedForClientInterface != null)
            {
                 _reusableMemoryStream.Position = 0;
                 using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
                 {
                    try { _genericOnMessageReceivedForClientInterface.Invoke(entityId, messageType, reader); }
                    catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing generic IClientNetworkLayer S2C handler (Broadcast Fallback) for Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                 }
            }
        }

        public void SendToClient(int clientId, int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
        {
            // Logger.Log($"[MockNetwork S->C UNICAST] To ClientId={clientId}, Entity: {entityId}, Type: {messageType}");
            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { serializePayloadAction(writer); }
            _reusableMemoryStream.Position = 0;

            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                bool handled = false;
                if (_clientSpecificS2CHandlers.TryGetValue(clientId, out var specificHandler))
                {
                    try { specificHandler.Invoke(entityId, messageType, reader); handled = true; }
                    catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing specific S2C handler (Unicast) for ClientId={clientId}, Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                }
                // Also try the generic IClientNetworkLayer.OnMessageReceived if this mock instance is being used by a "client"
                if (_genericOnMessageReceivedForClientInterface != null) {
                     _reusableMemoryStream.Position = 0; // Reset for second read
                     using(var reader2 = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { // New reader for safety
                        try { _genericOnMessageReceivedForClientInterface.Invoke(entityId, messageType, reader2); handled = true; }
                        catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing generic IClientNetworkLayer S2C handler (Unicast) for ClientId={clientId}, Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                     }
                }
                if (!handled) { Logger.LogWarning($"[MockNetworkLayer] SendToClient: No handler for ClientId={clientId} (specific or generic IClientNetworkLayer.OnMessageReceived)."); }
            }
        }

        public void SendDestroyCommand(int entityId, IEnumerable<int> targetClientIds)
        {
            if (targetClientIds == null || !targetClientIds.Any()) return;
            string targetIdsStr = string.Join(",", targetClientIds);
            Logger.Log($"[MockNetwork S->C DESTROY] Entity: {entityId} to Clients: [{targetIdsStr}]");
            Action<BinaryWriter> emptyPayloadAction = writer => { };
            foreach (int clientId in targetClientIds) { SendToClient(clientId, entityId, MessageType.DestroyEntity, emptyPayloadAction); }
        }
    }
}