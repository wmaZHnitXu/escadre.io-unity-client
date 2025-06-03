// File: Scripts/Client/ClientComposer.cs
using UnityEngine; // Fully qualify: UnityEngine.Vector3, Quaternion, Camera, Color etc.
using Core.Network;
using Core.Client;
using Core.Logging;
using Core.Time;
using Logger = Core.Logging.Logger; // Alias
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Core.Primitives; // For Core.Primitives.Vector2
using System;
// Using Core.Primitives.Vector2 directly, no need for `using Vector2 = Core.Primitives.Vector2;`

using Core.Model; // For Entity.EntityTypeEnum
using Core.Network.Proxies; // For EscadreProxy
using Core.Ocean;
using Client.Camera; // For TopDownCameraController
using Client.InputServices; // For PlayerInputController
using Client.Presentation;  // For CommandVisualizer

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
    private OceanSettingsProvider oceanSettingsProvider;

    [Header("Ocean Debug Visualizer (Client)")]
    [SerializeField]
    private OceanDebugVisualizer clientOceanVisualizer;

    [Header("Ocean Presentation (Client)")]
    [SerializeField]
    private OceanPresentation oceanPresentation; // Scene reference or prefab
    [SerializeField]
    private Material oceanMaterial;

    [Header("Presentation Managers & Visualizers")]
    [SerializeField]
    private ClientPresentationManager clientPresentationManager;
    [SerializeField]
    private CommandVisualizer commandVisualizer;  // Scene reference

    [Header("Input")]
    [SerializeField]
    private PlayerInputController playerInputController; // Scene reference

    [Header("Camera Control")]
    [SerializeField]
    private TopDownCameraController topDownCameraController;
    [SerializeField]
    private UnityEngine.Camera mainGameCamera;


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
    [SerializeField, ReadOnly]
    private string localEscadreDest_Display = "N/A";
    [SerializeField, ReadOnly]
    private string localEscadreAttackTargets_Display = "N/A";


    private ClientLevel _clientLevel;
    public ClientLevel ClientLevel => _clientLevel;

    private ClientEntityManager _entityManager;
    private IClientNetworkLayer _clientNetworkAccess;
    private IClock _clientClock;

    private ClientGameActions _gameActions;
    public ClientGameActions GameActions => _gameActions;

    private EscadreProxy.ClientProxy _localEscadreProxy;
    public EscadreProxy.ClientProxy LocalEscadreProxy => _localEscadreProxy;
    public event Action<EscadreProxy.ClientProxy> OnLocalEscadreProxyChanged;


    void Awake()
    {
        Application.targetFrameRate = 120;
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Awake: Initializing Client Logic...");
        thisClientNickname = $"{thisClientNickname}_{thisClientInstanceId}";

        _clientClock = new UnityClock();

        // --- Dependency Checks ---
        if (oceanSettingsProvider == null)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanSettingsProvider not assigned. Attempting to find.");
            oceanSettingsProvider = FindObjectOfType<OceanSettingsProvider>();
            if (oceanSettingsProvider == null) Logger.LogError($"[ClientComposer {thisClientInstanceId}] OceanSettingsProvider not found.");
        }
        if (oceanMaterial == null) Logger.LogError($"[ClientComposer {thisClientInstanceId}] Ocean material not assigned!");
        if (mockNetworkLayer == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer not assigned. Attempting to find.");
            mockNetworkLayer = FindObjectOfType<MockNetworkLayer>();
            if (mockNetworkLayer == null) { Logger.LogError($"[ClientComposer {thisClientInstanceId}] MockNetworkLayer not found. Client cannot function."); enabled = false; return; }
        }
        // --- End Dependency Checks ---

        _clientNetworkAccess = mockNetworkLayer;
        mockNetworkLayer.defaultSendingClientId = thisClientInstanceId;

        _clientLevel = new ClientLevel(_clientClock);
        _clientLevel.OnOceanSettingsReceived += HandleOceanSettingsReceived;

        _entityManager = new ClientEntityManager(_clientNetworkAccess, _clientLevel, _clientClock);
        _gameActions = new ClientGameActions(_clientNetworkAccess, thisClientInstanceId);

        _clientLevel.OnProxyAdded += TryFindAndAssignLocalEscadreProxy;
        _clientLevel.OnProxyRemoved += HandleLocalEscadreProxyRemoval;

        // --- Systems Initialization ---
        SetupMainCamera();
        SetupOceanDebugVisualizer();
        SetupOceanPresentation();
        SetupCameraInputStrategy();

        if (clientPresentationManager == null) clientPresentationManager = GetComponent<ClientPresentationManager>();
        if (clientPresentationManager != null) clientPresentationManager.Initialize(_clientLevel);
        else Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] ClientPresentationManager not assigned or found.");

        SetupPlayerInputController();
        SetupCommandVisualizer();
        // --- End Systems Initialization ---

        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Initialization complete. Will attempt connection in OnEnable.");
    }

    private void SetupPlayerInputController()
    {
        if (playerInputController != null)
        {
            if (mainGameCamera != null)
            {
                playerInputController.Initialize(this, mainGameCamera, oceanPresentation);
            }
            else Logger.LogError($"[ClientComposer {thisClientInstanceId}] MainGameCamera for PlayerInputController not assigned!");
        }
        else Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] PlayerInputController (scene reference) not assigned in Inspector.");
    }

    private void SetupCommandVisualizer()
    {
        if (commandVisualizer != null)
        {
            commandVisualizer.Initialize(this);
        }
        else Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] CommandVisualizer (scene reference) not assigned in Inspector.");
    }


    private void SetupMainCamera()
    {
        if (mainGameCamera == null)
        {
            if (topDownCameraController != null && topDownCameraController.mainCamera != null)
            {
                mainGameCamera = topDownCameraController.mainCamera;
                Logger.Log($"[ClientComposer {thisClientInstanceId}] MainGameCamera assigned from TopDownCameraController.");
            }
            else
            {
                mainGameCamera = UnityEngine.Camera.main;
                if (mainGameCamera != null)
                {
                    Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] MainGameCamera not assigned, falling back to UnityEngine.Camera.main.");
                }
                else
                {
                    Logger.LogError($"[ClientComposer {thisClientInstanceId}] MainGameCamera not assigned and UnityEngine.Camera.main is null! Critical for input and camera systems.");
                    return;
                }
            }
        }
        if (topDownCameraController != null && topDownCameraController.mainCamera == null && mainGameCamera != null)
        {
            topDownCameraController.mainCamera = mainGameCamera;
        }
    }


    private void SetupCameraInputStrategy()
    {
        if (topDownCameraController != null)
        {
            if (mainGameCamera == null)
            {
                Logger.LogError($"[ClientComposer {thisClientInstanceId}] MainGameCamera is not assigned. Cannot set camera input strategy.");
                return;
            }
            if (topDownCameraController.mainCamera == null) topDownCameraController.mainCamera = mainGameCamera;


            #if UNITY_STANDALONE || UNITY_EDITOR
            topDownCameraController.SetInputStrategy(new DesktopCameraInput(mainGameCamera));
            #elif UNITY_ANDROID || UNITY_IOS
            topDownCameraController.SetInputStrategy(new MobileCameraInput(mainGameCamera));
            #else
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Unknown platform for camera input. Defaulting to DesktopCameraInput.");
            topDownCameraController.SetInputStrategy(new DesktopCameraInput(mainGameCamera));
            #endif

            if (oceanPresentation != null)
            {
                topDownCameraController.SetOceanBoundary(oceanPresentation);
            }
        }
        else
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] TopDownCameraController not assigned in Inspector. Camera manual controls will not be available.");
        }
    }


    private void SetupOceanDebugVisualizer()
    {
        if (clientOceanVisualizer == null)
        {
            if (oceanSettingsProvider != null)
                clientOceanVisualizer = oceanSettingsProvider.GetComponentInChildren<OceanDebugVisualizer>();
            if(clientOceanVisualizer == null)
                clientOceanVisualizer = GetComponentInChildren<OceanDebugVisualizer>();
        }

        if (clientOceanVisualizer != null)
        {
            if (clientOceanVisualizer.gameObject.scene.name == null)
            {
                Transform parentTransform = (oceanSettingsProvider != null) ? oceanSettingsProvider.transform : transform;
                OceanDebugVisualizer prefabInstance = clientOceanVisualizer;
                clientOceanVisualizer = Instantiate(prefabInstance, parentTransform.position, UnityEngine.Quaternion.identity, parentTransform);
                clientOceanVisualizer.name = $"{prefabInstance.name}_Instance";
            }
            Logger.Log($"[ClientComposer {thisClientInstanceId}] ClientOceanVisualizer is set up, waiting for ocean data.");
        }
        else
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] ClientOceanVisualizer not assigned or found. No client-side ocean debug visualization.");
        }
    }

    private void SetupOceanPresentation()
    {
        if (oceanPresentation == null)
        {
            oceanPresentation = FindObjectOfType<OceanPresentation>();
            if (oceanPresentation != null)
            {
                Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanPresentation not assigned, found existing instance in scene: {oceanPresentation.name}.");
            }
            else
            {
                 Logger.LogError($"[ClientComposer {thisClientInstanceId}] OceanPresentation not assigned and no instance found in scene. Visual ocean cannot be initialized.");
                 return;
            }
        }
        else if (oceanPresentation.gameObject.scene.name == null)
        {
            Transform parentTransform = (oceanSettingsProvider != null) ? oceanSettingsProvider.transform : this.transform;
            OceanPresentation prefabRef = oceanPresentation;
            oceanPresentation = Instantiate(prefabRef, parentTransform.position, UnityEngine.Quaternion.identity, parentTransform);
            oceanPresentation.name = $"{prefabRef.name}_Instance";
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Instantiated OceanPresentation from prefab: {oceanPresentation.name}");
        }

        if (oceanPresentation != null)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] OceanPresentation is set up, waiting for ocean data to fully initialize.");
            if (topDownCameraController != null)
            {
                topDownCameraController.SetOceanBoundary(oceanPresentation);
            }
            // Inform TapResolverService about the ocean's Y level if it's available
            // Assuming oceanPresentation.oceanYLevel is a public property or method
            // TapResolverService.UpdateOceanPlaneHeight(oceanPresentation.oceanYLevel);
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
                Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanSettingsProvider did not provide texture bytes. ClientTextureBasedOceanDataProvider may use dummy data or fail if it requires them.");
            }
        }
        else
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No OceanSettingsProvider available to get local texture bytes.");
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
            if (oceanPresentation == null) reason += "OceanPresentation component missing/not found. ";
            if (_clientLevel.OceanDataProvider == null) reason += "OceanDataProvider not ready. ";
            if (oceanMaterial == null) reason += "OceanMaterial missing. ";
            if (textureBytesToUse == null) reason += "TextureBytes missing/not loaded for OceanPresentation. ";
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanPresentation could NOT be fully initialized. Reason(s): {reason}");
        }

        CheckSessionActivation();
    }

    private void TryFindAndAssignLocalEscadreProxy(IClientProxy proxy)
    {
        if (_localEscadreProxy != null) return;

        if (proxy.EntityType == Entity.EntityTypeEnum.Escadre && proxy is EscadreProxy.ClientProxy escadreProxy)
        {
            if (escadreProxy.OwnerClientId == thisClientInstanceId)
            {
                _localEscadreProxy = escadreProxy;
                OnLocalEscadreProxyChanged?.Invoke(_localEscadreProxy);

                localEscadreEntityId_Display = _localEscadreProxy.EntityId;
                Logger.Log($"[ClientComposer {thisClientInstanceId}] Local EscadreProxy ASSIGNED! Entity ID: {_localEscadreProxy.EntityId}, Owner: {_localEscadreProxy.OwnerClientId}");

                SubscribeToLocalEscadreEvents();
                TrySetCameraTargetToLocalEscadre();
                CheckSessionActivation();
            }
        }
    }

    private void TrySetCameraTargetToLocalEscadre()
    {
        if (LocalEscadreProxy != null && topDownCameraController != null && clientPresentationManager != null)
        {
            GameObject presentationGO = FindPresentationForProxy(LocalEscadreProxy.EntityId);
            if (presentationGO != null)
            {
                topDownCameraController.SetTarget(presentationGO.transform, true);
                Logger.Log($"[ClientComposer {thisClientInstanceId}] Camera target set to local escadre: {presentationGO.name}");
            }
            else
            {
                Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Could not find presentation GameObject for local escadre proxy ID {LocalEscadreProxy.EntityId} to set camera target. Will retry.");
            }
        }
    }

    private GameObject FindPresentationForProxy(int proxyId)
    {
        if (clientPresentationManager == null) return null;
        ClientProxyPresentation[] allPresentations = FindObjectsOfType<ClientProxyPresentation>(true);
        foreach (var presentation in allPresentations)
        {
            if (presentation.TargetProxy != null && presentation.TargetProxy.EntityId == proxyId)
            {
                return presentation.gameObject;
            }
        }
        return null;
    }


    private void HandleLocalEscadreProxyRemoval(IClientProxy proxy)
    {
        if (LocalEscadreProxy != null && proxy.EntityId == LocalEscadreProxy.EntityId)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Local EscadreProxy (Entity ID: {LocalEscadreProxy.EntityId}) was removed.");
            UnsubscribeFromLocalEscadreEvents();

            if (topDownCameraController != null)
            {
                var currentTarget = topDownCameraController.GetTargetToFollow();
                if (currentTarget != null)
                {
                    var cpp = currentTarget.GetComponent<ClientProxyPresentation>();
                    if (cpp != null && cpp.TargetProxy != null && cpp.TargetProxy.EntityId == LocalEscadreProxy.EntityId)
                    {
                        topDownCameraController.SetTarget(null);
                        Logger.Log($"[ClientComposer {thisClientInstanceId}] Camera target cleared as local escadre was removed.");
                    }
                }
            }
            _localEscadreProxy = null;
            OnLocalEscadreProxyChanged?.Invoke(null);
            localEscadreEntityId_Display = -1;
            isSessionFullyActive = false;
        }
    }

    private void SubscribeToLocalEscadreEvents()
    {
        if (LocalEscadreProxy == null) return;
        UnsubscribeFromLocalEscadreEvents();

        LocalEscadreProxy.OnShopDesignsChanged += HandleShopDesignsChanged_Debug;
        LocalEscadreProxy.OnFormationChanged += HandleFormationChanged_Debug;
        LocalEscadreProxy.OnResourcesChanged += HandleResourcesChanged_Debug;
        LocalEscadreProxy.OnNicknameChanged += HandleNicknameChanged_Debug;
        LocalEscadreProxy.OnCurrentDestinationChanged += UpdateDebugDisplay_Destination;
        LocalEscadreProxy.OnTargetEscadreEntityIdsChanged += UpdateDebugDisplay_AttackTargets;

        UpdateDebugDisplay_Destination(); // Initial update
        UpdateDebugDisplay_AttackTargets(); // Initial update
    }

    private void UnsubscribeFromLocalEscadreEvents()
    {
        if (LocalEscadreProxy == null) return;
        LocalEscadreProxy.OnShopDesignsChanged -= HandleShopDesignsChanged_Debug;
        LocalEscadreProxy.OnFormationChanged -= HandleFormationChanged_Debug;
        LocalEscadreProxy.OnResourcesChanged -= HandleResourcesChanged_Debug;
        LocalEscadreProxy.OnNicknameChanged -= HandleNicknameChanged_Debug;
        LocalEscadreProxy.OnCurrentDestinationChanged -= UpdateDebugDisplay_Destination;
        LocalEscadreProxy.OnTargetEscadreEntityIdsChanged -= UpdateDebugDisplay_AttackTargets;
    }

    private void UpdateDebugDisplay_Destination()
    {
        if (LocalEscadreProxy != null && LocalEscadreProxy.CurrentDestination.HasValue)
            localEscadreDest_Display = LocalEscadreProxy.CurrentDestination.Value.ToString();
        else
            localEscadreDest_Display = "N/A";
    }
    private void UpdateDebugDisplay_AttackTargets()
    {
        if (LocalEscadreProxy != null && LocalEscadreProxy.TargetEscadreEntityIds.Any())
            localEscadreAttackTargets_Display = string.Join(", ", LocalEscadreProxy.TargetEscadreEntityIds);
        else
            localEscadreAttackTargets_Display = "N/A";
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

        bool escadreProxyExists = LocalEscadreProxy != null && !LocalEscadreProxy.IsDestroyed;
        bool shopInfoExists = escadreProxyExists && LocalEscadreProxy.AvailableShopDesigns.Any();

        if (escadreProxyExists && shopInfoExists && _clientLevel.IsOceanInitialized)
        {
            isSessionFullyActive = true;
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Game session is now FULLY active.");
            if (topDownCameraController != null && topDownCameraController.GetTargetToFollow() == null)
            {
                TrySetCameraTargetToLocalEscadre();
            }
        }
    }

    void Update()
    {
        if (_clientClock != null) currentTime_Display = _clientClock.CurrentTime;
        if (_clientLevel != null) isOceanReady_Display = _clientLevel.IsOceanInitialized;

        if (LocalEscadreProxy != null && oceanPresentation != null && LocalEscadreProxy.Position != null)
        {
            oceanPresentation.FollowTarget(LocalEscadreProxy.Position.ToUnityVector());
        }

        if (!isSessionFullyActive && isConnectionAttempted)
        {
            if (LocalEscadreProxy == null && Time.frameCount % 60 == 0 && _clientLevel != null)
            {
                 var foundProxy = _clientLevel.ActiveProxies.Values
                    .OfType<EscadreProxy.ClientProxy>()
                    .FirstOrDefault(ep => ep.OwnerClientId == thisClientInstanceId);
                if (foundProxy != null)
                {
                    TryFindAndAssignLocalEscadreProxy(foundProxy);
                }
            }
            else if (LocalEscadreProxy != null && topDownCameraController != null && topDownCameraController.GetTargetToFollow() == null)
            {
                if (Time.frameCount % 120 == 0) TrySetCameraTargetToLocalEscadre();
            }

            CheckSessionActivation();
            if (!isSessionFullyActive && Time.frameCount > 60 && Time.frameCount % 120 == 0)
            {
                string reasons = "";
                if (LocalEscadreProxy == null) reasons += "Waiting for local Escadre proxy. ";
                else if (!LocalEscadreProxy.AvailableShopDesigns.Any()) reasons += "Waiting for shop designs. ";
                if (!_clientLevel.IsOceanInitialized) reasons += "Waiting for ocean init. ";
                if(!string.IsNullOrEmpty(reasons)) Logger.Log($"[ClientComposer {thisClientInstanceId}] Session not fully active. Reasons: {reasons}");
            }
        }

        if (_clientLevel != null) _clientLevel.DoUpdate(Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            CycleCameraTarget();
        }
    }

    private void CycleCameraTarget()
    {
        if (topDownCameraController == null || clientPresentationManager == null || _clientLevel == null || !_clientLevel.ActiveProxies.Any())
        {
            return;
        }

        var escadreProxies = _clientLevel.ActiveProxies.Values
            .OfType<EscadreProxy.ClientProxy>()
            .Where(ep => !ep.IsDestroyed)
            .OrderBy(ep => ep.EntityId)
            .ToList();

        if (escadreProxies.Count == 0) return;

        Transform currentTargetTransform = topDownCameraController.GetTargetToFollow();
        int currentIndex = -1;

        if (currentTargetTransform != null)
        {
            var cpp = currentTargetTransform.GetComponent<ClientProxyPresentation>();
            if (cpp != null && cpp.TargetProxy is EscadreProxy.ClientProxy currentEscadreProxy)
            {
                currentIndex = escadreProxies.FindIndex(ep => ep.EntityId == currentEscadreProxy.EntityId);
            }
        }

        int nextIndex = (currentIndex + 1) % escadreProxies.Count;
        EscadreProxy.ClientProxy nextTargetProxy = escadreProxies[nextIndex];

        GameObject presentationGO = FindPresentationForProxy(nextTargetProxy.EntityId);
        if (presentationGO != null)
        {
            topDownCameraController.SetTarget(presentationGO.transform, false);
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Cycled camera target to Escadre ID: {nextTargetProxy.EntityId}");
        }
        else
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Could not find presentation for next Escadre ID: {nextTargetProxy.EntityId} during TAB cycle.");
        }
    }


    void OnDisable()
    {
        Logger.Log($"[ClientComposer {thisClientInstanceId}] OnDisable called.");
        if (_clientNetworkAccess != null && isConnectionAttempted)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Sending _ClientDisconnect message.");
            _clientNetworkAccess.SendToServer(thisClientInstanceId, 0, MessageType._ClientDisconnect, writer => { });
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
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] DefaultShip design not found in shop info.");
            return;
        }
        float yOffset = LocalEscadreProxy.FormationSlots.Count * 2.5f;
        if (LocalEscadreProxy.FormationSlots.Count % 2 == 1) yOffset *= -1;
        _gameActions.RequestBuyShip(design.DesignId, new Core.Primitives.Vector2(LocalEscadreProxy.FormationSlots.Count * 1.0f , yOffset));
    }

    [ContextMenu("Shop: Upgrade First Ship")]
    public void MockUpgradeFirstShip()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }

        var firstShipSlot = LocalEscadreProxy.FormationSlots.FirstOrDefault(s => s.ShipEntityId.HasValue);
        if (firstShipSlot == null || !firstShipSlot.ShipEntityId.HasValue)
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ship in formation to upgrade.");
            return;
        }
        _gameActions.RequestUpgradeShip(firstShipSlot.ShipEntityId.Value);
    }

    [ContextMenu("Formation: Set Random Valid Formation")]
    public void MockSetRandomFormation()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }

        var shipsInFormation = LocalEscadreProxy.FormationSlots.Where(s => s.ShipEntityId.HasValue).ToList();
        if (!shipsInFormation.Any()) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No ships in formation."); return; }

        var newLayout = new List<Tuple<int, Core.Primitives.Vector2>>();
        float angleStep = 360f / shipsInFormation.Count;
        float radiusBase = 2.5f;
        float radiusIncrement = 1.5f;

        for (int i = 0; i < shipsInFormation.Count; i++)
        {
            var slot = shipsInFormation[i];
            float currentRadius = radiusBase + (i * radiusIncrement);
            float angle = i * angleStep * Mathf.Deg2Rad;
            if (i % 2 == 1 && shipsInFormation.Count > 3) currentRadius *= 1.2f;

            newLayout.Add(new Tuple<int, Core.Primitives.Vector2>(slot.ShipEntityId.Value, new Core.Primitives.Vector2(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius)));
        }
        if (newLayout.Any()) _gameActions.RequestSetFormation(newLayout);
    }

    [ContextMenu("Command: Attack First Other Escadre")]
    public void MockAttackFirstOtherEscadre()
    {
        if (!isSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
        if (_gameActions == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] GameActions not initialized."); return; }
        if (_clientLevel == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] ClientLevel not init."); return; }

        EscadreProxy.ClientProxy targetEscadre = _clientLevel.ActiveProxies.Values
            .OfType<EscadreProxy.ClientProxy>()
            .FirstOrDefault(ep => ep.EntityId != LocalEscadreProxy.EntityId && !ep.IsDestroyed);

        if (targetEscadre != null)
        {
            _gameActions.SendAttackEscadre(targetEscadre.EntityId);
        }
        else Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] No other (non-destroyed) escadres to attack.");
    }

    public void UICancelAllAttacks()
    {
        if (playerInputController != null)
        {
            playerInputController.RequestCancelAllAttacks();
        }
        else if (GameActions != null && LocalEscadreProxy != null && !LocalEscadreProxy.IsDestroyed)
        {
            GameActions.SendCancelAttack();
            Logger.Log("[ClientComposer] UICancelAllAttacks called via direct GameActions fallback.");
        }
        else
        {
             Logger.LogWarning($"[ClientComposer] UICancelAllAttacks: Cannot perform. PlayerInputController missing or session not ready.");
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
        _localEscadreProxy = null;
        OnLocalEscadreProxyChanged = null;


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