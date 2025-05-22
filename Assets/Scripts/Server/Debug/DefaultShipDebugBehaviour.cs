// File: Scripts/Server/Debug/DefaultShipDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Logger = Core.Logging.Logger;
// Removed Vector3 and Quaternion using alias as they are clear from context or full Core.Primitives path is used.

public class DefaultShipDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("DefaultShip Specific")]
    [SerializeField, ReadOnly] protected float currentSpeed;
    [SerializeField, ReadOnly] protected float maxSpeed; 
    [SerializeField, ReadOnly] protected float turnRate;
    [SerializeField, ReadOnly] protected float attackDamage;
    [SerializeField, ReadOnly] protected float attackRange;
    [SerializeField, ReadOnly] protected float attackCooldown;
    [SerializeField, ReadOnly] protected int owningEscadreId;

    // Removed movementTargetGizmo, will use actionTargetGizmo from base class

    protected DefaultShip TargetDefaultShip => _targetEntity as DefaultShip;

    public override void Initialize(Entity entity)
    {
        if (entity is DefaultShip defaultShip) {
            base.Initialize(defaultShip);
            // actionTargetGizmo is initialized by base.Initialize if assigned
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
            attackDamage = TargetDefaultShip.AttackDamage;
            attackRange = TargetDefaultShip.AttackRange;
            attackCooldown = TargetDefaultShip.AttackCooldown;
            owningEscadreId = TargetDefaultShip.OwningEscadreClientId;
        } else { 
            currentSpeed = 0; 
            // Optionally zero out other stats or let them hold last known value
            maxSpeed = 0; turnRate = 0; attackDamage = 0; attackRange = 0; attackCooldown = 0; owningEscadreId = -1;
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
        // Optionally hide the gizmo if it was only for movement target
        // if (actionTargetGizmo != null) actionTargetGizmo.gameObject.SetActive(false);
        Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget(null)");
        TargetDefaultShip.SetMovementTarget(null, Time.time); 
    }

    [ContextMenu("Call PerformUpgrade on Ship")]
    protected void CallPerformUpgrade() { 
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead) return; 
        TargetDefaultShip.PerformUpgrade(); 
    }
    
    // OnDrawGizmosSelected is removed to avoid conflict with base.OnDrawGizmos
    // Base class OnDrawGizmos will draw the actionTargetGizmo line.
    // We can add ship-specific gizmos here if needed, using OnDrawGizmos.
    protected override void OnDrawGizmos() 
    {
        base.OnDrawGizmos(); // Call base to draw action target gizmo if active

        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead) {
            // Draw Attack Range
            Color attackRangeColor = Color.red; 
            attackRangeColor.a = 0.1f; // More transparent fill
            Gizmos.color = attackRangeColor;
            // Unity's Handles.DrawSolidDisc could be used for a filled disc if in Editor namespace,
            // but for Gizmos, multiple lines or a wire disk is standard.
            // For simplicity, draw wire disk.
            DrawWireDisk(transform.position, TargetDefaultShip.AttackRange, Color.red, 32);

            // Draw Forward Vector
            Gizmos.color = Color.blue; // Different from base gizmo color for clarity
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f); // Slightly longer line
        }
    }

    private static void DrawWireDisk(UnityEngine.Vector3 position, float radius, Color color, int segments = 32) 
    {
        if (radius <= 0 || segments <= 2) return; 
        Color oldColor = Gizmos.color; 
        Gizmos.color = color;
        float angleStep = 360.0f / segments; 
        UnityEngine.Vector3 prevPoint = position + UnityEngine.Quaternion.Euler(0, 0, 0) * UnityEngine.Vector3.forward * radius;
        for (int i = 1; i <= segments; i++) { 
            float angle = i * angleStep; 
            UnityEngine.Vector3 nextPoint = position + UnityEngine.Quaternion.Euler(0, angle, 0) * UnityEngine.Vector3.forward * radius; 
            Gizmos.DrawLine(prevPoint, nextPoint); 
            prevPoint = nextPoint; 
        }
        Gizmos.color = oldColor;
    }
}