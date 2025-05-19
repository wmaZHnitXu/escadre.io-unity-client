// File: Scripts/Server/Debug/DefaultShipDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Logger = Core.Logging.Logger;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

public class DefaultShipDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("DefaultShip Specific")]
    [SerializeField, ReadOnly] protected float currentSpeed;
    [SerializeField, ReadOnly] protected float maxSpeed; // These are readonly for display
    // ... (other readonly stat fields)
    [SerializeField, ReadOnly] protected float turnRate;
    [SerializeField, ReadOnly] protected float attackDamage;
    [SerializeField, ReadOnly] protected float attackRange;
    [SerializeField, ReadOnly] protected float attackCooldown;
    [SerializeField, ReadOnly] protected int owningEscadreId;


    [Header("Movement Debug")]
    public Transform movementTargetGizmo;

    protected DefaultShip TargetDefaultShip => _targetEntity as DefaultShip;

    public override void Initialize(Entity entity)
    {
        if (entity is DefaultShip defaultShip) {
            base.Initialize(defaultShip);
            if (movementTargetGizmo != null) {
                movementTargetGizmo.gameObject.name = $"{gameObject.name}_MoveTargetGizmo";
                movementTargetGizmo.position = transform.position;
                movementTargetGizmo.gameObject.SetActive(false);
            }
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
            maxSpeed = TargetDefaultShip.MaxSpeed; // Accessing the property from Ship/DefaultShip
            turnRate = TargetDefaultShip.TurnRate;
            attackDamage = TargetDefaultShip.AttackDamage;
            attackRange = TargetDefaultShip.AttackRange;
            attackCooldown = TargetDefaultShip.AttackCooldown;
            owningEscadreId = TargetDefaultShip.OwningEscadreClientId;
        } else { currentSpeed = 0; /* other fields could be zeroed or kept */ }
    }
    // Update() method can remain as is, base.Update() handles following.

    [ContextMenu("Set Ship Movement Target To Gizmo Position")]
    protected void SetShipMovementTargetToGizmo()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead) { /* ... */ return; }
        if (movementTargetGizmo == null) { /* ... */ return; }
        if (!movementTargetGizmo.gameObject.activeSelf) {
            movementTargetGizmo.position = transform.position + transform.forward * 10f;
            movementTargetGizmo.gameObject.SetActive(true);
            Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Activated gizmo. Click again."); return;
        }
        Core.Primitives.Vector2 targetPos2D = new Core.Primitives.Vector2(movementTargetGizmo.position.x, movementTargetGizmo.position.z);
        // Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget({targetPos2D})");
        TargetDefaultShip.SetMovementTarget(targetPos2D, Time.time); // Pass serverTime (UnityEngine.Time.time for editor server)
    }

    [ContextMenu("Clear Ship Movement Target")]
    protected void ClearShipMovementTarget()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead) { /* ... */ return; }
        if (movementTargetGizmo != null) movementTargetGizmo.gameObject.SetActive(false);
        // Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget(null)");
        TargetDefaultShip.SetMovementTarget(null, Time.time); // Pass serverTime
    }

    // CallPerformUpgrade, OnDrawGizmosSelected, DrawWireDisk methods remain the same.
    [ContextMenu("Call PerformUpgrade on Ship")]
    protected void CallPerformUpgrade() { if (TargetDefaultShip == null || TargetDefaultShip.IsDead) return; TargetDefaultShip.PerformUpgrade(); }
    protected virtual void OnDrawGizmosSelected() {
        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead) {
            if (movementTargetGizmo != null && movementTargetGizmo.gameObject.activeSelf) { Gizmos.color = Color.blue; Gizmos.DrawLine(transform.position, movementTargetGizmo.position); Gizmos.DrawWireSphere(movementTargetGizmo.position, 0.5f); }
            Color attackRangeColor = Color.red; attackRangeColor.a = 0.3f; Gizmos.color = attackRangeColor; DrawWireDisk(transform.position, TargetDefaultShip.AttackRange, Color.red);
            Gizmos.color = Color.cyan; Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
    private static void DrawWireDisk(Vector3 position, float radius, Color color, int segments = 32) { /* ... */
        if (radius <= 0 || segments <= 2) return; Color oldColor = Gizmos.color; Gizmos.color = color;
        float angleStep = 360.0f / segments; Vector3 prevPoint = position + Quaternion.Euler(0, 0, 0) * Vector3.forward * radius;
        for (int i = 1; i <= segments; i++) { float angle = i * angleStep; Vector3 nextPoint = position + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius; Gizmos.DrawLine(prevPoint, nextPoint); prevPoint = nextPoint; }
        Gizmos.color = oldColor;
    }
}