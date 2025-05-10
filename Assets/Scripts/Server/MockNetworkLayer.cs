// File: Scripts/Server/Network/MockNetworkLayer.cs
using System;
using System.IO; // For BinaryReader/Writer
using System.Collections.Generic;
using System.Linq;
using Core.Network;
using Core.Logging;
using Core.Visibility;

namespace ServerSpecific.Network
{
    public class MockNetworkLayer : IServerNetworkLayer, IClientNetworkLayer
    {
        public event Action<int, int, MessageType, BinaryReader> OnClientMessageReceived;

        private readonly Dictionary<int, Action<int, MessageType, BinaryReader>> _clientSpecificS2CHandlers = new();
        private readonly VisibilityManager _visibilityManager;
        private MemoryStream _reusableMemoryStream = new MemoryStream(1024);
        private Action<int, MessageType, BinaryReader> _genericOnMessageReceived; // For IClientNetworkLayer.OnMessageReceived

        public MockNetworkLayer(VisibilityManager visibilityManager)
        {
            _visibilityManager = visibilityManager ?? throw new ArgumentNullException(nameof(visibilityManager));
        }

        public void RegisterClientS2CHandler(int clientId, Action<int, MessageType, BinaryReader> handler)
        {
            if (handler == null) return;
            _clientSpecificS2CHandlers[clientId] = handler;
            Logger.Log($"[MockNetworkLayer] Registered S2C handler for ClientId={clientId}");
        }

        public void UnregisterClientS2CHandler(int clientId)
        {
            if (_clientSpecificS2CHandlers.Remove(clientId))
            {
                Logger.Log($"[MockNetworkLayer] Unregistered S2C handler for ClientId={clientId}");
            }
        }

        public event Action<int, MessageType, BinaryReader> OnMessageReceived // IClientNetworkLayer
        {
            add { _genericOnMessageReceived += value; Logger.Log($"[MockNetworkLayer] Generic S->C Handler Added."); }
            remove { _genericOnMessageReceived -= value; Logger.Log($"[MockNetworkLayer] Generic S->C Handler Removed."); }
        }

        public void SendToServer(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction) // IClientNetworkLayer
        {
            int mockClientId = 1; 
            Logger.Log($"[MockNetwork C->S SEND] From MockClientId={mockClientId}, Entity: {entityId}, Type: {messageType}");
            _reusableMemoryStream.Position = 0;
            _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                serializePayloadAction(writer);
            }
            _reusableMemoryStream.Position = 0;
            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                try
                {
                    OnClientMessageReceived?.Invoke(mockClientId, entityId, messageType, reader);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[MockNetworkLayer] Error invoking server C->S handler for Entity {entityId}, Type {messageType}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                }
            }
        }

        public void BroadcastRelevant(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction) // IServerNetworkLayer
        {
            if (_visibilityManager == null)
            {
                Logger.LogError("[MockNetworkLayer] BroadcastRelevant: VisibilityManager is null.");
                return;
            }

            IEnumerable<int> relevantClientIds = _visibilityManager.GetClientsSeeingEntity(entityId);
            if (!relevantClientIds.Any())
            {
                return;
            }
            
            Logger.Log($"[MockNetwork S->C BROADCAST] Entity: {entityId}, Type: {messageType}. Relevant clients: {string.Join(",", relevantClientIds)}");

            _reusableMemoryStream.Position = 0;
            _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                serializePayloadAction(writer);
            }

            foreach (int clientId in relevantClientIds)
            {
                // Create a new reader or reset stream for each client to ensure they read from the beginning
                _reusableMemoryStream.Position = 0;
                using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) 
                {
                    if (_clientSpecificS2CHandlers.TryGetValue(clientId, out var specificHandler))
                    {
                        try
                        {
                            specificHandler.Invoke(entityId, messageType, reader);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[MockNetworkLayer] Error executing specific S2C handler (Broadcast) for ClientId={clientId}, Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                        }
                    }
                    else
                    {
                        // Optionally invoke generic handler if no specific one for a *relevant* client.
                        // This behavior might be desirable if a generic listener is interested in all messages it's technically allowed to see.
                        if(_genericOnMessageReceived != null)
                        {
                            _reusableMemoryStream.Position = 0; // Reset again for generic, just in case
                             using (var genericReader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
                             {
                                try { _genericOnMessageReceived.Invoke(entityId, messageType, genericReader); }
                                catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing generic S2C handler (Broadcast Fallback for relevant client {clientId}) for Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                             }
                        }
                        // Logger.LogWarning($"[MockNetworkLayer] BroadcastRelevant: No specific S2C handler for relevant ClientId={clientId}. Invoked generic if available.");
                    }
                }
            }
        }

        public void SendToClient(int clientId, int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction) // IServerNetworkLayer
        {
            // Logger.Log($"[MockNetwork S->C UNICAST] To ClientId={clientId}, Entity: {entityId}, Type: {messageType}");

            _reusableMemoryStream.Position = 0;
            _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                serializePayloadAction(writer);
            }
            _reusableMemoryStream.Position = 0;
            using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                if (_clientSpecificS2CHandlers.TryGetValue(clientId, out var specificHandler))
                {
                    try
                    {
                        specificHandler.Invoke(entityId, messageType, reader);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[MockNetworkLayer] Error executing specific S2C handler (Unicast) for ClientId={clientId}, Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                    }
                }
                else
                {
                    // Fallback to generic for unicast if specific not found.
                    // This is less common for unicast but maintained for consistency with BroadcastRelevant's fallback.
                     if(_genericOnMessageReceived != null)
                     {
                        _reusableMemoryStream.Position = 0; // Reset again
                         using (var genericReader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
                         {
                            try { _genericOnMessageReceived.Invoke(entityId, messageType, genericReader); }
                            catch (Exception ex) { Logger.LogError($"[MockNetworkLayer] Error executing generic S2C handler (Unicast Fallback for client {clientId}) for Entity {entityId}: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                         }
                     }
                    // Logger.LogWarning($"[MockNetworkLayer] SendToClient: No specific S2C handler for ClientId={clientId}. Invoked generic if available.");
                }
            }
        }

        public void SendDestroyCommand(int entityId, IEnumerable<int> targetClientIds) // IServerNetworkLayer
        {
            if (targetClientIds == null || !targetClientIds.Any()) return;

            string targetIdsStr = string.Join(",", targetClientIds);
            Logger.Log($"[MockNetwork S->C DESTROY] Entity: {entityId} to Clients: [{targetIdsStr}]");

            Action<BinaryWriter> emptyPayloadAction = writer => {};

            foreach (int clientId in targetClientIds)
            {
                SendToClient(clientId, entityId, MessageType.DestroyEntity, emptyPayloadAction);
            }
        }
    }
}