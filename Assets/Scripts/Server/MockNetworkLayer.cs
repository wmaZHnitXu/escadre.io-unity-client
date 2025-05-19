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
    public event Action<int /*sendingNetworkSourceId*/, int /*entityId (target/context)*/, MessageType, BinaryReader> C2S_OnMessageReceived;
    public event Action<int /*entityId*/, MessageType, BinaryReader> S2C_OnMessageReceived; // This is for THE client instance running this code.

    [Header("Mock Settings")]
    [Tooltip("Default Network Source ID for C->S messages if not overridden by a specific ClientComposer instance.")]
    public int defaultSendingClientId = 1; // This is the Network Source ID

    private VisibilityManager _visibilityManager; 
    private MemoryStream _reusableMemoryStream = new MemoryStream(1024);

    // Tracks which network source IDs (e.g., individual ClientComposer instances) are listening for S->C messages.
    // Key: networkSourceId (e.g., ClientComposer's instance ID)
    private HashSet<int> _listeningNetworkSourceIds = new HashSet<int>();

    // Maps a game session's ClientId (from JWT/validation) to its current networkSourceId.
    // Key: gameClientId (from ClientIdentity), Value: networkSourceId
    // This allows routing S->C messages targeted at a gameClientId to the correct network pipe.
    private Dictionary<int, int> _gameClientIdToNetworkSourceIdMap = new Dictionary<int, int>();
    private Dictionary<int, int> _networkSourceIdToGameClientIdMap = new Dictionary<int, int>(); // Reverse lookup for cleanup

    void Awake()
    {
        Logger.Log("[MockNetworkLayer MB] Awake. Instance created.");
    }

    public void SetVisibilityManager(VisibilityManager visibilityManager)
    {
        _visibilityManager = visibilityManager;
        Logger.Log($"[MockNetworkLayer MB] VisibilityManager {(visibilityManager != null ? "set" : "cleared")}.");
    }

    // Called by ClientComposer instances to register their network source ID for S->C message routing
    public void RegisterMockClientS2CRouting(int networkSourceId)
    {
        _listeningNetworkSourceIds.Add(networkSourceId);
        Logger.Log($"[MockNetworkLayer MB] ClientComposer instance with Network Source ID {networkSourceId} registered for S->C message routing.");
    }

    public void UnregisterMockClientS2CRouting(int networkSourceId)
    {
        _listeningNetworkSourceIds.Remove(networkSourceId);
        // Also remove any mappings associated with this networkSourceId if it disconnects
        if (_networkSourceIdToGameClientIdMap.TryGetValue(networkSourceId, out int gameClientId))
        {
            _gameClientIdToNetworkSourceIdMap.Remove(gameClientId);
            _networkSourceIdToGameClientIdMap.Remove(networkSourceId);
            Logger.Log($"[MockNetworkLayer MB] Cleaned up mappings for disconnected Network Source ID {networkSourceId} (was Game Client ID {gameClientId}).");
        }
        Logger.Log($"[MockNetworkLayer MB] ClientComposer instance with Network Source ID {networkSourceId} unregistered from S->C message routing.");
    }

    // Called by Core.CoreComposer after successful client validation
    public void MapNetworkSourceToClientId(int networkSourceId, int gameClientId)
    {
        // If this gameClientId was previously mapped to a different networkSourceId, clean that up.
        if (_gameClientIdToNetworkSourceIdMap.TryGetValue(gameClientId, out int oldNetworkSourceId) && oldNetworkSourceId != networkSourceId)
        {
            _networkSourceIdToGameClientIdMap.Remove(oldNetworkSourceId);
            Logger.LogWarning($"[MockNetworkLayer MB] Game Client ID {gameClientId} was previously mapped to Network Source ID {oldNetworkSourceId}. Remapping to {networkSourceId}.");
        }
        // If this networkSourceId was previously mapped to a different gameClientId, clean that up.
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
    event Action<int, MessageType, BinaryReader> IClientNetworkLayer.OnMessageReceived
    {
        add { S2C_OnMessageReceived += value; Logger.Log($"[MockNetworkLayer MB] Client subscribed to S2C_OnMessageReceived (Network Source ID: {this.defaultSendingClientId})."); }
        remove { S2C_OnMessageReceived -= value; Logger.Log($"[MockNetworkLayer MB] Client unsubscribed from S2C_OnMessageReceived (Network Source ID: {this.defaultSendingClientId})."); }
    }

    void IClientNetworkLayer.SendToServer(int entityIdContext, MessageType messageType, Action<BinaryWriter> serializePayloadAction)
    {
        // 'this.defaultSendingClientId' here is the networkSourceId of the ClientComposer calling this.
        Logger.Log($"[MockNetworkLayer MB C->S SEND] From NetworkSourceID {this.defaultSendingClientId}, EntityCtx: {entityIdContext}, Type: {messageType}");
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
                C2S_OnMessageReceived?.Invoke(this.defaultSendingClientId, entityIdContext, messageType, reader);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MockNetworkLayer MB] Error invoking C2S_OnMessageReceived: {ex.Message}\nPayload Type: {messageType}, Entity: {entityIdContext}\n{ex.StackTrace}");
            }
        }
    }


    // --- IServerNetworkLayer Implementation ---
    event Action<int, int, MessageType, BinaryReader> IServerNetworkLayer.OnClientMessageReceived
    {
        add { C2S_OnMessageReceived += value; Logger.Log("[MockNetworkLayer MB] Server subscribed to C2S_OnMessageReceived."); }
        remove { C2S_OnMessageReceived -= value; Logger.Log("[MockNetworkLayer MB] Server unsubscribed from C2S_OnMessageReceived.");}
    }

    private void DispatchS2CMessage(int targetNetworkSourceId, int entityId, MessageType messageType, MemoryStream memoryStream)
    {
        // This mock assumes only one S2C_OnMessageReceived handler (THE client instance).
        // If targetNetworkSourceId matches the defaultSendingClientId of *this* MockNetworkLayer instance
        // (which is set by the ClientComposer running the client logic), then invoke.
        // This simulates sending to a specific client "connection".
        if (_listeningNetworkSourceIds.Contains(targetNetworkSourceId))
        {
            // Check if this specific MockNetworkLayer instance is the one associated with targetNetworkSourceId
            // In a real setup, this would be a direct send over a socket/connection.
            // Here, we rely on ClientComposer setting its defaultSendingClientId on the MockNetworkLayer it uses.
            // And S2C_OnMessageReceived is subscribed by that ClientComposer's EntityManager.
            // This check is a bit indirect for a mock. A better mock might have a dictionary of S2C_OnMessageReceived handlers keyed by networkSourceId.
            // For now, if the source ID is listening, we assume the S2C_OnMessageReceived is for it.
            
            memoryStream.Position = 0;
            using (var reader = new BinaryReader(memoryStream, System.Text.Encoding.UTF8, true))
            {
                try
                {
                    // Logger.Log($"[MockNetworkLayer MB] Invoking S2C_OnMessageReceived for Entity {entityId}, Type {messageType} (intended for NetworkSourceID {targetNetworkSourceId})");
                    S2C_OnMessageReceived?.Invoke(entityId, messageType, reader);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[MockNetworkLayer MB] Error invoking S2C_OnMessageReceived (DispatchS2CMessage to {targetNetworkSourceId}): {ex.Message}\nPayload Type: {messageType}, Entity: {entityId}\n{ex.StackTrace}");
                }
            }
        }
        else
        {
            // Logger.LogWarning($"[MockNetworkLayer MB S->C] No listening client for NetworkSourceID {targetNetworkSourceId}. Message for Entity {entityId}, Type {messageType} dropped.");
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
            // If no VM, broadcast to all *mapped* game clients.
            targetGameClientIds = new List<int>(_gameClientIdToNetworkSourceIdMap.Keys);
            Logger.LogWarning($"[MockNetworkLayer MB S->C BROADCAST-ALL (VM not set or no relevant clients from VM)] Entity: {entityId}, Type: {messageType}. Targets: All mapped game clients.");
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
            // Logger.Log($"[MockNetworkLayer MB S->C UNICAST] To GameClientID={gameClientId} (NetworkSourceID={targetNetworkSourceId}), Entity: {entityId}, Type: {messageType}");
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