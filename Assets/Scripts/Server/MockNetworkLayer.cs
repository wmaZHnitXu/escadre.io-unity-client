// File: Scripts/Server/Network/MockNetworkLayer.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; 
using Core.Network;
using Core.Logging;
using Core.Visibility;
using Logger = Core.Logging.Logger; 

public class MockNetworkLayer : MonoBehaviour, IServerNetworkLayer, IClientNetworkLayer
{
    // C2S event remains the same (server receives a BinaryReader)
    public event Action<int /*sendingNetworkSourceId*/, int /*entityId (target/context)*/, MessageType, BinaryReader> C2S_OnMessageReceived;
    
    // S2C event now passes byte[] for payload
    private event Action<int /*entityId*/, MessageType, byte[] /*payload*/> S2C_OnMessageReceived_Internal;

    [Header("Mock Settings")]
    // defaultSendingClientId is no longer used by IClientNetworkLayer.SendToServer directly,
    // but ClientComposer still sets it on this instance. It's used by RegisterMockClientS2CRouting.
    [Tooltip("Default Network Source ID for associating this MockNetworkLayer instance if it acts as a specific client's direct network interface (for S2C routing).")]
    public int defaultSendingClientId = 1; 


    private VisibilityManager _visibilityManager; 
    private MemoryStream _reusableMemoryStream = new MemoryStream(1024);

    private HashSet<int> _listeningNetworkSourceIds = new HashSet<int>();
    private Dictionary<int, int> _gameClientIdToNetworkSourceIdMap = new Dictionary<int, int>();
    private Dictionary<int, int> _networkSourceIdToGameClientIdMap = new Dictionary<int, int>();

    // S2C routing table: maps networkSourceId to its specific handler.
    private Dictionary<int, Action<int, MessageType, byte[]>> _s2cHandlersByNetworkId = new Dictionary<int, Action<int, MessageType, byte[]>>();


    void Awake()
    {
        Logger.Log("[MockNetworkLayer MB] Awake. Instance created.");
    }

    public void SetVisibilityManager(VisibilityManager visibilityManager)
    {
        _visibilityManager = visibilityManager;
        Logger.Log($"[MockNetworkLayer MB] VisibilityManager {(visibilityManager != null ? "set" : "cleared")}.");
    }

    public void RegisterMockClientS2CRouting(int networkSourceId, Action<int, MessageType, byte[]> handler)
    {
        _listeningNetworkSourceIds.Add(networkSourceId); // Still useful for general tracking
        _s2cHandlersByNetworkId[networkSourceId] = handler;
        Logger.Log($"[MockNetworkLayer MB] Client with Network Source ID {networkSourceId} registered for S2C message routing with specific handler.");
    }

    public void UnregisterMockClientS2CRouting(int networkSourceId)
    {
        _listeningNetworkSourceIds.Remove(networkSourceId);
        _s2cHandlersByNetworkId.Remove(networkSourceId);

        if (_networkSourceIdToGameClientIdMap.TryGetValue(networkSourceId, out int gameClientId))
        {
            _gameClientIdToNetworkSourceIdMap.Remove(gameClientId);
            _networkSourceIdToGameClientIdMap.Remove(networkSourceId);
            Logger.Log($"[MockNetworkLayer MB] Cleaned up mappings for disconnected Network Source ID {networkSourceId} (was Game Client ID {gameClientId}).");
        }
        Logger.Log($"[MockNetworkLayer MB] Client with Network Source ID {networkSourceId} unregistered from S->C message routing.");
    }

    public void MapNetworkSourceToClientId(int networkSourceId, int gameClientId)
    {
        if (_gameClientIdToNetworkSourceIdMap.TryGetValue(gameClientId, out int oldNetworkSourceId) && oldNetworkSourceId != networkSourceId)
        {
            _networkSourceIdToGameClientIdMap.Remove(oldNetworkSourceId);
            Logger.LogWarning($"[MockNetworkLayer MB] Game Client ID {gameClientId} was previously mapped to Network Source ID {oldNetworkSourceId}. Remapping to {networkSourceId}.");
        }
        if (_networkSourceIdToGameClientIdMap.TryGetValue(networkSourceId, out int oldGameClientId) && oldGameClientId != gameClientId)
        {
            _gameClientIdToNetworkSourceIdMap.Remove(oldGameClientId);
             Logger.LogWarning($"[MockNetworkLayer MB] Network Source ID {networkSourceId} was previously mapped to Game Client ID {oldGameClientId}. Remapping to {gameClientId}.");
        }

        _gameClientIdToNetworkSourceIdMap[gameClientId] = networkSourceId;
        _networkSourceIdToGameClientIdMap[networkSourceId] = gameClientId;
        Logger.Log($"[MockNetworkLayer MB] Mapped Network Source ID {networkSourceId} to Game Client ID {gameClientId}.");
    }
    
    public void RemoveNetworkSourceMappingForClientId(int gameClientId)
    {
        if (_gameClientIdToNetworkSourceIdMap.TryGetValue(gameClientId, out int networkSourceId))
        {
            _gameClientIdToNetworkSourceIdMap.Remove(gameClientId);
            _networkSourceIdToGameClientIdMap.Remove(networkSourceId);
            Logger.Log($"[MockNetworkLayer MB] Removed mapping for Game Client ID {gameClientId} (was Network Source ID {networkSourceId}).");
        }
    }


    // --- IClientNetworkLayer Implementation ---
    // Event now uses byte[]
    event Action<int, MessageType, byte[]> IClientNetworkLayer.OnMessageReceived
    {
        add 
        {
            // This 'defaultSendingClientId' is set by the ClientComposer instance using this MockNetworkLayer.
            // It correctly identifies which client is subscribing.
            int clientIdForHandler = this.defaultSendingClientId; 
            if (_s2cHandlersByNetworkId.ContainsKey(clientIdForHandler))
            {
                _s2cHandlersByNetworkId[clientIdForHandler] += value;
            }
            else
            {
                _s2cHandlersByNetworkId[clientIdForHandler] = value;
            }
            Logger.Log($"[MockNetworkLayer MB] Client subscribed to S2C via specific handler map (Network Source ID: {clientIdForHandler}).");
        }
        remove 
        {
            int clientIdForHandler = this.defaultSendingClientId;
            if (_s2cHandlersByNetworkId.ContainsKey(clientIdForHandler))
            {
                _s2cHandlersByNetworkId[clientIdForHandler] -= value;
                if(_s2cHandlersByNetworkId[clientIdForHandler] == null) // Remove key if no listeners left
                {
                    _s2cHandlersByNetworkId.Remove(clientIdForHandler);
                }
            }
            Logger.Log($"[MockNetworkLayer MB] Client unsubscribed from S2C via specific handler map (Network Source ID: {clientIdForHandler}).");
        }
    }

    // SendToServer now takes sendingNetworkSourceId
    void IClientNetworkLayer.SendToServer(int sendingNetworkSourceId, int entityIdContext, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        Logger.Log($"[MockNetworkLayer MB C->S SEND] From NetworkSourceID {sendingNetworkSourceId}, EntityCtx: {entityIdContext}, Type: {messageType}");
        _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
        using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            serializePayloadAction(writer);
        }
        _reusableMemoryStream.Position = 0;

        using (var reader = new BinaryReader(_reusableMemoryStream, System.Text.Encoding.UTF8, true)) // Server still gets a reader
        {
            try
            {
                // Pass the explicit sendingNetworkSourceId
                C2S_OnMessageReceived?.Invoke(sendingNetworkSourceId, entityIdContext, messageType, reader);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MockNetworkLayer MB] Error invoking C2S_OnMessageReceived: {ex.Message}\nPayload Type: {messageType}, Entity: {entityIdContext}, SenderID: {sendingNetworkSourceId}\n{ex.StackTrace}");
            }
        }
    }


    // --- IServerNetworkLayer Implementation ---
    event Action<int, int, MessageType, BinaryReader> IServerNetworkLayer.OnClientMessageReceived
    {
        add { C2S_OnMessageReceived += value; Logger.Log("[MockNetworkLayer MB] Server subscribed to C2S_OnMessageReceived."); }
        remove { C2S_OnMessageReceived -= value; Logger.Log("[MockNetworkLayer MB] Server unsubscribed from C2S_OnMessageReceived.");}
    }

    private void DispatchS2CMessage(int targetNetworkSourceId, int entityId, MessageType messageType, MemoryStream memoryStreamWithPayload)
    {
        if (_s2cHandlersByNetworkId.TryGetValue(targetNetworkSourceId, out var handler) && handler != null)
        {
            byte[] payloadBytes = memoryStreamWithPayload.ToArray(); // Get a copy of the bytes
            try
            {
                // Logger.Log($"[MockNetworkLayer MB] Invoking S2C specific handler for NetworkSourceID {targetNetworkSourceId}. Entity {entityId}, Type {messageType}, Payload Size: {payloadBytes.Length}");
                handler.Invoke(entityId, messageType, payloadBytes);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MockNetworkLayer MB] Error invoking S2C specific handler (DispatchS2CMessage to {targetNetworkSourceId}): {ex.Message}\nPayload Type: {messageType}, Entity: {entityId}\n{ex.StackTrace}");
            }
        }
        else
        {
            // Logger.LogWarning($"[MockNetworkLayer MB S->C] No specific S2C handler for NetworkSourceID {targetNetworkSourceId}. Message for Entity {entityId}, Type {messageType} dropped.");
        }
    }


    void IServerNetworkLayer.BroadcastRelevant(int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
        using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
        {
            serializePayloadAction(writer);
        }

        IEnumerable<int> targetGameClientIds;
        if (_visibilityManager != null)
        {
            targetGameClientIds = _visibilityManager.GetClientsSeeingEntity(entityId).ToList();
            if (!targetGameClientIds.Any() && messageType != MessageType.DestroyEntity && messageType != MessageType.VanishEntity) 
            {
                return;
            }
        }
        else
        {
            targetGameClientIds = new List<int>(_gameClientIdToNetworkSourceIdMap.Keys);
        }

        foreach (int gameClientId in targetGameClientIds) 
        {
            if (_gameClientIdToNetworkSourceIdMap.TryGetValue(gameClientId, out int targetNetworkSourceId))
            {
                DispatchS2CMessage(targetNetworkSourceId, entityId, messageType, _reusableMemoryStream);
            }
            else
            {
                Logger.LogWarning($"[MockNetworkLayer MB S->C BROADCAST] No network source mapping for Game Client ID {gameClientId}. Cannot send message for Entity {entityId}, Type {messageType}.");
            }
        }
    }

    void IServerNetworkLayer.SendToClient(int gameClientId, int entityId, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        if (_gameClientIdToNetworkSourceIdMap.TryGetValue(gameClientId, out int targetNetworkSourceId))
        {
            _reusableMemoryStream.Position = 0; _reusableMemoryStream.SetLength(0);
            using (var writer = new BinaryWriter(_reusableMemoryStream, System.Text.Encoding.UTF8, true))
            {
                serializePayloadAction(writer);
            }
            DispatchS2CMessage(targetNetworkSourceId, entityId, messageType, _reusableMemoryStream);
        }
        else
        {
            Logger.LogWarning($"[MockNetworkLayer MB S->C UNICAST] No network source mapping for Game Client ID {gameClientId}. Cannot send message for Entity {entityId}, Type {messageType}.");
        }
    }

    void IServerNetworkLayer.SendVanishCommand(int entityId, IEnumerable<int> targetGameClientIds)
    {
        if (targetGameClientIds == null || !targetGameClientIds.Any()) return;
        Action<BinaryWriter> emptyPayloadAction = writer => { };
        var serverLayer = (IServerNetworkLayer)this; 
        foreach (int gameClientId in targetGameClientIds)
        {
            serverLayer.SendToClient(gameClientId, entityId, MessageType.VanishEntity, emptyPayloadAction);
        }
    }
}