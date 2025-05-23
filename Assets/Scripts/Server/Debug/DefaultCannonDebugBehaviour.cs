using UnityEngine;
using Core.Model;
using Logger = Core.Logging.Logger;

public class DefaultCannonDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("DefaultCannon Specific")]
    [SerializeField, ReadOnly] protected int ownerShipId_Display = -1;
    [SerializeField, ReadOnly] protected float attackRange_Display;
    [SerializeField, ReadOnly] protected float attackDamage_Display;
    [SerializeField, ReadOnly] protected float attackCooldown_Display;
    [SerializeField, ReadOnly] protected float currentAttackCooldownTimer_Display;
    [SerializeField, ReadOnly] protected Core.Primitives.Vector3 projectileSpawnOffset_Display;
    [SerializeField, ReadOnly] protected int currentTargetId_Display = -1;
    [SerializeField, ReadOnly] protected float currentYaw_Display;
    [SerializeField, ReadOnly] protected float currentPitch_Display;
    [SerializeField, ReadOnly] protected Core.Primitives.Quaternion baseLocalRotationOffset_Display;

    protected DefaultCannon TargetDefaultCannon => _targetEntity as DefaultCannon;

    public override void Initialize(Entity entity)
    {
        if (entity is DefaultCannon defaultCannon)
        {
            base.Initialize(defaultCannon);
        }
        else
        {
            Logger.LogError($"[DefaultCannonDebugBehaviour] Incorrect entity type: {entity?.GetType().Name}. Expected DefaultCannon.");
            _targetEntity = null;
            enabled = false;
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo(); // Updates common fields like Id, Position, Rotation
        if (TargetDefaultCannon != null && !TargetDefaultCannon.IsDead)
        {
            ownerShipId_Display = TargetDefaultCannon.Owner?.Id ?? -1;
            attackRange_Display = TargetDefaultCannon.AttackRange;
            attackDamage_Display = TargetDefaultCannon.AttackDamage;
            attackCooldown_Display = TargetDefaultCannon.AttackCooldown;
            // Accessing private _currentAttackCooldownTimer is not possible directly.
            // DefaultCannon would need a public getter or this debug class needs special access (e.g. via reflection or making it a friend class).
            // For now, we can't display it without modifying DefaultCannon. Let's assume 0 if not accessible or add a getter later.
            // currentAttackCooldownTimer_Display = TargetDefaultCannon._currentAttackCooldownTimer; // This won't compile
            currentAttackCooldownTimer_Display = -1f; // Placeholder
            
            projectileSpawnOffset_Display = TargetDefaultCannon.ProjectileSpawnOffset;
            currentTargetId_Display = TargetDefaultCannon.GetType().GetField("_currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(TargetDefaultCannon) is Entity currentTarget ? currentTarget.Id : -1;
            currentYaw_Display = TargetDefaultCannon.CurrentYaw;
            currentPitch_Display = TargetDefaultCannon.CurrentPitch;

            // Displaying _baseLocalRotationOffset (protected in Sentry)
            var baseLocalRotationField = typeof(Sentry<Ship>).GetField("_baseLocalRotationOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (baseLocalRotationField != null)
            {
                 baseLocalRotationOffset_Display = (Core.Primitives.Quaternion)baseLocalRotationField.GetValue(TargetDefaultCannon);
            }
            else { baseLocalRotationOffset_Display = Core.Primitives.Quaternion.Identity; }
        }
        else
        {
            ownerShipId_Display = -1;
            attackRange_Display = 0;
            attackDamage_Display = 0;
            attackCooldown_Display = 0;
            currentAttackCooldownTimer_Display = 0;
            projectileSpawnOffset_Display = Core.Primitives.Vector3.Zero;
            currentTargetId_Display = -1;
            currentYaw_Display = 0;
            currentPitch_Display = 0;
            baseLocalRotationOffset_Display = Core.Primitives.Quaternion.Identity;
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos(); // Draws entity label and position

        if (TargetDefaultCannon != null && !TargetDefaultCannon.IsDead)
        {
            // Convert Core.Primitives.Quaternion to UnityEngine.Quaternion for Gizmos
            UnityEngine.Quaternion cannonWorldRotation = new UnityEngine.Quaternion(
                TargetDefaultCannon.Rotation.X,
                TargetDefaultCannon.Rotation.Y,
                TargetDefaultCannon.Rotation.Z,
                TargetDefaultCannon.Rotation.W
            );
            UnityEngine.Vector3 cannonWorldPosition = new UnityEngine.Vector3(TargetDefaultCannon.Position.X, TargetDefaultCannon.Position.Y, TargetDefaultCannon.Position.Z);

            // Draw Attack Range
            Color attackRangeColor = Color.magenta;
            attackRangeColor.a = 0.1f;
            Gizmos.color = attackRangeColor;
            DrawWireDisk(cannonWorldPosition, TargetDefaultCannon.AttackRange, Color.magenta, 32);

            // Draw Forward Vector (Aiming Direction)
            Gizmos.color = Color.cyan;
            UnityEngine.Vector3 cannonForward = cannonWorldRotation * UnityEngine.Vector3.forward;
            Gizmos.DrawLine(cannonWorldPosition, cannonWorldPosition + cannonForward * 2f); // Line length 2 units

            // Draw Projectile Spawn Offset
            Core.Primitives.Vector3 pso = TargetDefaultCannon.ProjectileSpawnOffset;
            Core.Primitives.Vector3 psoWorld = TargetDefaultCannon.Rotation * pso;
            UnityEngine.Vector3 psoWorldUnity = new UnityEngine.Vector3(cannonWorldPosition.x + psoWorld.X, cannonWorldPosition.y + psoWorld.Y, cannonWorldPosition.z + psoWorld.Z);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(psoWorldUnity, 0.1f);
            Gizmos.DrawLine(cannonWorldPosition, psoWorldUnity);

            // TODO: Draw Firing Arc (Yaw/Pitch limits) - More complex, involves creating a partial sphere segment or lines
        }
    }

    // Helper from DefaultShipDebugBehaviour, ensure it's accessible or duplicated if this class is in a different assembly/namespace without access.
    // For simplicity, duplicating it here. Ideally, this would be in a shared GizmoUtils class.
    private static void DrawWireDisk(UnityEngine.Vector3 position, float radius, Color color, int segments = 32)
    {
        if (radius <= 0 || segments <= 2) return;
        Color oldColor = Gizmos.color;
        Gizmos.color = color;
        float angleStep = 360.0f / segments;
        UnityEngine.Vector3 prevPoint = position + UnityEngine.Quaternion.Euler(0, 0, 0) * UnityEngine.Vector3.forward * radius;
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