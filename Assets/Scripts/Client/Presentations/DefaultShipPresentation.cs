// File: Scripts/Client/Presentations/DefaultShipPresentation.cs
using UnityEngine;
using Core.Network.Proxies;
using Logger = Core.Logging.Logger; // Alias
using Client.Presentation; // For ShipTapAreaVisualizer if it's in this namespace
using Client.InputServices;
using System.Linq;

public class DefaultShipPresentation : ClientProxyPresentation
{
    [Header("DefaultShip Visuals")]
    public ParticleSystem explosionEffectPrefab;
    public AudioClip explosionSound;
    private AudioSource _audioSource;

    [Header("Tap Area Visualization")]
    [Tooltip("Assign the child ShipTapAreaVisualizer component here if it exists.")]
    [SerializeField] private ShipTapAreaVisualizer tapAreaVisualizer;
    [SerializeField] private float tapAreaRadius = 2.0f;
    
    [Tooltip("Color for friendly ships. CHECK INSPECTOR VALUE!")]
    [SerializeField] private Color friendlyTapAreaColor = new Color(0.5f, 1f, 0.5f, 0.3f); // Greenish
    [Tooltip("Color for enemy ships (not targeted). CHECK INSPECTOR VALUE!")]
    [SerializeField] private Color enemyTapAreaColor = new Color(1f, 0.5f, 0.5f, 0.3f);    // Reddish
    [Tooltip("Color for enemy ships targeted by player. CHECK INSPECTOR VALUE!")]
    [SerializeField] private Color enemyTargetedTapAreaColor = new Color(1f, 0.2f, 0.2f, 0.5f); // Brighter/Deeper Red

    private ShipProxy.ClientProxy _shipProxy;
    private ClientComposer _clientComposer;
    private EscadreProxy.ClientProxy _subscribedLocalEscadreForTargetEvents; // Stores the escadre we're listening to for target changes

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _shipProxy = TargetProxy as ShipProxy.ClientProxy;
        if (_shipProxy == null)
        {
            Logger.LogError($"[DefaultShipPresentation {gameObject.name}] TargetProxy is not a ShipProxy.ClientProxy! Type: {TargetProxy?.GetType().Name}");
            enabled = false;
            return;
        }

        _clientComposer = FindObjectOfType<ClientComposer>();
        if (_clientComposer == null)
        {
            Logger.LogWarning($"[DefaultShipPresentation {gameObject.name}] ClientComposer not found in scene. Cannot determine advanced tap area states.");
        }

        _shipProxy.HealthChanged += OnHealthChanged;
        _shipProxy.CurrentSpeedChanged += OnSpeedChanged;
        _shipProxy.StatsChanged += OnStatsChanged;
        
        if (_clientComposer != null)
        {
            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChangedForTapArea;
            SubscribeToCurrentLocalEscadreTargetEvents(); // Initial subscription
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        if (tapAreaVisualizer == null)
        {
            tapAreaVisualizer = GetComponentInChildren<ShipTapAreaVisualizer>();
        }
        
        UpdateTapAreaVisuals(); 

        OnHealthChanged(_shipProxy.CurrentHealth, _shipProxy.MaxHealth);
        OnSpeedChanged(_shipProxy.ClientSimulatedSpeed);
        OnStatsChanged();
    }

    private void SubscribeToCurrentLocalEscadreTargetEvents()
    {
        if (_clientComposer == null) return;

        // Unsubscribe from any previously subscribed escadre
        if (_subscribedLocalEscadreForTargetEvents != null)
        {
            _subscribedLocalEscadreForTargetEvents.OnTargetEscadreEntityIdsChanged -= UpdateTapAreaVisuals;
            _subscribedLocalEscadreForTargetEvents = null;
        }

        // Subscribe to the new one, if it exists
        if (_clientComposer.LocalEscadreProxy != null)
        {
            _clientComposer.LocalEscadreProxy.OnTargetEscadreEntityIdsChanged += UpdateTapAreaVisuals;
            _subscribedLocalEscadreForTargetEvents = _clientComposer.LocalEscadreProxy;
            Logger.Log($"[DefaultShipPresentation ShipID:{_shipProxy?.EntityId}] Subscribed to OnTargetEscadreEntityIdsChanged for LocalEscadre ID: {_clientComposer.LocalEscadreProxy.EntityId}");
        }
        else
        {
            Logger.Log($"[DefaultShipPresentation ShipID:{_shipProxy?.EntityId}] No LocalEscadreProxy to subscribe to for target events.");
        }
    }

    private void HandleLocalEscadreProxyChangedForTapArea(EscadreProxy.ClientProxy newLocalEscadreProxy)
    {
        // ClientComposer's LocalEscadreProxy has changed. Re-evaluate subscriptions and visuals.
        Logger.Log($"[DefaultShipPresentation ShipID:{_shipProxy?.EntityId}] LocalEscadreProxy changed. New local escadre ID: {(newLocalEscadreProxy != null ? newLocalEscadreProxy.EntityId.ToString() : "NULL")}");
        SubscribeToCurrentLocalEscadreTargetEvents();
        UpdateTapAreaVisuals(); 
    }

    private void UpdateTapAreaVisuals()
    {
        if (tapAreaVisualizer == null || _shipProxy == null) return;

        bool isFriendly = false;
        bool isTargetedByPlayer = false;
        
        string logPrefix = $"[DSP.UpdateTapAreaVisuals ShipID:{_shipProxy.EntityId}] ";
        System.Text.StringBuilder logBuilder = new System.Text.StringBuilder(logPrefix);

        logBuilder.Append($"OwnEscClientID:{_shipProxy.OwningEscadreClientId}. ");

        if (_clientComposer != null && _clientComposer.LocalEscadreProxy != null && _clientComposer.ClientLevel != null)
        {
            logBuilder.Append($"LocalPlayerEscOwnClientID:{_clientComposer.LocalEscadreProxy.OwnerClientId}. ");
            if (_shipProxy.OwningEscadreClientId == _clientComposer.LocalEscadreProxy.OwnerClientId)
            {
                isFriendly = true;
            }
            else 
            {
                EscadreProxy.ClientProxy thisShipsEscadreOwnerProxy = null;
                foreach (var proxyPair in _clientComposer.ClientLevel.ActiveProxies)
                {
                    if (proxyPair.Value is EscadreProxy.ClientProxy escadre && 
                        !escadre.IsDestroyed &&
                        escadre.OwnerClientId == _shipProxy.OwningEscadreClientId) 
                    {
                        thisShipsEscadreOwnerProxy = escadre;
                        break;
                    }
                }

                if (thisShipsEscadreOwnerProxy != null)
                {
                    logBuilder.Append($"EnemyEscID:{thisShipsEscadreOwnerProxy.EntityId}. ");
                    if (_clientComposer.LocalEscadreProxy.TargetEscadreEntityIds.Contains(thisShipsEscadreOwnerProxy.EntityId))
                    {
                        isTargetedByPlayer = true;
                    }
                }
                else
                {
                    logBuilder.Append("EnemyEscProxy:Not Found. ");
                }
            }
        }
        else
        {
            logBuilder.Append("Composer/LocalEsc/ClientLevel:Missing. ");
        }

        logBuilder.Append($"isFriendly:{isFriendly}, isTargeted:{isTargetedByPlayer}. ");
        logBuilder.Append($"ColorVars(F,E,ET): {friendlyTapAreaColor:F3}, {enemyTapAreaColor:F3}, {enemyTargetedTapAreaColor:F3}. ");

        Color chosenColor;
        if (isFriendly)
        {
            chosenColor = friendlyTapAreaColor;
            logBuilder.Append($"Chose:Friendly({chosenColor:F3}).");
        }
        else 
        {
            chosenColor = isTargetedByPlayer ? enemyTargetedTapAreaColor : enemyTapAreaColor;
            logBuilder.Append($"Chose:{(isTargetedByPlayer ? "EnemyTargeted" : "Enemy")}({chosenColor:F3}).");
        }
        
        Logger.Log(logBuilder.ToString());
        
        tapAreaVisualizer.RefreshVisuals(tapAreaRadius, chosenColor, tapAreaVisualizer.lineWidth);
        tapAreaVisualizer.SetVisibility(true);
    }

    private void OnHealthChanged(float current, float max)
    {
        var r = GetComponent<Renderer>();
        if (r != null && max > 0) r.material.color = Color.Lerp(Color.red, Color.green, current / max);
    }
    private void OnSpeedChanged(float speed) { /* ... */ }
    private void OnStatsChanged() { /* ... */ }

    protected override void HandleLoudDestruction()
    {
        base.HandleLoudDestruction();
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
        if (explosionSound != null && _audioSource != null) _audioSource.PlayOneShot(explosionSound);

        var mainRenderer = GetComponent<Renderer>(); if (mainRenderer != null) mainRenderer.enabled = false;
        var mainUnityCollider = GetComponent<Collider>(); if (mainUnityCollider != null) mainUnityCollider.enabled = false;

        if (tapAreaVisualizer != null) tapAreaVisualizer.SetVisibility(false);
    }

    protected override void UnsubscribeFromProxyEvents()
    {
        base.UnsubscribeFromProxyEvents();
        if (_shipProxy != null)
        {
            _shipProxy.HealthChanged -= OnHealthChanged;
            _shipProxy.CurrentSpeedChanged -= OnSpeedChanged;
            _shipProxy.StatsChanged -= OnStatsChanged;
        }
        if (_clientComposer != null)
        {
            _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChangedForTapArea;
        }
        if (_subscribedLocalEscadreForTargetEvents != null)
        {
            Logger.Log($"[DefaultShipPresentation ShipID:{_shipProxy?.EntityId}] Unsubscribing from OnTargetEscadreEntityIdsChanged for LocalEscadre ID: {_subscribedLocalEscadreForTargetEvents.EntityId}");
            _subscribedLocalEscadreForTargetEvents.OnTargetEscadreEntityIdsChanged -= UpdateTapAreaVisuals;
            _subscribedLocalEscadreForTargetEvents = null;
        }
    }
}