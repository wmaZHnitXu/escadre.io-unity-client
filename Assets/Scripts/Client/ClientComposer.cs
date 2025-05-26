// File: Scripts/Client/ClientComposer.cs
using UnityEngine;
using Core.Network;
using Core.Client; 
using Core.Logging;
using Core.Time; 
using Logger = Core.Logging.Logger;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Core.Primitives;
using System;
using Vector2 = Core.Primitives.Vector2;
using Core.Model; 
using Core.Network.Proxies; 
using Core.Ocean; 

public class ClientComposer : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private MockNetworkLayer mockNetworkLayer;
    [SerializeField]
    private int thisClientInstanceId = 1; 
    [SerializeField]
    private string thisClientNickname = "Player";
     [SerializeField]
    private string thisClientAuthTypeString = "Account"; 
    [SerializeField]
    private bool thisClientIsAdmin = false;

    [Header("Ocean Settings Provider")]
    [SerializeField]
    [Tooltip("Assign the OceanSettingsProvider component that configures client-side ocean parameters and texture data.")]
    private OceanSettingsProvider oceanSettingsProvider;
    
    [Header("Ocean Debug Visualizer (Client)")]
    [SerializeField]
    [Tooltip("Optional: Assign an OceanDebugVisualizer instance for client-side visualization. If not assigned, it will try to find one on this GameObject or its children, or on the OceanSettingsProvider's GameObject or its children.")]
    private OceanDebugVisualizer clientOceanVisualizer;

    [Header("Ocean Presentation (Client)")]
    [SerializeField]
    [Tooltip("Assign an OceanPresentation instance from the scene or a prefab to be instantiated. If prefab, ensure it's configured correctly.")]
    private OceanPresentation oceanPresentation; // Can be a scene instance or a prefab
    [SerializeField]
    [Tooltip("The material to be used by the OceanPresentation. Should use the LowPolyOceanWater shader.")]
    private Material oceanMaterial;

    [Header("Presentation")]
    [SerializeField]
    private ClientPresentationManager clientPresentationManager;

    [Header("Debug Info")]
    [SerializeField, ReadOnly] 
    private float currentTime_Display;
    [SerializeField, ReadOnly]
    private bool isConnectionAttempted = false;
    [SerializeField, ReadOnly]
    private bool isSessionFullyActive = false;
    [SerializeField, ReadOnly]
    private int localEscadreEntityId_Display = -1;
    [SerializeField, ReadOnly]
    private bool isOceanReady_Display = false; 


    private ClientLevel _clientLevel;
    private ClientEntityManager _entityManager; 
    private IClientNetworkLayer _clientNetworkAccess; 
    private IClock _clientClock; 
    private ClientGameActions _gameActions; 
    
    public EscadreProxy.ClientProxy LocalEscadreProxy { get; private set; } 

    void Awake()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Awake: Initializing Client Logic...");
        thisClientNickname = $"{thisClientNickname}_{thisClientInstanceId}"; 

        _clientClock = new UnityClock(); 

        if (oceanSettingsProvider == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] OceanSettingsProvider not assigned! Ocean data will not be available for client. Attempting to find in scene.");
            oceanSettingsProvider = FindObjectOfType<OceanSettingsProvider>();
            if (oceanSettingsProvider == null)
            {
                 Logger.LogError($"[ClientComposer {thisClientInstanceId}] OceanSettingsProvider not found in scene. Client-side ocean will be disabled until server sends settings.");
            }
        }
        
        // In ClientComposer.cs, replace the material setup section in Awake() with this:

        if (oceanMaterial == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] Ocean material not assigned! Please assign a material that uses the 'Custom/LowPolyOceanWater' shader in the inspector.");
        }


        if (mockNetworkLayer == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer not assigned! Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null) { Logger.LogError($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer not found. Client cannot function."); enabled = false; return; }
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer found in scene. Assign in Inspector for robustness.");
        }
        _clientNetworkAccess = mockNetworkLayer;
        mockNetworkLayer.defaultSendingClientId = thisClientInstanceId; 

        _clientLevel = new ClientLevel(_clientClock); 
        _clientLevel.OnOceanSettingsReceived += HandleOceanSettingsReceived; 

        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel, _clientClock); 
        _gameActions = new ClientGameActions(_clientNetworkAccess, thisClientInstanceId); 

        _clientLevel.OnProxyAdded += TryFindAndAssignLocalEscadreProxy;
        _clientLevel.OnProxyRemoved += HandleLocalEscadreProxyRemoval;
        
        SetupOceanDebugVisualizer();
        SetupOceanPresentation();

        if (clientPresentationManager == null) clientPresentationManager = GetComponent<ClientPresentationManager>();
        if (clientPresentationManager != null) clientPresentationManager.Initialize(_clientLevel); 
        
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Initialization complete. Will attempt connection in OnEnable.");
    }

    private void SetupOceanDebugVisualizer()
    {
        if (clientOceanVisualizer == null) 
        {
            clientOceanVisualizer = GetComponentInChildren<OceanDebugVisualizer>();
             if (clientOceanVisualizer == null && oceanSettingsProvider != null)
            {
                 clientOceanVisualizer = oceanSettingsProvider.GetComponentInChildren<OceanDebugVisualizer>();
            }
        }

        if (clientOceanVisualizer != null)
        {
            if (clientOceanVisualizer.gameObject.scene.name == null) 
            {
                Transform parentTransform = (oceanSettingsProvider != null) ? oceanSettingsProvider.transform : transform;
                clientOceanVisualizer = Instantiate(clientOceanVisualizer, parentTransform.position, UnityEngine.Quaternion.identity, parentTransform);
                clientOceanVisualizer.name = "ClientOceanDebugVisualizer_Instance";
            }
            Logger.Log($"[ClientComposer {thisClientInstanceId}] ClientOceanVisualizer is set up, waiting for ocean data to fully initialize.");
        }
        else
        {
             Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] ClientOceanVisualizer not assigned or found. No client-side ocean debug visualization.");
        }
    }

    private void SetupOceanPresentation()
    {
        if (oceanPresentation == null) // If not assigned, try to find or create
        {
            oceanPresentation = FindObjectOfType<OceanPresentation>();
            if (oceanPresentation == null)
            {
                Logger.Log($"[ClientComposer {thisClientInstanceId}] OceanPresentation not found in scene. Attempting to create from default settings.");
                GameObject opGO = new GameObject("OceanPresentation_Instance");
                opGO.transform.SetParent((oceanSettingsProvider != null) ? oceanSettingsProvider.transform : transform, false);
                oceanPresentation = opGO.AddComponent<OceanPresentation>();
            }
        }
        else if (oceanPresentation.gameObject.scene.name == null) // It's a prefab assigned in inspector
        {
            Transform parentTransform = (oceanSettingsProvider != null) ? oceanSettingsProvider.transform : this.transform;
            oceanPresentation = Instantiate(oceanPresentation, parentTransform.position, UnityEngine.Quaternion.identity, parentTransform);
            oceanPresentation.name = "OceanPresentation_InstanceFromPrefab";
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Instantiated OceanPresentation from prefab.");
        }

        if (oceanPresentation != null)
        {
             Logger.Log($"[ClientComposer {thisClientInstanceId}] OceanPresentation is set up, waiting for ocean data to fully initialize.");
        }
        else
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] OceanPresentation could not be set up. Visual ocean will not be rendered.");
        }
    }

    private void HandleOceanSettingsReceived(OceanSettings settings)
    {
        if (settings == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] Received null OceanSettings. Cannot initialize client ocean.");
            return;
        }
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Received OceanSettings from server. Initializing client-side ocean provider.");

        byte[] textureBytesToUse = null;
        if (oceanSettingsProvider != null)
        {
            textureBytesToUse = oceanSettingsProvider.GetOceanTextureBytes();
            if (textureBytesToUse == null)
            {
                Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanSettingsProvider did not provide texture bytes. ClientTextureBasedOceanDataProvider will use dummy data.");
            }
        }
        else
        {
             Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No OceanSettingsProvider available to get local texture bytes. ClientTextureBasedOceanDataProvider will use dummy data.");
        }

        var clientOceanProvider = new ClientTextureBasedOceanDataProvider(settings, textureBytesToUse);
        _clientLevel.InitializeOcean(clientOceanProvider);
        isOceanReady_Display = _clientLevel.IsOceanInitialized; 
        
        if (clientOceanVisualizer != null && _clientLevel.OceanDataProvider != null)
        {
            clientOceanVisualizer.Initialize(_clientLevel.OceanDataProvider, _clientClock);
            Logger.Log($"[ClientComposer {thisClientInstanceId}] ClientOceanVisualizer fully initialized.");
        }

        if (oceanPresentation != null && _clientLevel.OceanDataProvider != null && oceanMaterial != null && textureBytesToUse != null)
        {
            oceanPresentation.Initialize(_clientLevel.OceanDataProvider, _clientClock, textureBytesToUse, oceanMaterial);
            Logger.Log($"[ClientComposer {thisClientInstanceId}] OceanPresentation fully initialized.");
        }
        else
        {
            string reason = "";
            if(oceanPresentation == null) reason += "OceanPresentation component missing. ";
            if(_clientLevel.OceanDataProvider == null) reason += "OceanDataProvider not ready. ";
            if(oceanMaterial == null) reason += "OceanMaterial missing. ";
            if(textureBytesToUse == null) reason += "TextureBytes missing. ";
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanPresentation could NOT be initialized. Reason(s): {reason}");
        }

        CheckSessionActivation(); 
    }
    
    private void TryFindAndAssignLocalEscadreProxy(IClientProxy proxy)
    {
        if (proxy.EntityType == Entity.EntityTypeEnum.Escadre && proxy is EscadreProxy.ClientProxy escadreProxy)
        {
            if (escadreProxy.OwnerClientId == thisClientInstanceId) 
            {
                if (LocalEscadreProxy != null && LocalEscadreProxy.EntityId != escadreProxy.EntityId)
                {
                    Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] New local EscadreProxy (ID: {escadreProxy.EntityId}) assigned, replacing old one (ID: {LocalEscadreProxy.EntityId}).");
                    UnsubscribeFromLocalEscadreEvents();
                }
                else if (LocalEscadreProxy != null && LocalEscadreProxy.EntityId == escadreProxy.EntityId)
                {
                     return; 
                }

                LocalEscadreProxy = escadreProxy;
                localEscadreEntityId_Display = LocalEscadreProxy.EntityId;
                Logger.Log($"[ClientComposer {thisClientInstanceId}] Local EscadreProxy found and assigned! Entity ID: {LocalEscadreProxy.EntityId}, Owner: {LocalEscadreProxy.OwnerClientId}");
                SubscribeToLocalEscadreEvents();
                CheckSessionActivation();
            }
        }
    }

    private void HandleLocalEscadreProxyRemoval(IClientProxy proxy)
    {
        if (LocalEscadreProxy != null && proxy.EntityId == LocalEscadreProxy.EntityId)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Local EscadreProxy (Entity ID: {LocalEscadreProxy.EntityId}) was removed.");
            UnsubscribeFromLocalEscadreEvents();
            LocalEscadreProxy = null;
            localEscadreEntityId_Display = -1;
            isSessionFullyActive = false; 
        }
    }

    private void SubscribeToLocalEscadreEvents()
    {
        if (LocalEscadreProxy == null) return;
        LocalEscadreProxy.OnShopDesignsChanged += HandleShopDesignsChanged_Debug;
        LocalEscadreProxy.OnFormationChanged += HandleFormationChanged_Debug;
        LocalEscadreProxy.OnResourcesChanged += HandleResourcesChanged_Debug;
        LocalEscadreProxy.OnNicknameChanged += HandleNicknameChanged_Debug;
    }

    private void UnsubscribeFromLocalEscadreEvents()
    {
        if (LocalEscadreProxy == null) return;
        LocalEscadreProxy.OnShopDesignsChanged -= HandleShopDesignsChanged_Debug;
        LocalEscadreProxy.OnFormationChanged -= HandleFormationChanged_Debug;
        LocalEscadreProxy.OnResourcesChanged -= HandleResourcesChanged_Debug;
        LocalEscadreProxy.OnNicknameChanged -= HandleNicknameChanged_Debug;
    }

    private void HandleShopDesignsChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Shop designs updated. Count: {LocalEscadreProxy.AvailableShopDesigns.Count}"); CheckSessionActivation(); }
    private void HandleFormationChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Formation updated. Slot Count: {LocalEscadreProxy.FormationSlots.Count}"); CheckSessionActivation(); }
    private void HandleResourcesChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Resources updated. Amount: {LocalEscadreProxy.Resources}"); }
    private void HandleNicknameChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Nickname updated. Value: '{LocalEscadreProxy.Nickname}'"); }

    void OnEnable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnEnable called.");
        if (!isConnectionAttempted && !isSessionFullyActive) RequestConnection();
        else if (isConnectionAttempted && !isSessionFullyActive) Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Connection previously attempted but session not fully active. Waiting for server data.");
    }

    private void RequestConnection()
    {
        if (_clientNetworkAccess == null) { Logger.LogError($"[ClientComposer {thisClientInstanceId}] Network layer unavailable for connection request."); return; }
        isConnectionAttempted = true; isSessionFullyActive = false; 
        string mockJwt = $"{thisClientInstanceId};{thisClientNickname};{thisClientIsAdmin.ToString().ToLowerInvariant()};{thisClientAuthTypeString}";
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Sending _ClientConnectRequest with Mock JWT: '{mockJwt}'");
        _clientNetworkAccess.SendToServer(thisClientInstanceId, 0, MessageType._ClientConnectRequest, writer => writer.Write(mockJwt));
    }

    private void CheckSessionActivation()
    {
        if (isSessionFullyActive) return; 

        bool escadreProxyExists = LocalEscadreProxy != null;
        bool shopInfoExists = escadreProxyExists && LocalEscadreProxy.AvailableShopDesigns.Any();
        
        if (escadreProxyExists && shopInfoExists && _clientLevel.IsOceanInitialized) 
        {
            isSessionFullyActive = true; 
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Game session is now FULLY active (local EscadreProxy initialized with shop designs and ocean data received).");
        }
    }

    void Update()
    {
        if (_clientClock != null) currentTime_Display = _clientClock.CurrentTime;
        if (_clientLevel != null) isOceanReady_Display = _clientLevel.IsOceanInitialized; 

        if (!isSessionFullyActive && isConnectionAttempted && LocalEscadreProxy == null)
        {
            if (Time.frameCount % 60 == 0) 
            {
                var foundProxy = _clientLevel.ActiveProxies.Values
                    .OfType<EscadreProxy.ClientProxy>()
                    .FirstOrDefault(ep => ep.OwnerClientId == thisClientInstanceId); 
                if (foundProxy != null)
                {
                    TryFindAndAssignLocalEscadreProxy(foundProxy);
                }
            }
        }
        
        if (!isSessionFullyActive && isConnectionAttempted)
        {
            CheckSessionActivation(); 

            if (!isSessionFullyActive && Time.frameCount > 60 && Time.frameCount % 120 == 0) 
            {
                string reasons = "";
                if (LocalEscadreProxy == null) reasons += "Waiting for local Escadre proxy. ";
                else {
                    if (!LocalEscadreProxy.AvailableShopDesigns.Any()) reasons += "Waiting for shop designs info on Escadre proxy. ";
                }
                if (!_clientLevel.IsOceanInitialized) reasons += "Waiting for ocean initialization data. ";
                Logger.Log($"[ClientComposer {thisClientInstanceId}] Session not fully active yet. Reason(s): {reasons}");
            }
        }

        if (_clientLevel != null) _clientLevel.DoUpdate(Time.deltaTime); 
    }

    void OnDisable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDisable called.");
        if (_clientNetworkAccess != null && isConnectionAttempted)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Sending _ClientDisconnect message.");
            _clientNetworkAccess.SendToServer(thisClientInstanceId, 0, MessageType._ClientDisconnect, writer => {});
        }
        isSessionFullyActive = false; isConnectionAttempted = false; 
    }

    [ContextMenu("Shop: Buy DefaultShip (Slot near last)")]
    public void MockBuyDefaultShip()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing. Cannot buy ship."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }
        
        var design = LocalEscadreProxy.AvailableShopDesigns.FirstOrDefault(d => d.ShipEntityType == Entity.EntityTypeEnum.DefaultShip);
        if (design == null)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] DefaultShip design not found in shop info. Available: {string.Join(", ", LocalEscadreProxy.AvailableShopDesigns.Select(x => x.Name))}");
            return;
        }
        
        float newX = 0;
        float newY = LocalEscadreProxy.FormationSlots.Count * 2.5f; 
        _gameActions.RequestBuyShip(design.DesignId, new Vector2(newX, newY)); 
    }

    [ContextMenu("Shop: Upgrade First Ship")]
    public void MockUpgradeFirstShip()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing. Cannot upgrade ship."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }
        
        var firstShipSlot = LocalEscadreProxy.FormationSlots.FirstOrDefault(s => s.ShipEntityId.HasValue);
        if (firstShipSlot == null || !firstShipSlot.ShipEntityId.HasValue) 
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ship with an ID found in the current formation slots to upgrade.");
            return;
        }
        _gameActions.RequestUpgradeShip(firstShipSlot.ShipEntityId.Value);
    }

    [ContextMenu("Formation: Set Random Valid Formation")]
    public void MockSetRandomFormation()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing. Cannot set formation."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }

        var shipsInFormation = LocalEscadreProxy.FormationSlots.Where(s => s.ShipEntityId.HasValue).ToList();
        if (!shipsInFormation.Any()) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ships in formation to rearrange."); return; }
        
        var newLayout = new List<Tuple<int, Vector2>>();
        float angleStep = 360f / shipsInFormation.Count;
        float radius = 3f + (shipsInFormation.Count * 0.5f); 

        for(int i=0; i < shipsInFormation.Count; i++)
        {
            var slot = shipsInFormation[i];
            float angle = i * angleStep * Mathf.Deg2Rad;
            newLayout.Add(new Tuple<int, Vector2>(slot.ShipEntityId.Value, new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius)));
        }

        if (newLayout.Any()) _gameActions.RequestSetFormation(newLayout);
        else Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ships with IDs found in local formation state to set.");
    }
    
    [ContextMenu("Command: Attack First Other Escadre")]
    public void MockAttackFirstOtherEscadre()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }

        EscadreProxy.ClientProxy targetEscadre = _clientLevel.ActiveProxies.Values
            .OfType<EscadreProxy.ClientProxy>()
            .FirstOrDefault(ep => ep.EntityId != LocalEscadreProxy.EntityId && ep.OwnerClientId != thisClientInstanceId);

        if (targetEscadre != null)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Ordering attack on Escadre Entity ID: {targetEscadre.EntityId} (Owner: {targetEscadre.OwnerClientId})");
            _gameActions.SendAttackEscadre(targetEscadre.EntityId);
        }
        else
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No other escadres found to attack.");
        }
    }

    void OnDestroy()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDestroy: Cleaning up...");
        if (_clientLevel != null)
        {
            _clientLevel.OnProxyAdded -= TryFindAndAssignLocalEscadreProxy;
            _clientLevel.OnProxyRemoved -= HandleLocalEscadreProxyRemoval;
            _clientLevel.OnOceanSettingsReceived -= HandleOceanSettingsReceived; 
        }
        UnsubscribeFromLocalEscadreEvents();
        LocalEscadreProxy = null; 

        _entityManager?.Dispose(); 
        _entityManager = null;
        _gameActions = null;
        _clientClock = null; 
        
        if (clientOceanVisualizer != null && clientOceanVisualizer.gameObject.scene.name != null && clientOceanVisualizer.name.EndsWith("_Instance"))
        {
            Destroy(clientOceanVisualizer.gameObject);
        }
        clientOceanVisualizer = null;

        if (oceanPresentation != null && oceanPresentation.gameObject.scene.name != null && (oceanPresentation.name.EndsWith("_Instance") || oceanPresentation.name.EndsWith("_InstanceFromPrefab")))
        {
            Destroy(oceanPresentation.gameObject);
        }
        oceanPresentation = null;

        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Cleanup complete.");
    }
}
