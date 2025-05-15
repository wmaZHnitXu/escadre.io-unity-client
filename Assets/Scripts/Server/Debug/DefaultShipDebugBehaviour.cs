// File: Scripts/Server/Debug/DefaultShipDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using Core.Primitives; // For Vector2
using Core.Logging;
using Logger = Core.Logging.Logger;

// Ensure this script is in a namespace accessible by your Unity project,
// typically outside 'Core' if 'Core' is a separate assembly.
// For example, ServerSpecific.Debug if you have such a structure.

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

    [Header("Movement Debug")]
    [Tooltip("Click 'Set Movement Target To This' to send the ship here.")]
    public Transform movementTargetGizmo; // Assign a simple visual (e.g., a sphere) in the editor

    protected DefaultShip TargetDefaultShip => _targetEntity as DefaultShip;

    public override void Initialize(Entity entity)
    {
        if (entity is DefaultShip defaultShip)
        {
            base.Initialize(defaultShip); // Call base ModelEntityDebugBehaviour initialization
            if (movementTargetGizmo != null)
            {
                movementTargetGizmo.gameObject.name = $"{gameObject.name}_MoveTargetGizmo";
                // Initially place gizmo at ship's current position or hide it
                movementTargetGizmo.position = transform.position;
                movementTargetGizmo.gameObject.SetActive(false);
            }
        }
        else
        {
            Logger.LogError($"[DefaultShipDebugBehaviour] Attempted to initialize with incorrect entity type: {entity?.GetType().Name ?? "null"}. Expected DefaultShip.");
            _targetEntity = null; // Prevent base class from operating on wrong type
            enabled = false;
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo(); // Updates common fields like corePosition, coreRotation, isDead

        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead)
        {
            currentSpeed = TargetDefaultShip.CurrentSpeed;
            maxSpeed = TargetDefaultShip.MaxSpeed;
            turnRate = TargetDefaultShip.TurnRate;
            attackDamage = TargetDefaultShip.AttackDamage;
            attackRange = TargetDefaultShip.AttackRange;
            attackCooldown = TargetDefaultShip.AttackCooldown; // This is the config, not current cooldown
            owningEscadreId = TargetDefaultShip.OwningEscadreClientId;
        }
        else
        {
            // Clear or default ship-specific fields if dead or null
            currentSpeed = 0;
            // Keep last known stats or zero them out
        }
    }

    protected override void Update() // Override Update to also manage gizmo visibility
    {
        base.Update(); // Handles following entity and updating common debug info

        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead && movementTargetGizmo != null)
        {
            // Check if the ship has an internal movement target from the Core.Model.Ship
            // This requires exposing _movementTargetPosition from Ship or having a getter.
            // For now, we'll assume if the gizmo is active, it represents a debug-set target.
            // A better way would be to reflect the actual _movementTargetPosition.
        }
    }


    // --- Context Menu Actions for DefaultShip ---

    [ContextMenu("Set Ship Movement Target To Gizmo Position")]
    protected void SetShipMovementTargetToGizmo()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead)
        {
            Logger.LogWarning($"[DefaultShipDebugBehaviour:{entityId}] Ship is null or dead.");
            return;
        }
        if (movementTargetGizmo == null)
        {
            Logger.LogWarning($"[DefaultShipDebugBehaviour:{entityId}] Movement Target Gizmo not assigned.");
            return;
        }

        if (!movementTargetGizmo.gameObject.activeSelf)
        {
            // If gizmo wasn't active, activate and place it at a default offset for convenience
            movementTargetGizmo.position = transform.position + transform.forward * 10f; // 10 units in front
            movementTargetGizmo.gameObject.SetActive(true);
            Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Activated and positioned movement gizmo. Click again to set target.");
            return;
        }

        Core.Primitives.Vector2 targetPos2D = new Core.Primitives.Vector2(
            movementTargetGizmo.position.x,
            movementTargetGizmo.position.z // Movement is on XZ plane
        );

        Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget({targetPos2D})");
        TargetDefaultShip.SetMovementTarget(targetPos2D);
        // The ship's UpdateMovement logic will then take over.
    }

    [ContextMenu("Clear Ship Movement Target")]
    protected void ClearShipMovementTarget()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead)
        {
            Logger.LogWarning($"[DefaultShipDebugBehaviour:{entityId}] Ship is null or dead.");
            return;
        }
        if (movementTargetGizmo != null)
        {
            movementTargetGizmo.gameObject.SetActive(false);
        }

        Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Calling Ship.SetMovementTarget(null)");
        TargetDefaultShip.SetMovementTarget(null);
    }

    [ContextMenu("Call PerformUpgrade on Ship")]
    protected void CallPerformUpgrade()
    {
        if (TargetDefaultShip == null || TargetDefaultShip.IsDead)
        {
            Logger.LogWarning($"[DefaultShipDebugBehaviour:{entityId}] Ship is null or dead.");
            return;
        }
        Logger.Log($"[DefaultShipDebugBehaviour:{entityId}] Requesting PerformUpgrade for Ship {TargetDefaultShip.Id}");
        TargetDefaultShip.PerformUpgrade();
        // UpdateDebugInfo(); // Refresh displayed stats immediately if desired, or wait for next Update
    }


    // Optional: Draw Gizmos in Scene view for better visualization
    protected virtual void OnDrawGizmosSelected() // Only draws when this GameObject is selected
    {
        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead && movementTargetGizmo != null && movementTargetGizmo.gameObject.activeSelf)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, movementTargetGizmo.position);
            Gizmos.DrawWireSphere(movementTargetGizmo.position, 0.5f); // Small sphere at target
        }

        if (TargetDefaultShip != null && !TargetDefaultShip.IsDead)
        {
            // Draw Attack Range
            Color attackRangeColor = Color.red;
            attackRangeColor.a = 0.3f; // Transparent
            Gizmos.color = attackRangeColor;
            DrawWireDisk(transform.position, TargetDefaultShip.AttackRange, Color.red);

            // Draw Forward Vector (helpful for orientation)
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f); // Length of 2 units
        }
    }
     // Helper to draw a flat disk on XZ plane
    private static void DrawWireDisk(UnityEngine.Vector3 position, float radius, Color color, int segments = 32)
    {
        if (radius <= 0 || segments <= 2) return;
        Color oldColor = Gizmos.color;
        Gizmos.color = color;
        float angleStep = 360.0f / segments;
        UnityEngine.Vector3 prevPoint = position + UnityEngine.Quaternion.Euler(0, 0, 0) * UnityEngine.Vector3.forward * radius; // Start point

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            UnityEngine.Vector3 nextPoint = position + UnityEngine.Quaternion.Euler(0, angle, 0) * UnityEngine.Vector3.forward * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
        Gizmos.color = oldColor;
    }
}