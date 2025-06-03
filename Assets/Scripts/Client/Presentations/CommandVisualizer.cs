// File: Client/Presentation/CommandVisualizer.cs
using UnityEngine;
using Core.Client;
using Core.Network.Proxies;
using System.Collections.Generic;
using System.Linq;
using Core.Logging;
using Core.Network; // For IClientProxy to avoid ambiguity
using Logger = Core.Logging.Logger;

namespace Client.Presentation
{
    public class CommandVisualizer : MonoBehaviour
    {
        private ClientComposer _clientComposer;
        private EscadreProxy.ClientProxy _localEscadreProxy;
        private ClientLevel _clientLevel;

        [Header("Line Prefabs")]
        [Tooltip("Prefab with a DashedLineRenderer for course lines.")]
        [SerializeField] private DashedLineRenderer courseLinePrefab;
        [Tooltip("Prefab with a DashedLineRenderer for attack lines.")]
        [SerializeField] private DashedLineRenderer attackLinePrefab;

        [Header("Line Appearance")]
        [SerializeField] private Color courseLineColor = Color.green;
        [SerializeField] private float courseLineWidth = 0.2f;
        [SerializeField] private Color attackLineColor = Color.red;
        [SerializeField] private float attackLineWidth = 0.2f;
        [SerializeField] private float lineYOffset = 0.2f;

        private DashedLineRenderer _activeCourseLine;
        private Dictionary<int, DashedLineRenderer> _activeAttackLines = new Dictionary<int, DashedLineRenderer>();
        
        // Master lists of all instantiated line renderers for proper cleanup
        private List<DashedLineRenderer> _courseLinePoolMasterList = new List<DashedLineRenderer>();
        private List<DashedLineRenderer> _attackLinePoolMasterList = new List<DashedLineRenderer>();

        private bool _isInitialized = false;

        public void Initialize(ClientComposer clientComposerInstance)
        {
            if (_isInitialized)
            {
                Logger.LogWarning("[CommandVisualizer] Already initialized.");
                return;
            }
            Logger.Log("[CommandVisualizer] Initialize BEGIN");

            _clientComposer = clientComposerInstance ?? throw new System.ArgumentNullException(nameof(clientComposerInstance));
            _clientLevel = _clientComposer.ClientLevel;

            if (courseLinePrefab == null || attackLinePrefab == null)
            {
                Logger.LogError("[CommandVisualizer] Line prefabs not assigned! Visualization will fail.");
                enabled = false; // Disable component if it can't function
                return;
            }
            if (_clientLevel == null)
            {
                Logger.LogError("[CommandVisualizer] ClientLevel is null! Initialization failed.");
                enabled = false;
                return;
            }

            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
            // Initial call with current proxy (which might be null at this stage)
            HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy); 

            _isInitialized = true;
            Logger.Log("[CommandVisualizer] Initialize END - IsInitialized: true");
        }

        private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
        {
            Logger.Log($"[CommandVisualizer] HandleLocalEscadreProxyChanged called. New proxy ID: {(newProxy != null ? newProxy.EntityId.ToString() : "null")}");
            if (_localEscadreProxy != null)
            {
                Logger.Log($"[CommandVisualizer] Unsubscribing from old proxy ID: {_localEscadreProxy.EntityId}");
                _localEscadreProxy.PositionChanged -= OnLocalEscadreTransformChanged_Position;
                _localEscadreProxy.RotationChanged -= OnLocalEscadreTransformChanged_Rotation;
                _localEscadreProxy.OnCurrentDestinationChanged -= OnProxyDestinationChanged;
                _localEscadreProxy.OnTargetEscadreEntityIdsChanged -= OnProxyAttackTargetsChanged;
            }

            _localEscadreProxy = newProxy;
            ClearAllLinesVisuals(); // Hide and pool existing lines

            if (_localEscadreProxy != null)
            {
                Logger.Log($"[CommandVisualizer] Subscribing to new proxy ID: {_localEscadreProxy.EntityId}. Current Pos: {_localEscadreProxy.Position}, Dest: {_localEscadreProxy.CurrentDestination?.ToString() ?? "null"}, Targets: {string.Join(",", _localEscadreProxy.TargetEscadreEntityIds)}");
                _localEscadreProxy.PositionChanged += OnLocalEscadreTransformChanged_Position;
                _localEscadreProxy.RotationChanged += OnLocalEscadreTransformChanged_Rotation;
                _localEscadreProxy.OnCurrentDestinationChanged += OnProxyDestinationChanged;
                _localEscadreProxy.OnTargetEscadreEntityIdsChanged += OnProxyAttackTargetsChanged;
                
                // Initial draw based on the new proxy's current state
                Logger.Log($"[CommandVisualizer] Proxy assigned. Initial call to UpdateCourseLine for proxy {_localEscadreProxy.EntityId}.");
                UpdateCourseLine();
                Logger.Log($"[CommandVisualizer] Proxy assigned. Initial call to UpdateAttackLines for proxy {_localEscadreProxy.EntityId}.");
                UpdateAttackLines();
            }
            else
            {
                Logger.Log("[CommandVisualizer] LocalEscadreProxy is null after HandleLocalEscadreProxyChanged. No lines will be drawn.");
            }
        }
        
        private void OnProxyDestinationChanged()
        {
            if(!_isInitialized) return;
            if (_localEscadreProxy == null) { Logger.LogWarning("[CommandVisualizer EVENT] OnProxyDestinationChanged called but _localEscadreProxy is null."); return; }
            Logger.Log($"[CommandVisualizer EVENT] OnProxyDestinationChanged! Proxy ID: {_localEscadreProxy.EntityId}, New Dest: {_localEscadreProxy.CurrentDestination?.ToString() ?? "null"}. Calling UpdateCourseLine.");
            UpdateCourseLine();
        }

        private void OnProxyAttackTargetsChanged()
        {
            if(!_isInitialized) return;
            if (_localEscadreProxy == null) { Logger.LogWarning("[CommandVisualizer EVENT] OnProxyAttackTargetsChanged called but _localEscadreProxy is null."); return; }
            Logger.Log($"[CommandVisualizer EVENT] OnProxyAttackTargetsChanged! Proxy ID: {_localEscadreProxy.EntityId}, New Targets: {string.Join(",", _localEscadreProxy.TargetEscadreEntityIds)}. Calling UpdateAttackLines.");
            UpdateAttackLines();
        }

        private void OnLocalEscadreTransformChanged_Position(Core.Primitives.Vector3 newPosition)
        {
            if(!_isInitialized || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed) return;
            // Logger.Log($"[CommandVisualizer EVENT] OnLocalEscadreTransformChanged (Pos: {newPosition}). Calling UpdateAllLineStartPoints.");
            UpdateAllLineStartPoints();
        }
        private void OnLocalEscadreTransformChanged_Rotation(Core.Primitives.Quaternion newRotation)
        {
             if(!_isInitialized || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed) return;
            // Logger.Log($"[CommandVisualizer EVENT] OnLocalEscadreTransformChanged (Rot). Calling UpdateAllLineStartPoints.");
            UpdateAllLineStartPoints();
        }

        void Update() 
        {
            if (!_isInitialized || _localEscadreProxy == null || _clientLevel == null || _localEscadreProxy.IsDestroyed) return;

            if (_activeAttackLines.Any())
            {
                // If target escadres positions are updated frequently, this ensures attack lines follow them.
                // This could be optimized by subscribing to individual target EscadreProxy.PositionChanged events,
                // but that adds complexity. For now, a periodic update or update driven by local escadre movement is okay.
                 if (Time.frameCount % 3 == 0) // Reduced frequency for less log spam if target following is jittery
                 {
                    UpdateAttackLines();
                 }
            }
        }
        
        private void UpdateAllLineStartPoints()
        {
            if (!_isInitialized || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed)
            {
                return;
            }
            
            UnityEngine.Vector3 startPos = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;

            if (_activeCourseLine != null && _activeCourseLine.gameObject.activeSelf)
            {
                 if (_localEscadreProxy.CurrentDestination.HasValue)
                 {
                    UnityEngine.Vector3 endPos = new UnityEngine.Vector3(
                        _localEscadreProxy.CurrentDestination.Value.X,
                        startPos.y, 
                        _localEscadreProxy.CurrentDestination.Value.Y
                    );
                    _activeCourseLine.SetPoints(startPos, endPos);
                 }
                 // If CurrentDestination is null, OnProxyDestinationChanged -> UpdateCourseLine should have hidden it.
            }

            List<int> keysToUpdate = new List<int>(_activeAttackLines.Keys); 
            foreach(var targetId in keysToUpdate)
            {
                if (_activeAttackLines.TryGetValue(targetId, out var line) && line.gameObject.activeSelf)
                {
                    if (_clientLevel.TryGetProxy(targetId, out Core.Network.IClientProxy targetIProxy) && 
                        targetIProxy is EscadreProxy.ClientProxy targetEscadreProxy &&
                        !targetEscadreProxy.IsDestroyed)
                    {
                        UnityEngine.Vector3 targetPosUnity = targetEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                        line.SetPoints(startPos, targetPosUnity);
                    }
                    // If target is no longer valid, OnProxyAttackTargetsChanged -> UpdateAttackLines should remove the line.
                }
            }
        }

        private void UpdateCourseLine()
        {
            if (!_isInitialized) { Logger.LogWarning("[CommandVisualizer UpdateCourseLine] Called but not initialized."); return; }
            if (_localEscadreProxy == null)
            {
                Logger.Log("[CommandVisualizer UpdateCourseLine] _localEscadreProxy is null. Ensuring course line is hidden.");
                if (_activeCourseLine != null)
                {
                    ReturnLineToPool(_activeCourseLine, true); // true for course line
                    _activeCourseLine = null;
                }
                return;
            }
            
            //Logger.Log($"[CommandVisualizer UpdateCourseLine] For Proxy ID: {_localEscadreProxy.EntityId}. CurrentDestination.HasValue: {_localEscadreProxy.CurrentDestination.HasValue}. CurrentDest: {_localEscadreProxy.CurrentDestination?.ToString() ?? "NULL"}");

            if (_localEscadreProxy.CurrentDestination.HasValue)
            {
                if (_activeCourseLine == null)
                {
                    _activeCourseLine = GetPooledLine(true); 
                    _activeCourseLine.SetColor(courseLineColor);
                    _activeCourseLine.SetWidth(courseLineWidth);
                    //Logger.Log($"[CV UpdateCourseLine] Got new/pooled course line: {_activeCourseLine.name} for proxy {_localEscadreProxy.EntityId}");
                }
                _activeCourseLine.Show(); // Ensure it's shown
                UnityEngine.Vector3 startPos = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                UnityEngine.Vector3 endPos = new UnityEngine.Vector3(
                    _localEscadreProxy.CurrentDestination.Value.X,
                    startPos.y, 
                    _localEscadreProxy.CurrentDestination.Value.Y
                );
                _activeCourseLine.SetPoints(startPos, endPos);
                //Logger.Log($"[CV UpdateCourseLine] SHOWING Course Line for Proxy {_localEscadreProxy.EntityId} from {startPos} to {endPos}. Line: {_activeCourseLine.name}");
            }
            else // No destination
            {
                if (_activeCourseLine != null)
                {
                    Logger.Log($"[CV UpdateCourseLine] HIDING Course Line for Proxy {_localEscadreProxy.EntityId} as destination is null. Line: {_activeCourseLine.name}");
                    ReturnLineToPool(_activeCourseLine, true);
                    _activeCourseLine = null;
                } else {
                    Logger.Log($"[CV UpdateCourseLine] No destination for Proxy {_localEscadreProxy.EntityId}, and no active course line to hide.");
                }
            }
        }

        private void UpdateAttackLines()
        {
            if (!_isInitialized) { Logger.LogWarning("[CommandVisualizer UpdateAttackLines] Called but not initialized."); return; }

            if (_localEscadreProxy == null || _clientLevel == null || _localEscadreProxy.IsDestroyed)
            {
                Logger.LogWarning($"[CV UpdateAttackLines] Bailing: Null proxy, null clientLevel, or proxy destroyed. Clearing visuals.");
                ClearAllAttackLinesVisuals();
                return;
            }

            UnityEngine.Vector3 localEscadrePosUnity = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
            var currentTargetIds = new HashSet<int>(_localEscadreProxy.TargetEscadreEntityIds);
            Logger.Log($"[CV UpdateAttackLines] For Proxy ID: {_localEscadreProxy.EntityId}. StartPos: {localEscadrePosUnity}. Target IDs from proxy: [{string.Join(",", currentTargetIds)}]. Currently active attack lines: [{string.Join(",", _activeAttackLines.Keys)}]");
            
            var linesToRemoveKeys = new List<int>(_activeAttackLines.Keys.Except(currentTargetIds));
            foreach (int targetIdToRemove in linesToRemoveKeys)
            {
                if (_activeAttackLines.TryGetValue(targetIdToRemove, out DashedLineRenderer lineToReturn))
                {
                    Logger.Log($"[CV UpdateAttackLines] Removing attack line to target {targetIdToRemove} (no longer in proxy's list). Line: {lineToReturn.name}");
                    ReturnLineToPool(lineToReturn, false); // false for attack line
                    _activeAttackLines.Remove(targetIdToRemove);
                }
            }

            foreach (int targetId in currentTargetIds)
            {
                if (_clientLevel.TryGetProxy(targetId, out Core.Network.IClientProxy targetEscadreIProxy) &&
                    targetEscadreIProxy is EscadreProxy.ClientProxy targetEscadreProxy &&
                    !targetEscadreProxy.IsDestroyed)
                {
                    DashedLineRenderer line;
                    if (!_activeAttackLines.TryGetValue(targetId, out line))
                    {
                        line = GetPooledLine(false); 
                        line.SetColor(attackLineColor);
                        line.SetWidth(attackLineWidth);
                        _activeAttackLines[targetId] = line;
                        Logger.Log($"[CV UpdateAttackLines] Got new/pooled attack line for target {targetId}: {line.name}");
                    }
                    line.Show(); // Ensure it's shown
                    UnityEngine.Vector3 targetPosUnity = targetEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                    line.SetPoints(localEscadrePosUnity, targetPosUnity);
                    Logger.Log($"[CV UpdateAttackLines] SHOWING/UPDATING Attack Line from proxy {_localEscadreProxy.EntityId} to target {targetId} ({targetEscadreProxy.EntityType} at {targetPosUnity}). Line: {line.name}");
                }
                else 
                {
                    Logger.LogWarning($"[CV UpdateAttackLines] Target Escadre ID {targetId} from proxy list not found, destroyed, or invalid type in ClientLevel. Ensuring its line is removed.");
                    if (_activeAttackLines.TryGetValue(targetId, out DashedLineRenderer lineToReturn))
                    {
                        Logger.Log($"[CV UpdateAttackLines] Removing attack line to invalid/missing target {targetId}. Line: {lineToReturn.name}");
                        ReturnLineToPool(lineToReturn, false);
                        _activeAttackLines.Remove(targetId);
                    }
                }
            }
             if (!currentTargetIds.Any() && _activeAttackLines.Any()){
                 Logger.LogWarning($"[CV UpdateAttackLines] Proxy {_localEscadreProxy.EntityId} has NO targets, but _activeAttackLines is NOT empty. Should have been cleared by loop above.");
             } else if (!currentTargetIds.Any()){
                 Logger.Log($"[CV UpdateAttackLines] Proxy {_localEscadreProxy.EntityId} has NO targets. No attack lines to draw.");
             }
        }
        
        private DashedLineRenderer GetPooledLine(bool isCourseLine)
        {
            var masterPoolList = isCourseLine ? _courseLinePoolMasterList : _attackLinePoolMasterList;
            var prefab = isCourseLine ? courseLinePrefab : attackLinePrefab;

            DashedLineRenderer line = masterPoolList.FirstOrDefault(l => l != null && !l.gameObject.activeSelf);

            if (line == null)
            {
                if (prefab == null) { Logger.LogError($"[CV GetPooledLine] Prefab for {(isCourseLine ? "Course" : "Attack")} Line is NULL!"); return null; }
                line = Instantiate(prefab, transform); 
                line.name = $"{prefab.name}_PooledInstance_{masterPoolList.Count}";
                masterPoolList.Add(line); 
                Logger.Log($"[CV GetPooledLine] Instantiated NEW {(isCourseLine ? "Course" : "Attack")} Line: {line.name}. Master pool size: {masterPoolList.Count}");
            }
            else
            {
                // Logger.Log($"[CV GetPooledLine] Reusing {(isCourseLine ? "Course" : "Attack")} Line from pool: {line.name}");
            }
            
            line.gameObject.SetActive(true); 
            return line;
        }

        private void ReturnLineToPool(DashedLineRenderer line, bool isCourseLine)
        {
            if (line != null)
            {
                // Logger.Log($"[CV ReturnLineToPool] Returning {(isCourseLine ? "Course" : "Attack")} Line to pool: {line.name}");
                line.Hide(); 
                line.gameObject.SetActive(false); 
            }
        }

        private void ClearAllAttackLinesVisuals()
        {
            List<int> keys = new List<int>(_activeAttackLines.Keys);
            if (keys.Any()) Logger.Log($"[CV ClearAllAttackLinesVisuals] Clearing {keys.Count} active attack lines.");
            foreach (var key in keys)
            {
                if (_activeAttackLines.TryGetValue(key, out var line))
                {
                    ReturnLineToPool(line, false);
                }
            }
            _activeAttackLines.Clear();
        }
        
        private void ClearAllLinesVisuals()
        {
            if (_activeCourseLine != null)
            {
                Logger.Log($"[CV ClearAllLinesVisuals] Clearing active course line: {_activeCourseLine.name}");
                ReturnLineToPool(_activeCourseLine, true);
                _activeCourseLine = null;
            }
            ClearAllAttackLinesVisuals();
        }

        void OnDestroy()
        {
            Logger.Log("[CommandVisualizer] OnDestroy BEGIN");
            _isInitialized = false; // Prevent any further event-driven calls during destruction

            if (_clientComposer != null)
            {
                _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            }
            if (_localEscadreProxy != null)
            {
                // Attempt to unsubscribe, though proxy might already be gone or events cleared
                try { _localEscadreProxy.PositionChanged -= OnLocalEscadreTransformChanged_Position; } catch {}
                try { _localEscadreProxy.RotationChanged -= OnLocalEscadreTransformChanged_Rotation; } catch {}
                try { _localEscadreProxy.OnCurrentDestinationChanged -= OnProxyDestinationChanged; } catch {}
                try { _localEscadreProxy.OnTargetEscadreEntityIdsChanged -= OnProxyAttackTargetsChanged; } catch {}
            }

            ClearAllLinesVisuals();

            Logger.Log($"[CommandVisualizer] Destroying {_courseLinePoolMasterList.Count} course lines from master pool.");
            foreach (var line in _courseLinePoolMasterList) if (line != null) Destroy(line.gameObject);
            _courseLinePoolMasterList.Clear();
            
            Logger.Log($"[CommandVisualizer] Destroying {_attackLinePoolMasterList.Count} attack lines from master pool.");
            foreach (var line in _attackLinePoolMasterList) if (line != null) Destroy(line.gameObject);
            _attackLinePoolMasterList.Clear();
            
            _activeCourseLine = null;
            _activeAttackLines.Clear();

            Logger.Log("[CommandVisualizer] OnDestroy END");
        }
    }
}