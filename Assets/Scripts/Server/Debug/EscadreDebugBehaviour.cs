// File: Scripts/Server/Debug/EscadreDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using CorePrims = Core.Primitives; // Alias for Core.Primitives
using System.Linq; 
using Logger = Core.Logging.Logger;

public class EscadreDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("Escadre Specific Info")]
    [SerializeField, ReadOnly] protected int ownerClientId_Display;
    [SerializeField, ReadOnly] protected string nickname_Display;
    [SerializeField, ReadOnly] protected int resources_Display;
    [SerializeField, ReadOnly] protected int shipCount_Display;
    [SerializeField, ReadOnly] protected string currentDestination_Display;
    [SerializeField, ReadOnly] protected CorePrims.Vector3 fleetCommandTargetPoint_Display;
    [SerializeField, ReadOnly] protected bool isFleetMovingToTarget_Display;
    [SerializeField, ReadOnly] protected float currentFleetSpeed_Display;
    [SerializeField, ReadOnly] protected float fleetMaxSpeed_Display;
    [SerializeField, ReadOnly] protected string targetEscadreIds_Display;


    [Header("Gizmo Settings")]
    public bool showFleetCommandTargetGizmo = true;
    public Color fleetCommandTargetColor = Color.magenta;
    public bool showShipSlotTargetsGizmo = true;
    public Color shipSlotLineColor = Color.cyan;

    protected Escadre TargetEscadre => _targetEntity as Escadre;

    public override void Initialize(Entity entity)
    {
        if (entity is Escadre escadre)
        {
            base.Initialize(escadre);
        }
        else
        {
            Logger.LogError($"[EscadreDebugBehaviour] Incorrect entity type: {entity?.GetType().Name}. Expected Escadre.");
            _targetEntity = null;
            enabled = false;
        }
    }

    protected override void UpdateDebugInfo()
    {
        base.UpdateDebugInfo();
        if (TargetEscadre != null && !TargetEscadre.IsDead)
        {
            ownerClientId_Display = TargetEscadre.OwnerClientId;
            nickname_Display = TargetEscadre.Nickname;
            resources_Display = TargetEscadre.Resources;
            shipCount_Display = TargetEscadre.ShipEntityIds.Count;
            currentDestination_Display = TargetEscadre.CurrentDestination?.ToString() ?? "None";
            
            var fleetCmdTargetField = typeof(Escadre).GetField("_fleetCommandTargetPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fleetCmdTargetField != null) fleetCommandTargetPoint_Display = (CorePrims.Vector3)fleetCmdTargetField.GetValue(TargetEscadre);
            else fleetCommandTargetPoint_Display = CorePrims.Vector3.Zero;
            
            var isMovingField = typeof(Escadre).GetField("_isFleetMovingToTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isMovingField != null) isFleetMovingToTarget_Display = (bool)isMovingField.GetValue(TargetEscadre);
            else isFleetMovingToTarget_Display = false;

            currentFleetSpeed_Display = TargetEscadre.CurrentFleetSpeed;
            fleetMaxSpeed_Display = TargetEscadre.FleetMaxSpeed;
            targetEscadreIds_Display = TargetEscadre.TargetEscadreEntityIds.Any() ? string.Join(",", TargetEscadre.TargetEscadreEntityIds) : "None";

        }
        else
        {
            ownerClientId_Display = -1;
            nickname_Display = "N/A";
            resources_Display = 0;
            shipCount_Display = 0;
            currentDestination_Display = "N/A";
            fleetCommandTargetPoint_Display = CorePrims.Vector3.Zero;
            isFleetMovingToTarget_Display = false;
            currentFleetSpeed_Display = 0f;
            fleetMaxSpeed_Display = 0f;
            targetEscadreIds_Display = "N/A";
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos(); 

        if (TargetEscadre != null && !TargetEscadre.IsDead)
        {
            UnityEngine.Vector3 escadreWorldPosUnity = TargetEscadre.Position.ToUnityVector();

            if (showFleetCommandTargetGizmo)
            {
                Gizmos.color = fleetCommandTargetColor;
                UnityEngine.Vector3 cmdTargetUnity = fleetCommandTargetPoint_Display.ToUnityVector();
                Gizmos.DrawSphere(cmdTargetUnity, 0.5f);
                Gizmos.DrawLine(escadreWorldPosUnity, cmdTargetUnity);
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(cmdTargetUnity + UnityEngine.Vector3.up * 0.6f, "FleetCmdTarget");
                #endif
            }

            if (showShipSlotTargetsGizmo)
            {
                Gizmos.color = shipSlotLineColor;
                CorePrims.Quaternion fleetCoreOrientation = TargetEscadre.Rotation;

                UnityEngine.Vector3 currentFormationAnchorUnity;
                bool isMoving = isFleetMovingToTarget_Display; 
                CorePrims.Vector3 cmdTargetCore = fleetCommandTargetPoint_Display; 

                if (isMoving && (TargetEscadre.CurrentDestination.HasValue || TargetEscadre.TargetEscadreEntityIds.Any()))
                {
                    currentFormationAnchorUnity = cmdTargetCore.ToUnityVector();
                }
                else
                {
                    currentFormationAnchorUnity = escadreWorldPosUnity; 
                }


                foreach (var slot in TargetEscadre.CurrentFormation.Slots)
                {
                    if (slot.ShipEntityId.HasValue)
                    {
                        CorePrims.Vector3 relativeOffset3D_Core = new CorePrims.Vector3(slot.RelativeOffset.X, 0, slot.RelativeOffset.Y);
                        CorePrims.Vector3 worldOffsetFromAnchor_Core = fleetCoreOrientation * relativeOffset3D_Core;
                        CorePrims.Vector3 targetShipWorldPosition_Core = currentFormationAnchorUnity.ToCoreVector() + worldOffsetFromAnchor_Core; 
                        
                        UnityEngine.Vector3 slotTargetUnity = targetShipWorldPosition_Core.ToUnityVector();
                        Gizmos.DrawLine(escadreWorldPosUnity, slotTargetUnity); 
                        Gizmos.DrawWireSphere(slotTargetUnity, 0.2f);

                        if (TargetEscadre.Level.TryGetEntity(slot.ShipEntityId.Value, out Entity shipEntity) && shipEntity is Ship)
                        {
                            Gizmos.DrawLine(shipEntity.Position.ToUnityVector(), slotTargetUnity); 
                        }
                    }
                }
            }
            Gizmos.color = Color.green;
            Gizmos.DrawLine(escadreWorldPosUnity, escadreWorldPosUnity + TargetEscadre.Rotation.ToUnityQuaternion() * UnityEngine.Vector3.forward * 3f);
        }
    }
}