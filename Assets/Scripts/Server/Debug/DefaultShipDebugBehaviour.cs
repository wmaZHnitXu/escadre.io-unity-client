// File: Scripts/Server/Debug/DefaultShipDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Logger = Core.Logging.Logger;
// Removed Vector3 and Quaternion using alias as they are clear from context or full Core.Primitives path is used.

public class DefaultShipDebugBehaviour : DestructibleEntityDebugBehaviour
{
    [Header("DefaultShip Specific")]
    [SerializeField, ReadOnly] protected float currentSpeed;
    [SerializeField, ReadOnly] protected float maxSpeed; 
    [SerializeField, ReadOnly] protected float turnRate;
    [SerializeField, ReadOnly] protected int owningEscadreClientId_Display; 
    [SerializeField, ReadOnly] protected int owningEscadreEntityId_Display = -1;
    [SerializeField, ReadOnly] protected int cannonCount_Display = 0;
    [SerializeField, ReadOnly] protected float collectableDetectionRange_Display; // Added


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
            currentSpeed = TargetDefaultShip.CurrentSpeed;
            maxSpeed = TargetDefaultShip.MaxSpeed; 
            turnRate = TargetDefaultShip.TurnRate;
            owningEscadreClientId_Display = TargetDefaultShip.OwningEscadreClientId;
            owningEscadreEntityId_Display = TargetDefaultShip.OwningEscadre?.Id ?? -1; 
            cannonCount_Display = TargetDefaultShip.Cannons?.Count ?? 0;
            collectableDetectionRange_Display = TargetDefaultShip.CollectableDetectionRange; // Added
        } else { 
            currentSpeed = 0; 
            maxSpeed = 0; turnRate = 0; 
            owningEscadreClientId_Display = -1;
            owningEscadreEntityId_Display = -1;
            cannonCount_Display = 0;
            collectableDetectionRange_Display = 0; // Added
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
            Gizmos.color = Color.blue; 
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f); 

            // Draw Collectable Detection Range
            Color detectionRangeColor = new Color(0.8f, 0.5f, 0.2f, 0.1f); // Orange, semi-transparent
            Gizmos.color = detectionRangeColor;
            Gizmos.DrawSphere(TargetDefaultShip.Position.ToUnityVector(), TargetDefaultShip.CollectableDetectionRange);
            
            Gizmos.color = new Color(0.8f, 0.5f, 0.2f, 0.6f); // Brighter wire for detection range
            Gizmos.DrawWireSphere(TargetDefaultShip.Position.ToUnityVector(), TargetDefaultShip.CollectableDetectionRange);
        }
    }
}