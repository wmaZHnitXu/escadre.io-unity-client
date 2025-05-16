// File: Scripts/Server/Debug/ClientConnectionDebugBehaviour.cs
using UnityEngine;
using Core;                 // For CoreComposer
using Core.Session;
using Core.Model;
using Core.Primitives;
using Core.Visibility;
using ServerSpecific.Debug;
using System.Linq;
using System.Collections.Generic;
using Logger = Core.Logging.Logger;


public class ClientConnectionDebugBehaviour : MonoBehaviour
{
    [Header("Client Info")]
    [SerializeField/*, ReadOnly*/] protected int clientId_Display;
    [SerializeField/*, ReadOnly*/] protected string clientState_Display;
    [SerializeField/*, ReadOnly*/] protected Core.Primitives.Vector3 pvsCenterPosition_Display;
    [SerializeField/*, ReadOnly*/] protected float pvsRadiusOfInterest_Display;

    [Header("Escadre Info (if any)")]
    [SerializeField/*, ReadOnly*/] protected bool hasEscadre_Display;
    [SerializeField/*, ReadOnly*/] protected int escadreShipCount_Display;
    [SerializeField/*, ReadOnly*/] protected string escadreDestination_Display;
    [SerializeField/*, ReadOnly*/] protected string escadreTargetEnemyIds_Display;
    [SerializeField/*, ReadOnly*/] protected int escadreResources_Display;

    [Header("PVS Visualization")]
    public bool showPVSEntities = true;
    public Color pvsEntityLineColor = new Color(0.2f, 1f, 0.2f, 0.5f);

    [Header("Debug Action Parameters")]
    [Tooltip("Assign a Gizmo Transform. Use 'Set Escadre Course To Gizmo' action.")]
    public Transform courseTargetGizmo;
    [Tooltip("Enter Client ID of the escadre to target.")]
    public int targetEscadreIdForAttack = -1; // Default to an invalid ID
    [Tooltip("Enter Entity ID of the ship to upgrade within this client's escadre.")]
    public int shipIdForUpgrade = -1; // Default to an invalid ID


    private ClientConnection _clientConnection;
    private Escadre _escadreInstance; // Cached for convenience

    private CoreComposer _coreComposerRef; // Reference to access Level, other ClientConnections
    private VisibilityManager _visibilityManager;
    private DebugPresentationManager _entityDebugManager;


    public void Initialize(ClientConnection connection,
                           CoreComposer coreComposer,
                           VisibilityManager visibilityManager,
                           DebugPresentationManager entityDebugManager)
    {
        _clientConnection = connection ?? throw new System.ArgumentNullException(nameof(connection));
        _coreComposerRef = coreComposer ?? throw new System.ArgumentNullException(nameof(coreComposer));
        _visibilityManager = visibilityManager ?? throw new System.ArgumentNullException(nameof(visibilityManager));
        _entityDebugManager = entityDebugManager ?? throw new System.ArgumentNullException(nameof(entityDebugManager));

        gameObject.name = $"ClientConn_{_clientConnection.ClientId}";

        if (courseTargetGizmo != null)
        {
            courseTargetGizmo.gameObject.name = $"{gameObject.name}_CourseGizmo";
            courseTargetGizmo.position = transform.position; // Initial position
            courseTargetGizmo.gameObject.SetActive(false); // Initially hidden
        }

        UpdateDebugInfo();
        Logger.Log($"[ClientConnDebug {clientId_Display}] Initialized.");
    }

    void Update()
    {
        if (_clientConnection == null) { Logger.LogWarning($"[ClientConnDebug {name}] Update called but _clientConnection is null."); enabled = false; return; }
        transform.position = _clientConnection.Position.ToUnityVector();
        UpdateDebugInfo();
    }

    protected virtual void UpdateDebugInfo()
    {
        if (_clientConnection == null) return;
        clientId_Display = _clientConnection.ClientId;
        clientState_Display = _clientConnection.CurrentState.ToString();
        pvsCenterPosition_Display = _clientConnection.Position;
        pvsRadiusOfInterest_Display = _clientConnection.RadiusOfInterest;
        _escadreInstance = _clientConnection.EscadreInstance;
        hasEscadre_Display = _escadreInstance != null;
        if (hasEscadre_Display && _escadreInstance != null) {
            escadreShipCount_Display = _escadreInstance.ShipEntityIds.Count;
            escadreDestination_Display = _escadreInstance.CurrentDestination?.ToString() ?? "None";
            escadreTargetEnemyIds_Display = _escadreInstance.TargetEscadreOwnerClientIds.Any()
                ? string.Join(", ", _escadreInstance.TargetEscadreOwnerClientIds) : "None";
            escadreResources_Display = _escadreInstance.Resources;
        } else {
            escadreShipCount_Display = 0; escadreDestination_Display = "N/A";
            escadreTargetEnemyIds_Display = "N/A"; escadreResources_Display = 0;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (_clientConnection == null) return;
        Color pvsSphereColor = Color.green; pvsSphereColor.a = 0.05f; Gizmos.color = pvsSphereColor;
        Gizmos.DrawSphere(transform.position, _clientConnection.RadiusOfInterest);
        Color pvsWireColor = Color.green; pvsWireColor.a = 0.5f; Gizmos.color = pvsWireColor;
        Gizmos.DrawWireSphere(transform.position, _clientConnection.RadiusOfInterest);

        if (showPVSEntities && _visibilityManager != null && _entityDebugManager != null) {
            IReadOnlyCollection<int> pvsEntityIds = _visibilityManager.GetPVSForClient(_clientConnection.ClientId);
            if (pvsEntityIds != null && pvsEntityIds.Any()) {
                Gizmos.color = pvsEntityLineColor;
                foreach (int entityId in pvsEntityIds) {
                    if (_entityDebugManager.TryGetDebugGameObjectForEntity(entityId, out GameObject entityGO) && entityGO != null) {
                        Gizmos.DrawLine(transform.position, entityGO.transform.position);
                    }
                }
            }
        }
        if (courseTargetGizmo != null && courseTargetGizmo.gameObject.activeSelf) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, courseTargetGizmo.position);
            Gizmos.DrawWireSphere(courseTargetGizmo.position, 0.7f);
        }
    }

    // --- Context Menu Actions ---

    [ContextMenu("Escadre: Set Course to Gizmo")]
    private void DebugSetEscadreCourse()
    {
        if (_clientConnection == null || _escadreInstance == null) { Logger.LogWarning("No client connection or escadre."); return; }
        if (courseTargetGizmo == null) { Logger.LogWarning("Course Target Gizmo not assigned."); return; }

        if (!courseTargetGizmo.gameObject.activeSelf)
        {
            courseTargetGizmo.position = transform.position + transform.forward * 20f; // Default offset
            courseTargetGizmo.gameObject.SetActive(true);
            Logger.Log($"[ClientConnDebug {clientId_Display}] Activated course gizmo. Move it and click again.");
            return;
        }
        Core.Primitives.Vector2 dest = new Core.Primitives.Vector2(courseTargetGizmo.position.x, courseTargetGizmo.position.z);
        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting SetCourse to {dest}");
        _clientConnection.RequestSetCourse(dest);
    }

    [ContextMenu("Escadre: Order Attack (using TargetEscadreIdForAttack field)")]
    private void DebugOrderAttack()
    {
        if (_clientConnection == null || _escadreInstance == null) { Logger.LogWarning("No client connection or escadre."); return; }
        if (targetEscadreIdForAttack < 0) { Logger.LogWarning("Invalid TargetEscadreIdForAttack. Set it in Inspector."); return; }
        if (targetEscadreIdForAttack == _clientConnection.ClientId) { Logger.LogWarning("Cannot attack self."); return; }

        // Optional: Check if targetEscadreIdForAttack actually exists
        if (!_coreComposerRef.ClientConnections.ContainsKey(targetEscadreIdForAttack))
        {
             Logger.LogWarning($"Target client ID {targetEscadreIdForAttack} does not exist.");
             // return; // Or proceed anyway for testing server resilience
        }

        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting AttackEscadre on Client {targetEscadreIdForAttack}");
        _clientConnection.RequestAttackEscadre(targetEscadreIdForAttack);
    }

    [ContextMenu("Escadre: Cancel All Attack Orders")]
    private void DebugCancelAttack()
    {
        if (_clientConnection == null || _escadreInstance == null) { Logger.LogWarning("No client connection or escadre."); return; }
        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting CancelAttack");
        _clientConnection.RequestCancelAttack();
    }

    [ContextMenu("Escadre: Buy DefaultShip (Debug - bypasses resources)")]
    private void DebugBuyDefaultShip()
    {
        if (_clientConnection == null || _escadreInstance == null) { Logger.LogWarning("No client connection or escadre."); return; }
        if (_coreComposerRef == null) { Logger.LogWarning("CoreComposer reference missing."); return; }

        Logger.Log($"[ClientConnDebug {clientId_Display}] DEBUG: Buying DefaultShip (bypassing Escadre.RequestBuyShip and resources).");
        Core.Primitives.Vector3 spawnPos = _escadreInstance.CalculateCenterPoint() +
                                          new Core.Primitives.Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));

        DefaultShip newShip = new DefaultShip(_coreComposerRef.ServerLevel, _escadreInstance, spawnPos);
        // The DefaultShip constructor calls _level.AddEntity(this), which queues it for processing.
        // We must also explicitly add it to the escadre's list.
        _escadreInstance.AddShip(newShip);
        Logger.Log($"[ClientConnDebug {clientId_Display}] Added DefaultShip. ID will be assigned by Level. Current escadre ships: {_escadreInstance.ShipEntityIds.Count}");
    }

    [ContextMenu("Escadre: Upgrade Ship (using ShipIdForUpgrade field)")]
    private void DebugUpgradeShipById()
    {
        if (_clientConnection == null || _escadreInstance == null) { Logger.LogWarning("No client connection or escadre."); return; }
        if (shipIdForUpgrade < 0) { Logger.LogWarning("Invalid ShipIdForUpgrade. Set it in Inspector."); return; }

        if (!_escadreInstance.ShipEntityIds.Contains(shipIdForUpgrade))
        {
            Logger.LogWarning($"Ship ID {shipIdForUpgrade} not found in escadre {_clientConnection.ClientId}.");
            return;
        }

        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting UpgradeShip for Ship ID {shipIdForUpgrade}");
        _clientConnection.RequestUpgradeShip(shipIdForUpgrade);
    }

    [ContextMenu("Escadre: Upgrade First Ship")]
    private void DebugUpgradeFirstShip()
    {
        if (_clientConnection == null || _escadreInstance == null) { Logger.LogWarning("No client connection or escadre."); return; }
        if (!_escadreInstance.ShipEntityIds.Any()) { Logger.LogWarning("Escadre has no ships to upgrade."); return; }

        int firstShipId = _escadreInstance.ShipEntityIds.First();
        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting UpgradeShip for first ship (ID {firstShipId})");
        _clientConnection.RequestUpgradeShip(firstShipId);
    }
     public ClientConnection GetClientConnection() => _clientConnection;
}