// File: Scripts/Server/Network/MockNetworkLayer.cs
using System;
using System.IO;
using System.Collections.Generic;
using Core.Network;
using Core.Logging;

namespace ServerSpecific.Network
{
    public class MockNetworkLayer : IServerNetworkLayer, IClientNetworkLayer
    {
        // S->C Client Handlers
        private readonly List<Action<int, MessageType, BinaryReader>> _clientMessageHandlers = new ();
        // C->S Server Handlers (via event)
        // UPDATED Signature
        public event Action<int /*clientId*/, int /*entityId*/, MessageType, BinaryReader /*payloadReader*/> OnClientMessageReceived;

        private MemoryStream _reusableMemoryStream = new MemoryStream(1024);

        // --- IClientNetworkLayer Implementation ---
        // Event for S->C messages received BY CLIENT
        public event Action<int, MessageType, BinaryReader> OnMessageReceived // S->C listener
        {
            add { Logger.Log("[MockNetworkLayer] Client S->C Handler Added."); _clientMessageHandlers.Add(value); }
            remove { Logger.Log("[MockNetworkLayer] Client S->C Handler Removed."); _clientMessageHandlers.Remove(value); }
        }

        /// <summary>
        /// Simulates a client sending data TO the server. Invokes the OnClientMessageReceived event WITH a mock clientId.
        /// </summary>
        public void SendToServer(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
        {
            // --- MOCK CLIENT ID ---
            int mockClientId = 1; // Simulate message coming from client with ID 1
            // --------------------

            Logger.Log($"[MockNetwork C->S SEND] From MockClientId={mockClientId}, Entity: {entityId}, Type: {messageType}");

            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { serializePayloadAction(writer); }
            long streamLength = _reusableMemoryStream.Position; _reusableMemoryStream.Position = 0;

            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                if (OnClientMessageReceived != null) {
                    try { OnClientMessageReceived.Invoke(mockClientId, entityId, messageType, reader); } // Pass mockClientId
                    catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error invoking server C->S handler for Entity {entityId}, Type {messageType}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                } else { Logger.LogWarning($"[MockNetworkLayer] SendToServer called, but no server handler (OnClientMessageReceived) is subscribed."); }
            }
        }

        // --- IServerNetworkLayer Implementation ---

        // BroadcastRelevant remains the same (used for Create, Events)
        public void BroadcastRelevant(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
        {
            Logger.Log($"[MockNetwork S->C BROADCAST] Entity: {entityId}, Type: {messageType}");
            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { serializePayloadAction(writer); }
            long streamLength = _reusableMemoryStream.Position; _reusableMemoryStream.Position = 0;
            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) {
                if (_clientMessageHandlers.Count == 0) { Logger.LogWarning($"[MockNetworkLayer] BroadcastRelevant called for Entity {entityId}, but no client handlers registered."); }
                foreach (var handler in _clientMessageHandlers) { _reusableMemoryStream.Position = 0; try { handler?.Invoke(entityId, messageType, reader); } catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing client S->C handler (Broadcast) for Entity {entityId}, Type {messageType}: {ex.Message}\nStackTrace: {ex.StackTrace}"); } }
            }
        }

        /// <summary>
        /// Sends data to a specific client. In this mock, we just log the clientId
        /// and then send to ALL registered client handlers (simulating it reached the right one).
        /// A more complex mock could map clientIds to specific handlers.
        /// </summary>
        public void SendToClient(int clientId, int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
        {
             Logger.Log($"[MockNetwork S->C UNICAST] To ClientId={clientId}, Entity: {entityId}, Type: {messageType}");

            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) { serializePayloadAction(writer); }
            long streamLength = _reusableMemoryStream.Position; _reusableMemoryStream.Position = 0;

            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) {
                if (_clientMessageHandlers.Count == 0) { Logger.LogWarning($"[MockNetworkLayer] SendToClient called for Entity {entityId}, but no client handlers registered."); }
                 // MOCK BEHAVIOR: Send to all handlers, assuming the correct one exists.
                foreach (var handler in _clientMessageHandlers) {
                    _reusableMemoryStream.Position = 0;
                    try { handler?.Invoke(entityId, messageType, reader); } // Handler logic on client side would process it
                    catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing client S->C handler (Unicast) for Entity {entityId}, Type {messageType}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                }
            }
        }


        // SendDestroyCommand remains the same (broadcasts DestroyEntity message)
        public void SendDestroyCommand(int entityId)
        {
             Logger.Log($"[MockNetwork S->C DESTROY] Entity: {entityId}");
             _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
             using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) {
                foreach (var handler in _clientMessageHandlers) { _reusableMemoryStream.Position = 0; try { handler?.Invoke(entityId, MessageType.DestroyEntity, reader); } catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing client S->C handler for Destroy Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); } }
             }
        }
    }
}