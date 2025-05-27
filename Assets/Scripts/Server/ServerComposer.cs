// File: Scripts/Server/ServerComposer.cs
using UnityEngine;
using Core;
using Core.Network;
using Core.Visibility;
using ServerSpecific.Debug; 
using ServerSpecific.Session; 
using Core.Logging;
using Core.Time; 
using System;
using System.Linq;
using Core.Session; 
using Core.Ocean; 
using Logger = Core.Logging.Logger;


public class ServerComposer : MonoBehaviour
{
    [Header("Core Components")]
    private Core.CoreComposer _coreComposer;

    [Header("Network Layer (Shared)")]
    [SerializeField]
    [Tooltip("Assign the MockNetworkLayer GameObject/Component from the scene here.")]
    private MockNetworkLayer mockNetworkLayer;

    [Header("Ocean Settings Provider")]
    [SerializeField]
    [Tooltip("Assign the OceanSettingsProvider component that configures ocean parameters and texture data.")]
    private OceanSettingsProvider oceanSettingsProvider;

    [Header("Ocean Debug Visualizer (Server)")]
    [SerializeField]
    [Tooltip("Optional: Assign an OceanDebugVisualizer instance from the scene for server-side visualization. If not assigned, it will try to find one on this GameObject or its children, or on the OceanSettingsProvider's GameObject or its children.")]
    private OceanDebugVisualizer serverOceanVisualizer;


    private IVisibilityStrategy _visibilityStrategy;
    private IClock _serverClock;
    private IClientConnectionValidator _connectionValidator;
    private IOceanDataProvider _serverOceanDataProvider;

    [Header("Entity Debug Presentation")]
    [SerializeField] private DebugPresentationManager entityDebugPresentationManager;

    [Header("Client Debug Presentation")]
    [SerializeField] private ClientDebugPresentationManager clientDebugPresentationManager;

    [Header("Debug Info")]
    [SerializeField, ReadOnly]
    private float currentTime_Display;

    private Core.Model.DebugEntity lastCreatedDebugEntity;


    void Awake()
    {
        Logger.Log("[ServerComposer MB] Awake: Initializing Server...");

#if UNITY_SERVER || UNITY_EDITOR
        _serverClock = new UnityClock();
        Logger.Log("[ServerComposer MB] Using UnityClock for server time.");
#else
        _serverClock = new SystemClock();
        Logger.Log("[ServerComposer MB] Using SystemClock for server time.");
#endif


        if (mockNetworkLayer == null)
        {
            Logger.LogError("[ServerComposer MB] MockNetworkLayer not assigned! Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null)
            {
                Logger.LogError("[ServerComposer MB] MockNetworkLayer not found. Aborting server setup.");
                enabled = false;
                return;
            }
            Logger.LogWarning("[ServerComposer MB] MockNetworkLayer found in scene. Assign in Inspector for robustness.");
        }

        _visibilityStrategy = new DummyVisibilityStrategy();
        _connectionValidator = new MockClientConnectionValidator();

        if (oceanSettingsProvider == null)
        {
            Logger.LogError("[ServerComposer MB] OceanSettingsProvider not assigned! Ocean data will not be available. Attempting to find in scene.");
            oceanSettingsProvider = FindObjectOfType<OceanSettingsProvider>();
            if (oceanSettingsProvider == null)
            {
                Logger.LogError("[ServerComposer MB] OceanSettingsProvider not found in scene. Server-side ocean will be disabled.");
            }
        }

        if (oceanSettingsProvider != null)
        {
            OceanSettings settings = oceanSettingsProvider.GetSettings();
            byte[] loadedOceanBytes = oceanSettingsProvider.GetOceanTextureBytes();
            _serverOceanDataProvider = new ServerOceanDataProvider(settings, loadedOceanBytes);
            Logger.Log("[ServerComposer MB] ServerOceanDataProvider initialized using OceanSettingsProvider.");
        }
        else
        {
            _serverOceanDataProvider = null;
            Logger.LogWarning("[ServerComposer MB] ServerOceanDataProvider is null due to missing OceanSettingsProvider.");
        }

        // Initialize Server Ocean Visualizer
        if (serverOceanVisualizer == null) // If not assigned in inspector
        {
            serverOceanVisualizer = GetComponentInChildren<OceanDebugVisualizer>();
            if (serverOceanVisualizer == null && oceanSettingsProvider != null)
            {
                serverOceanVisualizer = oceanSettingsProvider.GetComponentInChildren<OceanDebugVisualizer>();
            }
        }

        if (serverOceanVisualizer != null)
        {
            if (_serverOceanDataProvider != null && _serverClock != null)
            {
                // If visualizer is a prefab reference in the inspector field, instantiate it
                if (serverOceanVisualizer.gameObject.scene.name == null)
                {
                    Transform parentTransform = (oceanSettingsProvider != null) ? oceanSettingsProvider.transform : transform;
                    serverOceanVisualizer = Instantiate(serverOceanVisualizer, parentTransform.position, Quaternion.identity, parentTransform);
                    serverOceanVisualizer.name = "ServerOceanDebugVisualizer_Instance";
                }
                serverOceanVisualizer.Initialize(_serverOceanDataProvider, _serverClock);
                Logger.Log("[ServerComposer MB] ServerOceanVisualizer initialized.");
            }
            else
            {
                Logger.LogWarning($"[ServerComposer MB] ServerOceanVisualizer found/assigned, but cannot initialize due to missing OceanDataProvider or Clock.");
            }
        }
        else
        {
            Logger.LogWarning("[ServerComposer MB] ServerOceanVisualizer not assigned or found. No server-side ocean debug visualization.");
        }


        try
        {
            _coreComposer = new Core.CoreComposer(
                mockNetworkLayer,
                _visibilityStrategy,
                _serverClock,
                _connectionValidator,
                _serverOceanDataProvider
            );
            mockNetworkLayer.SetVisibilityManager(_coreComposer.VisibilityManager);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ServerComposer MB] CRITICAL ERROR during CoreComposer initialization: {ex.Message}\nStackTrace: {ex.StackTrace}");
            enabled = false;
            return;
        }

        if (entityDebugPresentationManager == null) entityDebugPresentationManager = GetComponent<DebugPresentationManager>();
        if (entityDebugPresentationManager != null && _coreComposer != null)
        {
            entityDebugPresentationManager.Initialize(_coreComposer.ServerLevel);
        }
        else if (entityDebugPresentationManager == null)
        {
            Logger.LogWarning("[ServerComposer MB] Entity DebugPresentationManager not assigned/found.");
        }

        if (clientDebugPresentationManager == null) clientDebugPresentationManager = GetComponent<ClientDebugPresentationManager>();
        if (clientDebugPresentationManager != null && _coreComposer != null)
        {
            clientDebugPresentationManager.Initialize(_coreComposer, entityDebugPresentationManager);
        }
        else if (clientDebugPresentationManager == null)
        {
            Logger.LogWarning("[ServerComposer MB] ClientDebugPresentationManager not assigned/found.");
        }

        Logger.Log("[ServerComposer MB] Initialization complete. Server is ready to accept client connections.");
    }

    void Update()
    {
        if (_serverClock != null)
        {
            currentTime_Display = _serverClock.CurrentTime;
        }

        if (_coreComposer != null && enabled)
        {
            _coreComposer.Update(Time.deltaTime);
        }
    }
    void OnDestroy()
    {
        Logger.Log("[ServerComposer MB] OnDestroy: Cleaning up...");
        _coreComposer?.Dispose();
        _coreComposer = null;
        _serverClock = null;
        (_serverOceanDataProvider as IDisposable)?.Dispose();
        _serverOceanDataProvider = null;

        (_visibilityStrategy as IDisposable)?.Dispose();
        _visibilityStrategy = null;

        if (serverOceanVisualizer != null && serverOceanVisualizer.gameObject.scene.name != null && serverOceanVisualizer.transform.parent == ((oceanSettingsProvider != null) ? oceanSettingsProvider.transform : transform) && serverOceanVisualizer.name.EndsWith("_Instance"))
        {
            // If it was instantiated as a child by this script, destroy it
            Destroy(serverOceanVisualizer.gameObject);
        }
        serverOceanVisualizer = null;


        Logger.Log("[ServerComposer MB] Cleanup complete.");
    }

    [ContextMenu("1. Create Debug Entity (At Origin)")]
    public void CreateDebugEntityOrigin() { CreateDebugEntityAt(Core.Primitives.Vector3.Zero); }

    [ContextMenu("1b. Create Debug Entity (Far Away)")]
    public void CreateDebugEntityFar() { CreateDebugEntityAt(new Core.Primitives.Vector3(100f, 0f, 100f)); }

    private void CreateDebugEntityAt(Core.Primitives.Vector3 position)
    {

    }

    [ContextMenu("2. Simulate Client Sync (Correct Checksum - needs Client running)")]
    public void SimulateClientSyncCorrect()
    {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        Logger.Log($"[ServerComposer MB Action] Simulating Client Sync (Correct) from NetworkSourceID: {mockNetworkLayer.defaultSendingClientId} for Entity: {lastCreatedDebugEntity.Id}");

        int hash = HashCode.Combine(lastCreatedDebugEntity.Position.GetHashCode(),
                                    lastCreatedDebugEntity.Rotation.GetHashCode(),
                                    lastCreatedDebugEntity.Hydration.GetHashCode(),
                                    lastCreatedDebugEntity.Guilt.GetHashCode());
        float checksum = (float)hash;

        clientNetwork.SendToServer(
            mockNetworkLayer.defaultSendingClientId,
            lastCreatedDebugEntity.Id,
            MessageType._ClientSyncState,
            writer => writer.Write(checksum)
        );
    }

    [ContextMenu("3. Simulate Client Sync (Incorrect Checksum - needs Client running)")]
    public void SimulateClientSyncIncorrect()
    {
        if (lastCreatedDebugEntity == null || lastCreatedDebugEntity.IsDead) { Logger.LogWarning("No active DebugEntity to sync with."); return; }
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        Logger.Log($"[ServerComposer MB Action] Simulating Client Sync (Incorrect) from NetworkSourceID: {mockNetworkLayer.defaultSendingClientId} for Entity: {lastCreatedDebugEntity.Id}");
        float incorrectChecksum = 9876.54f;

        clientNetwork.SendToServer(
            mockNetworkLayer.defaultSendingClientId,
            lastCreatedDebugEntity.Id,
            MessageType._ClientSyncState,
            writer => writer.Write(incorrectChecksum)
        );
    }

    [ContextMenu("4. Kill Last Debug Entity (Loud)")]
    public void KillLastDebugEntityLoud()
    {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead)
        {
            lastCreatedDebugEntity.Kill(false);
        }
        else
        {
            Logger.LogWarning("[ServerComposer MB Action] No living DebugEntity tracked to kill.");
        }
    }
    [ContextMenu("4b. Kill Last Debug Entity (Silent)")]
    public void KillLastDebugEntitySilent()
    {
        if (lastCreatedDebugEntity != null && !lastCreatedDebugEntity.IsDead)
        {
            lastCreatedDebugEntity.Kill(true);
        }
        else
        {
            Logger.LogWarning("[ServerComposer MB Action] No living DebugEntity tracked to kill.");
        }
    }

    [ContextMenu("5. Test Escadre Command (_SetCourse - needs Client running & connected)")]
    public void TestEscadreSetCourse()
    {
        if (mockNetworkLayer == null) { Logger.LogWarning("MockNetworkLayer not available."); return; }
        if (_coreComposer == null || !_coreComposer.ClientConnections.Any()) { Logger.LogWarning("No clients connected to send command for."); return; }

        var clientNetwork = (IClientNetworkLayer)mockNetworkLayer;
        Core.Primitives.Vector2 newDest = new Core.Primitives.Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
        Logger.Log($"[ServerComposer MB Action] Simulating C->S _SetCourse to {newDest} from NetworkSourceID: {mockNetworkLayer.defaultSendingClientId}");

        clientNetwork.SendToServer(
            mockNetworkLayer.defaultSendingClientId,
            0,
            MessageType._SetCourse,
            writer => Core.Network.Proxies.SerializationUtils.WriteVector2(writer, newDest)
        );
    }

    [ContextMenu("DEBUG: Force Unregister First Connected Client (if any)")]
    public void DebugUnregisterFirstClient()
    {
        if (_coreComposer == null || !_coreComposer.ClientConnections.Any()) { Logger.LogWarning("CoreComposer not initialized or no clients to unregister."); return; }
        int clientIdToUnregister = _coreComposer.ClientConnections.Keys.First();
        Logger.Log($"[ServerComposer MB Action] Forcibly unregistering client with game ID {clientIdToUnregister}.");
        _coreComposer.UnregisterClient(clientIdToUnregister);
    }
    
    // In ServerComposer.cs, add this method:
    [ContextMenu("Create ResourceBox (At Origin)")]
    public void CreateResourceBoxOrigin() {
        if (_coreComposer == null || _coreComposer.ServerLevel == null) {
            Logger.LogWarning("[ServerComposer] Core or Level not initialized. Cannot create ResourceBox.");
            return;
        }
        var box = new Core.Model.ResourceBox(_coreComposer.ServerLevel, Core.Primitives.Vector3.Zero, 100); // Spawns a box with 100 resources
        Logger.Log($"[ServerComposer MB Action] Created ResourceBox Entity ID: {box.Id} at origin.");
    }
}