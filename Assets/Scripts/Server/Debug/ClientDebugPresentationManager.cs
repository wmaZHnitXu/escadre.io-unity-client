// File: Scripts/Server/Debug/ClientDebugPresentationManager.cs
using UnityEngine;
using System.Collections.Generic;
using Core; // For CoreComposer
using Core.Session;
using Core.Visibility;
using ServerSpecific.Debug;
using Logger = Core.Logging.Logger;

public class ClientDebugPresentationManager : MonoBehaviour
{
    [SerializeField] private GameObject clientConnectionDebugPrefab;
    [SerializeField] private Transform clientDebugObjectParent;

    private CoreComposer _coreComposerRef; // Now stores CoreComposer
    private VisibilityManager _visibilityManagerRef;
    private DebugPresentationManager _entityDebugManagerRef;

    private Dictionary<int, ClientConnectionDebugBehaviour> _activeClientDebugObjects = new Dictionary<int, ClientConnectionDebugBehaviour>();
    private bool _isInitialized = false;

    // Initialize now takes CoreComposer
    public void Initialize(CoreComposer coreComposer, DebugPresentationManager entityDebugManager)
    {
        if (_isInitialized) { Logger.LogWarning("[ClientDebugPresentationManager] Already initialized."); return; }
        if (clientConnectionDebugPrefab == null) { Logger.LogError("[ClientDebugPresentationManager] Prefab not assigned!"); enabled = false; return; }
        if (clientConnectionDebugPrefab.GetComponent<ClientConnectionDebugBehaviour>() == null) { Logger.LogError("[ClientDebugPresentationManager] Prefab missing ClientConnectionDebugBehaviour script!"); enabled = false; return; }

        _coreComposerRef = coreComposer ?? throw new System.ArgumentNullException(nameof(coreComposer));
        _visibilityManagerRef = _coreComposerRef.VisibilityManager ?? throw new System.NullReferenceException("VisibilityManager not found in CoreComposer.");
        _entityDebugManagerRef = entityDebugManager ?? throw new System.ArgumentNullException(nameof(entityDebugManager));


        _coreComposerRef.ClientRegisteredEvent += HandleClientRegistered;
        _coreComposerRef.ClientUnregisteredEvent += HandleClientUnregistered;

        foreach (var clientConn in _coreComposerRef.ClientConnections.Values) { HandleClientRegistered(clientConn); }
        _isInitialized = true;
        Logger.Log("[ClientDebugPresentationManager] Initialized.");
    }

    private void HandleClientRegistered(ClientConnection connection)
    {
        if (!_isInitialized || connection == null || _activeClientDebugObjects.ContainsKey(connection.ClientId)) return;

        Logger.Log($"[ClientDebugPresentationManager] Client Registered: {connection.ClientId}. Creating debug representation.");
        Transform parentToUse = clientDebugObjectParent != null ? clientDebugObjectParent : this.transform;
        GameObject newDebugInstance = Instantiate(clientConnectionDebugPrefab, parentToUse);
        ClientConnectionDebugBehaviour debugScript = newDebugInstance.GetComponent<ClientConnectionDebugBehaviour>();

        // Pass CoreComposer reference along with other managers
        debugScript.Initialize(connection, _coreComposerRef, _visibilityManagerRef, _entityDebugManagerRef);
        _activeClientDebugObjects.Add(connection.ClientId, debugScript);
    }

    private void HandleClientUnregistered(ClientConnection connection)
    {
        if (!_isInitialized || connection == null) return;
        if (_activeClientDebugObjects.TryGetValue(connection.ClientId, out ClientConnectionDebugBehaviour debugScript))
        {
            if (debugScript != null && debugScript.gameObject != null) Destroy(debugScript.gameObject);
            _activeClientDebugObjects.Remove(connection.ClientId);
        }
    }

    private void OnDestroy()
    {
        if (_coreComposerRef != null)
        {
            _coreComposerRef.ClientRegisteredEvent -= HandleClientRegistered;
            _coreComposerRef.ClientUnregisteredEvent -= HandleClientUnregistered;
        }
        foreach (var debugScript in _activeClientDebugObjects.Values)
        {
            if (debugScript != null && debugScript.gameObject != null) Destroy(debugScript.gameObject);
        }
        _activeClientDebugObjects.Clear();
        _isInitialized = false;
    }
}