// File: Scripts/Server/Debug/ClientConnectionDebugBehaviour.cs
using UnityEngine;
using Core.Session;
using Core.Model;
using Core.Visibility;
using ServerSpecific.Debug; // For DebugPresentationManager
using System.Linq;
using System.Collections.Generic;
using Logger = Core.Logging.Logger;
using System;
using Random = UnityEngine.Random;


public class ClientConnectionDebugBehaviour : MonoBehaviour
{
    [Header("Client Info")]
    [SerializeField/*, ReadOnly*/] protected int clientId_Display;
    [SerializeField/*, ReadOnly*/] protected string clientState_Display;
    [SerializeField/*, ReadOnly*/] protected Core.Primitives.Vector3 pvsCenterPosition_Display; // This will be EscadreEntity.Position
    [SerializeField/*, ReadOnly*/] protected float pvsRadiusOfInterest_Display;
    [SerializeField/*, ReadOnly*/] protected int escadreEntityId_Display = -1;


    [Header("Escadre Info (if any)")]
    [SerializeField/*, ReadOnly*/] protected bool hasEscadre_Display;
    [SerializeField/*, ReadOnly*/] protected string escadreNickname_Display;
    [SerializeField/*, ReadOnly*/] protected int escadreShipCount_Display;
    [SerializeField/*, ReadOnly*/] protected string escadreDestination_Display;
    [SerializeField/*, ReadOnly*/] protected string escadreTargetEnemyEntityIds_Display; // Changed
    [SerializeField/*, ReadOnly*/] protected int escadreResources_Display;

    [Header("PVS Visualization")]
    public bool showPVSEntities = true;
    public Color pvsEntityLineColor = new Color(0.2f, 1f, 0.2f, 0.5f);

    [Header("Debug Action Parameters")]
    [Tooltip("Assign a Gizmo Transform. Use 'Set Escadre Course To Gizmo' action.")]
    public Transform courseTargetGizmo;
    [Tooltip("Enter Entity ID of the Escadre to target.")] // Changed
    public int targetEscadreEntityIdForAttack = -1; 
    [Tooltip("Enter Entity ID of the ship to upgrade within this client's escadre.")]
    public int shipIdForUpgrade = -1; 


    private ClientConnection _clientConnection;
    private Escadre _escadreEntity; // Renamed from _escadreInstance

    private Core.CoreComposer _coreComposerRef; 
    private VisibilityManager _visibilityManager;
    private DebugPresentationManager _entityDebugManager;


    public void Initialize(ClientConnection connection,
                           Core.CoreComposer coreComposer, 
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
            courseTargetGizmo.position = transform.position; 
            courseTargetGizmo.gameObject.SetActive(false); 
        }

        UpdateDebugInfo();
        Logger.Log($"[ClientConnDebug {clientId_Display}] Initialized.");
    }

    void Update()
    {
        if (_clientConnection == null) { Logger.LogWarning($"[ClientConnDebug {name}] Update called but _clientConnection is null."); enabled = false; return; }
        // The debug transform follows the PVS center, which is ClientConnection.Position (derived from EscadreEntity.Position)
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
        
        _escadreEntity = _clientConnection.EscadreEntity; // Get current reference
        hasEscadre_Display = _escadreEntity != null && !_escadreEntity.IsDead;
        escadreEntityId_Display = _escadreEntity?.Id ?? -1;

        if (hasEscadre_Display && _escadreEntity != null) {
            escadreNickname_Display = _escadreEntity.Nickname;
            escadreShipCount_Display = _escadreEntity.ShipEntityIds.Count;
            escadreDestination_Display = _escadreEntity.CurrentDestination?.ToString() ?? "None";
            escadreTargetEnemyEntityIds_Display = _escadreEntity.TargetEscadreEntityIds.Any()
                ? string.Join(", ", _escadreEntity.TargetEscadreEntityIds) : "None";
            escadreResources_Display = _escadreEntity.Resources;
        } else {
            escadreNickname_Display = "N/A";
            escadreShipCount_Display = 0; escadreDestination_Display = "N/A";
            escadreTargetEnemyEntityIds_Display = "N/A"; escadreResources_Display = 0;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (_clientConnection == null) return;
        // PVS sphere should be drawn at ClientConnection.Position, which is EscadreEntity.Position
        Vector3 pvsCenter = _clientConnection.Position.ToUnityVector(); 

        Color pvsSphereColor = Color.green; pvsSphereColor.a = 0.05f; Gizmos.color = pvsSphereColor;
        Gizmos.DrawSphere(pvsCenter, _clientConnection.RadiusOfInterest);
        Color pvsWireColor = Color.green; pvsWireColor.a = 0.5f; Gizmos.color = pvsWireColor;
        Gizmos.DrawWireSphere(pvsCenter, _clientConnection.RadiusOfInterest);

        if (showPVSEntities && _visibilityManager != null && _entityDebugManager != null) {
            IReadOnlyCollection<int> pvsEntityIds = _visibilityManager.GetPVSForClient(_clientConnection.ClientId);
            if (pvsEntityIds != null && pvsEntityIds.Any()) {
                Gizmos.color = pvsEntityLineColor;
                foreach (int entityId in pvsEntityIds) {
                    if (_entityDebugManager.TryGetDebugGameObjectForEntity(entityId, out GameObject entityGO) && entityGO != null) {
                        Gizmos.DrawLine(pvsCenter, entityGO.transform.position);
                    }
                }
            }
        }
        if (courseTargetGizmo != null && courseTargetGizmo.gameObject.activeSelf) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pvsCenter, courseTargetGizmo.position); // Line from PVS center (Escadre pos)
            Gizmos.DrawWireSphere(courseTargetGizmo.position, 0.7f);
        }
    }


    [ContextMenu("Escadre: Set Course to Gizmo")]
    private void DebugSetEscadreCourse()
    {
        if (_clientConnection == null || _escadreEntity == null || _escadreEntity.IsDead) { Logger.LogWarning("No client connection or active escadre."); return; }
        if (courseTargetGizmo == null) { Logger.LogWarning("Course Target Gizmo not assigned."); return; }

        if (!courseTargetGizmo.gameObject.activeSelf)
        {
            // Place gizmo relative to the escadre's current position
            courseTargetGizmo.position = _escadreEntity.Position.ToUnityVector() + transform.forward * 20f; 
            courseTargetGizmo.gameObject.SetActive(true);
            Logger.Log($"[ClientConnDebug {clientId_Display}] Activated course gizmo. Move it and click again.");
            return;
        }
        Core.Primitives.Vector2 dest = new Core.Primitives.Vector2(courseTargetGizmo.position.x, courseTargetGizmo.position.z);
        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting SetCourse to {dest} for Escadre Entity {_escadreEntity.Id}");
        _clientConnection.RequestSetCourse(dest, Time.time); 
    }

    [ContextMenu("Escadre: Order Attack (using TargetEscadreEntityIdForAttack field)")]
    private void DebugOrderAttack()
    {
        if (_clientConnection == null || _escadreEntity == null || _escadreEntity.IsDead) { Logger.LogWarning("No client connection or active escadre."); return; }
        if (targetEscadreEntityIdForAttack < 0) { Logger.LogWarning("Invalid TargetEscadreEntityIdForAttack. Set it in Inspector."); return; }
        if (targetEscadreEntityIdForAttack == _escadreEntity.Id) { Logger.LogWarning("Cannot attack self."); return; }

        if (!_coreComposerRef.ServerLevel.TryGetEntity(targetEscadreEntityIdForAttack, out Entity targetEntity) || !(targetEntity is Escadre))
        {
             Logger.LogWarning($"Target Escadre Entity ID {targetEscadreEntityIdForAttack} does not exist or is not an Escadre.");
             return;
        }

        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting Attack on Escadre Entity {targetEscadreEntityIdForAttack} from Escadre {_escadreEntity.Id}");
        _clientConnection.RequestAttackEscadre(targetEscadreEntityIdForAttack, Time.time); 
    }

    [ContextMenu("Escadre: Cancel All Attack Orders")]
    private void DebugCancelAttack()
    {
        if (_clientConnection == null || _escadreEntity == null || _escadreEntity.IsDead) { Logger.LogWarning("No client connection or active escadre."); return; }
        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting CancelAttack for Escadre {_escadreEntity.Id}");
        _clientConnection.RequestCancelAttack();
    }

    [ContextMenu("Shop: Request Buy DefaultShip")]
    private void DebugBuyDefaultShip()
    {
        if (_clientConnection == null || _escadreEntity == null || _escadreEntity.IsDead) { Logger.LogWarning("No client connection or active escadre."); return; }
        if (_coreComposerRef == null) { Logger.LogWarning("CoreComposer reference missing."); return; }

        Logger.Log($"[ClientConnDebug {clientId_Display}] DEBUG: Requesting Buy DefaultShip for Escadre {_escadreEntity.Id}.");
        
        // Find a design ID for DefaultShip
        var defaultDesign = _coreComposerRef.ServerLevel.GameShop.AvailableShipDesigns
                              .FirstOrDefault(d => d.ShipEntityType == Entity.EntityTypeEnum.DefaultShip);
        if (defaultDesign == null)
        {
            Logger.LogError($"[ClientConnDebug {clientId_Display}] No DefaultShip design found in shop!");
            return;
        }

        // Simple offset for new ship
        Core.Primitives.Vector2 offset = new Core.Primitives.Vector2(Random.Range(-2f, 2f), Random.Range(2f, 5f) + _escadreEntity.ShipEntityIds.Count * 1.5f);
        
        _clientConnection.RequestBuyShip(defaultDesign.DesignId, offset, Time.time);
    }

    [ContextMenu("Shop: Request Upgrade Ship (using ShipIdForUpgrade field)")]
    private void DebugUpgradeShipById()
    {
        if (_clientConnection == null || _escadreEntity == null || _escadreEntity.IsDead) { Logger.LogWarning("No client connection or active escadre."); return; }
        if (shipIdForUpgrade < 0) { Logger.LogWarning("Invalid ShipIdForUpgrade. Set it in Inspector."); return; }

        if (!_escadreEntity.ShipEntityIds.Contains(shipIdForUpgrade))
        {
            Logger.LogWarning($"Ship ID {shipIdForUpgrade} not found in escadre {_escadreEntity.Id} (Owner: {clientId_Display}).");
            return;
        }

        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting UpgradeShip for Ship ID {shipIdForUpgrade} in Escadre {_escadreEntity.Id}");
        _clientConnection.RequestUpgradeShip(shipIdForUpgrade);
    }

    [ContextMenu("Shop: Request Upgrade First Ship in Escadre")]
    private void DebugUpgradeFirstShip()
    {
        if (_clientConnection == null || _escadreEntity == null || _escadreEntity.IsDead) { Logger.LogWarning("No client connection or active escadre."); return; }
        if (!_escadreEntity.ShipEntityIds.Any()) { Logger.LogWarning($"Escadre {_escadreEntity.Id} has no ships to upgrade."); return; }

        int firstShipId = _escadreEntity.ShipEntityIds.First();
        Logger.Log($"[ClientConnDebug {clientId_Display}] Requesting UpgradeShip for first ship (ID {firstShipId}) in Escadre {_escadreEntity.Id}");
        _clientConnection.RequestUpgradeShip(firstShipId);
    }
     public ClientConnection GetClientConnection() => _clientConnection;
}