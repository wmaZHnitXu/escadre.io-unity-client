// File: Scripts/Client/Presentation/ClientPresentationManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Client;     // For ClientLevel
using Core.Network;     // For IClientProxy
using Core.Model;       // For Entity.EntityTypeEnum
using Core.Logging;
using Logger = Core.Logging.Logger;
using System.Linq;

public class ClientPresentationManager : MonoBehaviour
{
    [Serializable]
    public struct ProxyFactoryEntry
    {
        public Entity.EntityTypeEnum EntityType;
        public GameObject PresentationPrefab;
        public ClientProxyPresentationFactory Factory;
    }

    [Header("Configuration")]
    [SerializeField]
    private List<ProxyFactoryEntry> proxyFactoryEntries = new List<ProxyFactoryEntry>();

    [Tooltip("Optional parent for instantiated presentation GameObjects.")]
    [SerializeField] private Transform presentationParent;


    private ClientLevel _clientLevel;
    private Dictionary<Entity.EntityTypeEnum, ProxyFactoryEntry> _entryLookup = new Dictionary<Entity.EntityTypeEnum, ProxyFactoryEntry>();
    private Dictionary<int, ClientProxyPresentation> _activePresentations = new Dictionary<int, ClientProxyPresentation>(); // To track active presentations
    private bool _isInitialized = false;

    public void Initialize(ClientLevel clientLevel)
    {
        if (_isInitialized)
        {
            Logger.LogWarning("[ClientPresentationManager] Already initialized.");
            return;
        }
        _clientLevel = clientLevel ?? throw new ArgumentNullException(nameof(clientLevel));

        _entryLookup.Clear();
        foreach (var entry in proxyFactoryEntries)
        {
            if (entry.PresentationPrefab == null)
            {
                Logger.LogError($"[ClientPresentationManager] PresentationPrefab not assigned for EntityType: {entry.EntityType}. This type will not be visualized.");
                continue;
            }
            if (entry.Factory == null)
            {
                Logger.LogError($"[ClientPresentationManager] Factory not assigned for EntityType: {entry.EntityType}. This type will not be visualized.");
                continue;
            }
            if (!_entryLookup.ContainsKey(entry.EntityType))
            {
                _entryLookup.Add(entry.EntityType, entry);
            }
            else
            {
                Logger.LogWarning($"[ClientPresentationManager] Duplicate EntityType mapping found for: {entry.EntityType}. Using first entry.");
            }
        }

        _clientLevel.OnProxyAdded += HandleProxyAddedToClientLevel;
        _clientLevel.OnProxyRemoved += HandleProxyRemovedFromClientLevel;

        foreach (var proxy in _clientLevel.GetAllProxies())
        {
            HandleProxyAddedToClientLevel(proxy);
        }

        _isInitialized = true;
        Logger.Log("[ClientPresentationManager] Initialized and subscribed to ClientLevel events.");
    }

    private void HandleProxyAddedToClientLevel(IClientProxy proxy)
    {
        if (!_isInitialized || proxy == null) return;
        if (_activePresentations.ContainsKey(proxy.EntityId)) 
        {
            Logger.LogWarning($"[ClientPresentationManager] Proxy Added (ID: {proxy.EntityId}), but a presentation already exists. This might indicate a re-creation or an issue.");
            return;
        }


        if (_entryLookup.TryGetValue(proxy.EntityType, out ProxyFactoryEntry factoryEntry))
        {
            Logger.Log($"[ClientPresentationManager] Proxy Added (ID: {proxy.EntityId}, Type: {proxy.EntityType}). Allocating presentation.");
            ClientProxyPresentation presentation = factoryEntry.Factory.AllocatePresentation(proxy, factoryEntry.PresentationPrefab);
            if (presentation != null)
            {
                if (presentationParent != null)
                {
                    presentation.transform.SetParent(presentationParent, false);
                }
                _activePresentations[proxy.EntityId] = presentation; 
                presentation.PresentationDisposedEvent += HandlePresentationDisposed; 
            }
            else
            {
                Logger.LogError($"[ClientPresentationManager] Factory failed to allocate presentation for proxy ID {proxy.EntityId}, Type {proxy.EntityType}.");
            }
        }
        else
        {
            Logger.LogWarning($"[ClientPresentationManager] No presentation factory mapping found for EntityType: {proxy.EntityType} (ID: {proxy.EntityId}). Cannot create visual representation.");
        }
    }

    private void HandleProxyRemovedFromClientLevel(IClientProxy proxy)
    {
        if (!_isInitialized || proxy == null) return;
        // The presentation will be removed from _activePresentations when its PresentationDisposedEvent fires,
        // which is triggered by the proxy's OnDestroyed event (handled in ClientProxyPresentation.HandleProxyDestroyed).
    }
    
    private void HandlePresentationDisposed(ClientProxyPresentation presentation)
    {
        if (presentation == null || presentation.TargetProxy == null) return;

        if (_activePresentations.ContainsKey(presentation.TargetProxy.EntityId))
        {
            _activePresentations.Remove(presentation.TargetProxy.EntityId);
            // Logger.Log($"[ClientPresentationManager] Presentation for Proxy ID {presentation.TargetProxy.EntityId} was disposed and removed from tracking.");
        }
        presentation.PresentationDisposedEvent -= HandlePresentationDisposed;
    }

    private void OnDestroy()
    {
        Logger.Log("[ClientPresentationManager] OnDestroy called. Cleaning up...");
        if (_clientLevel != null)
        {
            _clientLevel.OnProxyAdded -= HandleProxyAddedToClientLevel;
            _clientLevel.OnProxyRemoved -= HandleProxyRemovedFromClientLevel;
        }
        
        foreach(var presentation in _activePresentations.Values.ToList()) 
        {
            if(presentation != null)
            {
                // Directly calling DisposePresentation ensures cleanup logic in ClientProxyPresentation runs.
                // If it's pooled, it will go back to the pool. If not, it should destroy itself.
                presentation.SendMessage("DisposePresentation", SendMessageOptions.DontRequireReceiver); // Call DisposePresentation
                if (presentation.gameObject != null) Destroy(presentation.gameObject); // Fallback destroy if not pooled/self-destroyed
            }
        }
        _activePresentations.Clear();
        _entryLookup.Clear();
        _isInitialized = false;
        Logger.Log("[ClientPresentationManager] Cleanup complete.");
    }
}