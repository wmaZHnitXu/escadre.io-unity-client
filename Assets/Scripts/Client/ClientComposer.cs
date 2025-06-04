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

using Core.Model; 
using Core.Network.Proxies; 
using Core.Ocean;
using Client.Camera; 
using Client.InputServices; 
using Client.Presentation;  
using Client.UI; 
using Client.UI.Formation; // Required if directly referencing FormationUIMediator here

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
    private OceanPresentation oceanPresentation; 
    [SerializeField]
    private Material oceanMaterial;

    [Header("Presentation Managers & Visualizers")]
    [SerializeField]
    private ClientPresentationManager clientPresentationManager;
    [SerializeField]
    private CommandVisualizer commandVisualizer;  

    [Header("Input")]
    [SerializeField]
    private PlayerInputController playerInputController; 

    [Header("Camera Control")]
    [SerializeField]
    private TopDownCameraController topDownCameraController;
    [SerializeField]
    private UnityEngine.Camera mainGameCamera;

    [Header("UI Management")] 
    [SerializeField]
    private ClientUIManager clientUIManager; // ClientUIManager will manage its sub-components like FormationUIMediator


    [Header("Debug Info")]
    [SerializeField, ReadOnly]
    private float currentTime_Display;
    [SerializeField, ReadOnly]
    public bool isConnectionAttempted = false; 
    public bool IsSessionFullyActive { get; private set; } = false; 
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
        if (clientUIManager == null) // Ensure UIManager is found or assigned
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] ClientUIManager not assigned in Inspector. Attempting to find.");
            clientUIManager = FindObjectOfType<ClientUIManager>();
             if (clientUIManager == null)  Logger.LogError($"[ClientComposer {thisClientInstanceId}] ClientUIManager not found. UI system will not initialize.");
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
        SetupUIManager(); 
        // --- End Systems Initialization ---

        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Initialization complete. Will attempt connection in OnEnable.");
    }

    private void SetupPlayerInputController()
    {
        if (playerInputController != null)
        {
            if (mainGameCamera != null)
            {
                OceanPresentation oceanForInput = oceanPresentation ?? FindObjectOfType<OceanPresentation>();
                playerInputController.Initialize(this, mainGameCamera, oceanForInput);
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

    private void SetupUIManager() 
    {
        if (clientUIManager == null)
        {
            clientUIManager = FindObjectOfType<ClientUIManager>();
            if (clientUIManager != null)
            {
                Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] ClientUIManager was not assigned in Inspector, found existing instance in scene: {clientUIManager.name}.");
            }
        }

        if (clientUIManager != null)
        {
            clientUIManager.Initialize(this);
            Logger.Log($"[ClientComposer {thisClientInstanceId}] ClientUIManager initialized via ClientComposer.");
        }
        else Logger.LogError($"[ClientComposer {thisClientInstanceId}] ClientUIManager not assigned or found. UI will not function.");
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

            OceanPresentation oceanToBoundTo = oceanPresentation ?? FindObjectOfType<OceanPresentation>();
            if (oceanToBoundTo != null)
            {
                topDownCameraController.SetOceanBoundary(oceanToBoundTo);
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
            oceanPresentation.name = $"{prefabRef.name}_InstanceFromPrefab"; 
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Instantiated OceanPresentation from prefab: {oceanPresentation.name}");
        }


        if (oceanPresentation != null)
        {
            Logger.Log($"[ClientComposer {thisClientInstanceId}] OceanPresentation is set up ({oceanPresentation.name}), waiting for ocean data to fully initialize.");
            if (topDownCameraController != null)
            {
                topDownCameraController.SetOceanBoundary(oceanPresentation);
            }
            //TapResolverService.UpdateOceanPlaneHeight(oceanPresentation.oceanYLevel);
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

        if (_clientLevel == null)
        {
            Logger.LogError($"[ClientComposer {thisClientInstanceId}] _clientLevel is null when trying to initialize ocean. This should not happen.");
            return;
        }
        var clientOceanProvider = new ClientTextureBasedOceanDataProvider(settings, textureBytesToUse);
        _clientLevel.InitializeOcean(clientOceanProvider); 
        isOceanReady_Display = _clientLevel.IsOceanInitialized;

        if (clientOceanVisualizer != null && _clientLevel.OceanDataProvider != null)
        {
            clientOceanVisualizer.Initialize(_clientLevel.OceanDataProvider, _clientClock);
            Logger.Log($"[ClientComposer {thisClientInstanceId}] ClientOceanVisualizer fully initialized.");
        }

        OceanPresentation oceanToInit = oceanPresentation ?? FindObjectOfType<OceanPresentation>();
        if (oceanToInit != null && _clientLevel.OceanDataProvider != null && oceanMaterial != null && textureBytesToUse != null)
        {
            oceanToInit.Initialize(_clientLevel.OceanDataProvider, _clientClock, textureBytesToUse, oceanMaterial);
            Logger.Log($"[ClientComposer {thisClientInstanceId}] OceanPresentation fully initialized after receiving ocean settings.");
        }
        else
        {
            string reason = "";
            if (oceanToInit == null) reason += "OceanPresentation component missing/not found. ";
            if (_clientLevel.OceanDataProvider == null) reason += "OceanDataProvider not ready. ";
            if (oceanMaterial == null) reason += "OceanMaterial missing. ";
            if (textureBytesToUse == null) reason += "TextureBytes missing/not loaded for OceanPresentation. ";
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] OceanPresentation could NOT be fully initialized after ocean settings. Reason(s): {reason}");
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

    private GameObject FindPresentationForProxy(int proxyId)
    {
        if (clientPresentationManager == null)
        {
            clientPresentationManager = FindObjectOfType<ClientPresentationManager>();
            if (clientPresentationManager == null)
            {
                Logger.LogWarning($"[ClientComposer FindPresentationForProxy] ClientPresentationManager not available.");
                return null;
            }
        }
        ClientProxyPresentation[] allPresentations = FindObjectsOfType<ClientProxyPresentation>(true); 
        foreach (var presentation in allPresentations)
        {
            if (presentation.TargetProxy != null && presentation.TargetProxy.EntityId == proxyId)
            {
                return presentation.gameObject;
            }
        }
        Logger.LogWarning($"[ClientComposer FindPresentationForProxy] No presentation found for proxy ID {proxyId}.");
        return null;
    }

    private void TrySetCameraTargetToLocalEscadre()
    {
        if (LocalEscadreProxy != null && topDownCameraController != null) 
        {
            GameObject presentationGO = FindPresentationForProxy(LocalEscadreProxy.EntityId);
            if (presentationGO != null && presentationGO.activeInHierarchy) 
            {
                topDownCameraController.SetTarget(presentationGO.transform, true); 
                Logger.Log($"[ClientComposer {thisClientInstanceId}] Camera target set to local escadre: {presentationGO.name}");
            }
            else
            {
                Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Could not find active presentation GameObject for local escadre proxy ID {LocalEscadreProxy.EntityId} to set camera target. Will retry if session not active.");
            }
        }
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
            IsSessionFullyActive = false; 
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
        LocalEscadreProxy.OnShopDesignsChanged += CheckSessionActivation; 

        UpdateDebugDisplay_Destination(); 
        UpdateDebugDisplay_AttackTargets(); 
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
        LocalEscadreProxy.OnShopDesignsChanged -= CheckSessionActivation;
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
    private void HandleShopDesignsChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Shop designs updated. Count: {LocalEscadreProxy.AvailableShopDesigns.Count}");}
    private void HandleFormationChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Formation updated. Slot Count: {LocalEscadreProxy.FormationSlots.Count}");}
    private void HandleResourcesChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Resources updated. Amount: {LocalEscadreProxy.Resources}"); }
    private void HandleNicknameChanged_Debug() { if (LocalEscadreProxy == null) return; Logger.Log($"[ClientComposer {thisClientInstanceId} DEBUG] Nickname updated. Value: '{LocalEscadreProxy.Nickname}'"); }

    [ContextMenu("Connect")]
    public void ConnectToTheServer()
    {
        if (!isConnectionAttempted && !IsSessionFullyActive) RequestConnection();
        else if (isConnectionAttempted && !IsSessionFullyActive) Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Connection previously attempted but session not fully active. Waiting for server data.");
    }

    private void RequestConnection()
    {
        if (_clientNetworkAccess == null) { Logger.LogError($"[ClientComposer {thisClientInstanceId}] Network layer unavailable for connection request."); return; }
        isConnectionAttempted = true; IsSessionFullyActive = false;
        string mockJwt = $"{thisClientInstanceId};{thisClientNickname};{thisClientIsAdmin.ToString().ToLowerInvariant()};{thisClientAuthTypeString}";
        Logger.Log($"[ClientComposer {thisClientInstanceId}] Sending _ClientConnectRequest with Mock JWT: '{mockJwt}'");
        _clientNetworkAccess.SendToServer(thisClientInstanceId, 0, MessageType._ClientConnectRequest, writer => writer.Write(mockJwt));
    }

    private void CheckSessionActivation() 
    {
        if (IsSessionFullyActive) return;

        bool escadreProxyExistsAndValid = LocalEscadreProxy != null && !LocalEscadreProxy.IsDestroyed;
        bool shopInfoExists = escadreProxyExistsAndValid && LocalEscadreProxy.AvailableShopDesigns.Any();
        bool oceanReady = _clientLevel != null && _clientLevel.IsOceanInitialized;

        if (escadreProxyExistsAndValid && shopInfoExists && oceanReady)
        {
            IsSessionFullyActive = true;
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Game session is now FULLY active. Escadre: {LocalEscadreProxy.EntityId}, ShopDesigns: {LocalEscadreProxy.AvailableShopDesigns.Count}, OceanReady: {oceanReady}");
            if (topDownCameraController != null && topDownCameraController.GetTargetToFollow() == null)
            {
                TrySetCameraTargetToLocalEscadre();
            }
        }
        else
        {
            if (IsSessionFullyActive) 
            {
                IsSessionFullyActive = false;
                 Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Game session became INACTIVE.");
            }
        }
    }

    void Update()
    {
        if (_clientClock != null) currentTime_Display = _clientClock.CurrentTime;
        if (_clientLevel != null) isOceanReady_Display = _clientLevel.IsOceanInitialized;

        if (LocalEscadreProxy != null && oceanPresentation != null && LocalEscadreProxy.Position != Core.Primitives.Vector3.Zero) 
        {
            oceanPresentation.FollowTarget(LocalEscadreProxy.Position.ToUnityVector());
        }
        else if (LocalEscadreProxy == null && oceanPresentation != null && topDownCameraController != null && topDownCameraController.mainCamera != null)
        {
            UnityEngine.Vector3 camRigPos = topDownCameraController.transform.position;
            oceanPresentation.FollowTarget(new UnityEngine.Vector3(camRigPos.x, oceanPresentation.transform.position.y, camRigPos.z));
        }


        if (!IsSessionFullyActive && isConnectionAttempted)
        {
            if (LocalEscadreProxy == null && Time.frameCount % 60 == 0 && _clientLevel != null) 
            {
                 var foundProxy = _clientLevel.ActiveProxies.Values
                    .OfType<EscadreProxy.ClientProxy>()
                    .FirstOrDefault(ep => ep.OwnerClientId == thisClientInstanceId && !ep.IsDestroyed);
                if (foundProxy != null)
                {
                    TryFindAndAssignLocalEscadreProxy(foundProxy);
                }
            }
            else if (LocalEscadreProxy != null && topDownCameraController != null && topDownCameraController.GetTargetToFollow() == null)
            {
                if (Time.frameCount % 120 == 0) TrySetCameraTargetToLocalEscadre(); 
            }

            if (Time.frameCount % 30 == 0) CheckSessionActivation(); 

            if (!IsSessionFullyActive && Time.frameCount > 120 && Time.frameCount % 120 == 0) 
            {
                string reasons = "";
                if (LocalEscadreProxy == null) reasons += "Waiting for local Escadre proxy. ";
                else if (!LocalEscadreProxy.IsDestroyed && !LocalEscadreProxy.AvailableShopDesigns.Any()) reasons += "Waiting for shop designs. ";
                if (_clientLevel == null || !_clientLevel.IsOceanInitialized) reasons += "Waiting for ocean init. ";
                if(!string.IsNullOrEmpty(reasons)) Logger.Log($"[ClientComposer {thisClientInstanceId}] Session not fully active (Update loop). Reasons: {reasons}");
            }
        }

        if (_clientLevel != null) _clientLevel.DoUpdate(Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // CycleCameraTarget(); // Removed as per prompt, assuming this was debug/test code
        }
    }

    private void CycleCameraTarget()
    {
        if (topDownCameraController == null || _clientLevel == null || !_clientLevel.ActiveProxies.Any())
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
        if (presentationGO != null && presentationGO.activeInHierarchy)
        {
            topDownCameraController.SetTarget(presentationGO.transform, false); 
            Logger.Log($"[ClientComposer {thisClientInstanceId}] Cycled camera target to Escadre ID: {nextTargetProxy.EntityId}");
        }
        else
        {
            Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Could not find active presentation for next Escadre ID: {nextTargetProxy.EntityId} during TAB cycle.");
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
        IsSessionFullyActive = false; isConnectionAttempted = false;
    }

    [ContextMenu("Shop: Buy DefaultShip (Slot near last)")]
    public void MockBuyDefaultShip()
    {
        if (!IsSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing. Cannot buy ship."); return; }
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
        if (!IsSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
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
        if (!IsSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
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
        if (!IsSessionFullyActive || LocalEscadreProxy == null) { Logger.LogWarning($"[ClientComposer {thisClientInstanceId}] Session not active or LocalEscadreProxy missing."); return; }
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
            Logger.Log("[ClientComposer] UICancelAllAttacks routed through PlayerInputController.");
        }
        else if (GameActions != null && LocalEscadreProxy != null && !LocalEscadreProxy.IsDestroyed && IsSessionFullyActive)
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

        if (oceanPresentation != null && oceanPresentation.gameObject.scene.name != null && (oceanPresentation.name.EndsWith("_InstanceFromPrefab") ))
        {
            Destroy(oceanPresentation.gameObject);
        }
        oceanPresentation = null;

        Logger.Log($"[ClientComposer {thisClientInstanceId}] Client Core Cleanup complete.");
    }
}