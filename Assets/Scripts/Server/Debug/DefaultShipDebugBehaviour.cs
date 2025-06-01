// File: Scripts/Server/Debug/DefaultShipDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Logger = Core.Logging.Logger;
using Core.Primitives; // For explicit Core.Primitives.Vector2 if needed

public class DefaultShipDebugBehaviour : DestructibleEntityDebugBehaviour
{
    [Header("DefaultShip Specific")]
    [SerializeField, ReadOnly] protected float currentSpeed_Display; // Renamed from currentSpeed
    [SerializeField, ReadOnly] protected float maxSpeed_Display; // Renamed from maxSpeed
    [SerializeField, ReadOnly] protected float turnRate_Display; // Renamed from turnRate
    [SerializeField, ReadOnly] protected int owningEscadreClientId_Display; 
    [SerializeField, ReadOnly] protected int owningEscadreEntityId_Display = -1;
    [SerializeField, ReadOnly] protected int cannonCount_Display = 0;
    [SerializeField, ReadOnly] protected float collectableDetectionRange_Display;
    [SerializeField, ReadOnly] protected Core.Primitives.Vector2 movementTarget_Display; // New: display movement target
    [SerializeField, ReadOnly] protected bool isShipMoving_Display; // New: display IsMoving flag

    [Header("Ship Gizmo Settings")]
    public bool showMovementTargetGizmo = true;
    public Color movementTargetLineColor = Color.yellow;


    protected DefaultShip TargetDefaultShip => _targetEntity as DefaultShip;

    public override void Initialize(Entity entity)
    {
        if (entity is DefaultShip defaultShip) {
            base.Initialize(defaultShip);
        } else {
            Logger.LogError($"[DefaultShipDebugBehaviour] Incorrect entity type: {entity?.GetType().Name}. Expected DefaultShip.");
            _targetEntity = null; enabled = false;
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo();
        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead) {
            currentSpeed_Display = TargetDefaultShip.CurrentSpeed;
            maxSpeed_Display = TargetDefaultShip.MaxSpeed; 
            turnRate_Display = TargetDefaultShip.TurnRate;
            owningEscadreClientId_Display = TargetDefaultShip.OwningEscadreClientId;
            owningEscadreEntityId_Display = TargetDefaultShip.OwningEscadre?.Id ?? -1; 
            cannonCount_Display = TargetDefaultShip.Cannons?.Count ?? 0;
            collectableDetectionRange_Display = TargetDefaultShip.CollectableDetectionRange;
            isShipMoving_Display = TargetDefaultShip.IsMoving;

            // Accessing private _movementTargetPosition for debug display
            var movementTargetField = typeof(Ship).GetField("_movementTargetPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (movementTargetField != null)
            {
                var mtValue = (Core.Primitives.Vector2?)movementTargetField.GetValue(TargetDefaultShip);
                movementTarget_Display = mtValue ?? Core.Primitives.Vector2.Zero; // Show Zero if null
            } else { movementTarget_Display = Core.Primitives.Vector2.Zero;}

        } else { 
            currentSpeed_Display = 0; 
            maxSpeed_Display = 0; turnRate_Display = 0; 
            owningEscadreClientId_Display = -1;
            owningEscadreEntityId_Display = -1;
            cannonCount_Display = 0;
            collectableDetectionRange_Display = 0;
            movementTarget_Display = Core.Primitives.Vector2.Zero;
            isShipMoving_Display = false;
        }
    }

    [ContextMenu("Set Ship Movement Target To ActionTargetGizmo Position")]
    protected void SetShipMovementTargetToGizmo()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead)
        {
            Logger.LogWarning($"[DefaultShipDebugBehaviour:{entityId}] Ship is null or dead.");
            return;
        }
        Core.Primitives.Vector2 targetPos2D = new Core.Primitives.Vector2(transform.position.x, transform.position.z);
        Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget({targetPos2D}) using ActionTargetGizmo.");
        TargetDefaultShip.SetMovementTarget(targetPos2D, Time.time); 
    }

    [ContextMenu("Clear Ship Movement Target")]
    protected void ClearShipMovementTarget()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead) { 
            Logger.LogWarning($"[DefaultShipDebugBehaviour:{entityId}] Ship is null or dead.");
            return; 
        }
        Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget(null)");
        TargetDefaultShip.SetMovementTarget(null, Time.time); 
    }

    [ContextMenu("Call PerformUpgrade on Ship")]
    protected void CallPerformUpgrade() { 
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead) return; 
        TargetDefaultShip.PerformUpgrade(); 
    }
    
    protected override void OnDrawGizmos() 
    {
        base.OnDrawGizmos(); 

        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead) {
            UnityEngine.Vector3 shipWorldPos = TargetDefaultShip.Position.ToUnityVector();
            Gizmos.color = Color.blue; 
            Gizmos.DrawLine(shipWorldPos, shipWorldPos + TargetDefaultShip.Rotation.ToUnityQuaternion() * UnityEngine.Vector3.forward * 3f); 

            Color detectionRangeColor = new Color(0.8f, 0.5f, 0.2f, 0.1f); 
            Gizmos.color = detectionRangeColor;
            Gizmos.DrawSphere(shipWorldPos, TargetDefaultShip.CollectableDetectionRange);
            
            Gizmos.color = new Color(0.8f, 0.5f, 0.2f, 0.6f); 
            Gizmos.DrawWireSphere(shipWorldPos, TargetDefaultShip.CollectableDetectionRange);

            if (showMovementTargetGizmo && movementTarget_Display != Core.Primitives.Vector2.Zero)
            {
                Gizmos.color = movementTargetLineColor;
                UnityEngine.Vector3 targetUnityPos = new UnityEngine.Vector3(movementTarget_Display.X, shipWorldPos.y, movementTarget_Display.Y); // Keep Y level for line
                Gizmos.DrawLine(shipWorldPos, targetUnityPos);
                Gizmos.DrawWireSphere(targetUnityPos, 0.3f);
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(targetUnityPos + UnityEngine.Vector3.up * 0.4f, "ShipTarget");
                #endif
            }
        }
    }
}