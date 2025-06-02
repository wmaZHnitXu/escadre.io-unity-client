// File: Scripts/Server/Debug/EscadreDebugBehaviour.cs
using UnityEngine;
using Core.Model;
using CorePrims = Core.Primitives; // Alias for Core.Primitives
using System.Linq; 
using Logger = Core.Logging.Logger;
using System.Reflection; // For accessing private fields for debug

public class EscadreDebugBehaviour : ModelEntityDebugBehaviour
{
    [Header("Escadre Specific Info")]
    [SerializeField, ReadOnly] protected int ownerClientId_Display;
    [SerializeField, ReadOnly] protected string nickname_Display;
    [SerializeField, ReadOnly] protected int resources_Display;
    [SerializeField, ReadOnly] protected int shipCount_Display;
    [SerializeField, ReadOnly] protected string currentDestination_Display; // The ultimate fixed point destination
    [SerializeField, ReadOnly] protected CorePrims.Vector3 activeCommandTargetWorld_Display; // Ultimate target (dest or enemy)
    [SerializeField, ReadOnly] protected CorePrims.Vector3 currentFormationAnchorWorld_Display; // The moving anchor point
    [SerializeField, ReadOnly] protected bool isAnchorMoving_Display; // If the anchor is moving
    [SerializeField, ReadOnly] protected float currentFleetSpeed_Display;
    [SerializeField, ReadOnly] protected float fleetMaxSpeed_Display;
    [SerializeField, ReadOnly] protected string targetEscadreIds_Display;
    [SerializeField, ReadOnly] protected float formationScale_Display; 

    [Header("Gizmo Settings")]
    public bool showActiveCommandTargetGizmo = true; // Renamed
    public Color activeCommandTargetColor = Color.red; // Renamed
    public bool showFormationAnchorGizmo = true; // New
    public Color formationAnchorColor = Color.magenta; // New
    public bool showShipSlotTargetsGizmo = true;
    public Color shipSlotLineColor = Color.cyan;
    public Color actualShipToSlotLineColor = Color.yellow; 

    [Header("Debug Controls")]
    public float formationScaleIncrement = 0.1f;


    protected Escadre TargetEscadre => _targetEntity as Escadre;

    // Reflection fields for private Escadre state
    private FieldInfo _activeCommandTargetWorldField;
    private FieldInfo _currentFormationAnchorWorldField;
    private FieldInfo _isAnchorMovingToCommandTargetField;


    public override void Initialize(Entity entity)
    {
        if (entity is Escadre escadre)
        {
            base.Initialize(escadre);
            // Get reflection info for private fields
            var escadreType = typeof(Escadre);
            _activeCommandTargetWorldField = escadreType.GetField("_activeCommandTargetWorld", BindingFlags.NonPublic | BindingFlags.Instance);
            _currentFormationAnchorWorldField = escadreType.GetField("_currentFormationAnchorWorld", BindingFlags.NonPublic | BindingFlags.Instance);
            _isAnchorMovingToCommandTargetField = escadreType.GetField("_isAnchorMovingToCommandTarget", BindingFlags.NonPublic | BindingFlags.Instance);

            if(_activeCommandTargetWorldField == null) Logger.LogError("[EscadreDebugBehaviour] Reflection failed for _activeCommandTargetWorld");
            if(_currentFormationAnchorWorldField == null) Logger.LogError("[EscadreDebugBehaviour] Reflection failed for _currentFormationAnchorWorld");
            if(_isAnchorMovingToCommandTargetField == null) Logger.LogError("[EscadreDebugBehaviour] Reflection failed for _isAnchorMovingToCommandTarget");

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
            formationScale_Display = TargetEscadre.FormationScale; 
            
            // Use reflection to get private state for display
            if (_activeCommandTargetWorldField != null)
                activeCommandTargetWorld_Display = (CorePrims.Vector3)_activeCommandTargetWorldField.GetValue(TargetEscadre);
            else activeCommandTargetWorld_Display = CorePrims.Vector3.Zero;

            if (_currentFormationAnchorWorldField != null)
                currentFormationAnchorWorld_Display = (CorePrims.Vector3)_currentFormationAnchorWorldField.GetValue(TargetEscadre);
            else currentFormationAnchorWorld_Display = CorePrims.Vector3.Zero;
            
            if (_isAnchorMovingToCommandTargetField != null)
                isAnchorMoving_Display = (bool)_isAnchorMovingToCommandTargetField.GetValue(TargetEscadre);
            else isAnchorMoving_Display = false;


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
            activeCommandTargetWorld_Display = CorePrims.Vector3.Zero;
            currentFormationAnchorWorld_Display = CorePrims.Vector3.Zero;
            isAnchorMoving_Display = false;
            currentFleetSpeed_Display = 0f;
            fleetMaxSpeed_Display = 0f;
            targetEscadreIds_Display = "N/A";
            formationScale_Display = 1.0f;
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos(); 

        if (TargetEscadre != null && !TargetEscadre.IsDead)
        {
            // Escadre.Position is the average of ships
            UnityEngine.Vector3 escadreAveragePosUnity = TargetEscadre.Position.ToUnityVector(); 
            
            // Get the *actual* current formation anchor and active command target for gizmos
            CorePrims.Vector3 currentFormationAnchorCore = currentFormationAnchorWorld_Display; // From reflected field
            UnityEngine.Vector3 currentFormationAnchorUnity = currentFormationAnchorCore.ToUnityVector();

            CorePrims.Vector3 activeCommandTargetCore = activeCommandTargetWorld_Display; // From reflected field
            UnityEngine.Vector3 activeCommandTargetUnity = activeCommandTargetCore.ToUnityVector();
            
            CorePrims.Quaternion fleetCoreOrientation = TargetEscadre.Rotation; // Escadre's overall orientation

            // Gizmo for the ultimate Active Command Target
            if (showActiveCommandTargetGizmo && isAnchorMoving_Display) // Only show if anchor is moving towards it
            {
                Gizmos.color = activeCommandTargetColor;
                Gizmos.DrawSphere(activeCommandTargetUnity, 0.6f); // Slightly larger
                Gizmos.DrawLine(currentFormationAnchorUnity, activeCommandTargetUnity); // Line from anchor to command target
                #if UNITY_EDITOR
                if(UnityEditor.Selection.activeGameObject == gameObject) 
                    UnityEditor.Handles.Label(activeCommandTargetUnity + UnityEngine.Vector3.up * 0.7f, "ActiveCmdTarget");
                #endif
            }

            // Gizmo for the Current Formation Anchor (the moving center)
            if (showFormationAnchorGizmo)
            {
                Gizmos.color = formationAnchorColor;
                Gizmos.DrawSphere(currentFormationAnchorUnity, 0.5f); 
                // Line from average ship position to the anchor
                Gizmos.DrawLine(escadreAveragePosUnity, currentFormationAnchorUnity); 
                #if UNITY_EDITOR
                if(UnityEditor.Selection.activeGameObject == gameObject) 
                    UnityEditor.Handles.Label(currentFormationAnchorUnity + UnityEngine.Vector3.up * 0.6f, "FormationAnchor");
                #endif
            }


            if (showShipSlotTargetsGizmo)
            {
                foreach (var slot in TargetEscadre.CurrentFormation.Slots)
                {
                    if (slot.ShipEntityId.HasValue)
                    {
                        CorePrims.Vector2 relativeCenteredOffset2D_Core = TargetEscadre.CurrentFormation.GetShipRelativeOffset(slot.ShipEntityId.Value);
                        CorePrims.Vector3 relativeScaledOffset3D_Core = new CorePrims.Vector3(
                            relativeCenteredOffset2D_Core.X * TargetEscadre.FormationScale, 
                            0f, 
                            relativeCenteredOffset2D_Core.Y * TargetEscadre.FormationScale
                        );
                        
                        // Slot positions are relative to the _currentFormationAnchorWorld, oriented by Escadre.Rotation
                        CorePrims.Vector3 worldOffsetFromAnchor_Core = fleetCoreOrientation * relativeScaledOffset3D_Core;
                        CorePrims.Vector3 targetShipWorldPosition_Core = currentFormationAnchorCore + worldOffsetFromAnchor_Core; 
                        
                        UnityEngine.Vector3 slotTargetUnity = targetShipWorldPosition_Core.ToUnityVector();
                        
                        Gizmos.color = shipSlotLineColor;
                        // Draw line from Formation Anchor to the slot target
                        Gizmos.DrawLine(currentFormationAnchorUnity, slotTargetUnity); 
                        Gizmos.DrawWireSphere(slotTargetUnity, 0.2f * TargetEscadre.FormationScale);
                        #if UNITY_EDITOR
                        if(UnityEditor.Selection.activeGameObject == gameObject)
                            UnityEditor.Handles.Label(slotTargetUnity + UnityEngine.Vector3.up * 0.3f * TargetEscadre.FormationScale, $"Slot_{slot.ShipEntityId.Value}");
                        #endif

                        if (TargetEscadre.Level.TryGetEntity(slot.ShipEntityId.Value, out Entity shipEntity) && shipEntity is Ship actualShip && !actualShip.IsDead)
                        {
                            Gizmos.color = actualShipToSlotLineColor;
                            Gizmos.DrawLine(actualShip.Position.ToUnityVector(), slotTargetUnity); 
                        }
                    }
                }
            }
            // Draw Escadre's main orientation (from its Rotation property) starting from the Formation Anchor
            Gizmos.color = Color.green;
            Gizmos.DrawLine(currentFormationAnchorUnity, currentFormationAnchorUnity + TargetEscadre.Rotation.ToUnityQuaternion() * UnityEngine.Vector3.forward * (3f * TargetEscadre.FormationScale) );
        }
    }

    [ContextMenu("Formation: Increase Scale")]
    private void DebugIncreaseFormationScale()
    {
        if (TargetEscadre == null || TargetEscadre.IsDead || TargetEscadre.Level == null) return;
        float newScale = TargetEscadre.FormationScale + formationScaleIncrement;
        TargetEscadre.SetFormationScale(newScale, TargetEscadre.Level.CurrentTime); 
        UpdateDebugInfo();
    }

    [ContextMenu("Formation: Decrease Scale")]
    private void DebugDecreaseFormationScale()
    {
        if (TargetEscadre == null || TargetEscadre.IsDead || TargetEscadre.Level == null) return;
        float newScale = Mathf.Max(0.1f, TargetEscadre.FormationScale - formationScaleIncrement); 
        TargetEscadre.SetFormationScale(newScale, TargetEscadre.Level.CurrentTime);
        UpdateDebugInfo();
    }

    [ContextMenu("Formation: Reset Scale to 1.0")]
    private void DebugResetFormationScale()
    {
        if (TargetEscadre == null || TargetEscadre.IsDead || TargetEscadre.Level == null) return;
        TargetEscadre.SetFormationScale(1.0f, TargetEscadre.Level.CurrentTime);
        UpdateDebugInfo();
    }
}