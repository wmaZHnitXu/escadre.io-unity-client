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
        [Tooltip("IMPORTANT: Ensure this color is distinct in the Inspector (e.g., Green).")]
        [SerializeField] private Color courseLineColor = Color.green;
        [SerializeField] private float courseLineWidth = 0.2f;
        [Tooltip("IMPORTANT: Ensure this color is distinct in the Inspector (e.g., Red).")]
        [SerializeField] private Color attackLineColor = Color.red;
        [SerializeField] private float attackLineWidth = 0.2f;
        [SerializeField] private float lineYOffset = 0.2f;

        private DashedLineRenderer _activeCourseLine;
        private Dictionary<int, DashedLineRenderer> _activeAttackLines = new Dictionary<int, DashedLineRenderer>();
        
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
                enabled = false; 
                return;
            }
            if (_clientLevel == null)
            {
                Logger.LogError("[CommandVisualizer] ClientLevel is null! Initialization failed.");
                enabled = false;
                return;
            }
            
            // Important: Check Inspector values for colors
            if (courseLineColor == attackLineColor) {
                Logger.LogWarning($"[CommandVisualizer] Course line color and Attack line color are the same ({courseLineColor}). Ensure they are distinct in the Inspector for visual clarity.");
            }


            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
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
            ClearAllLinesVisuals(); 

            if (_localEscadreProxy != null)
            {
                Logger.Log($"[CommandVisualizer] Subscribing to new proxy ID: {_localEscadreProxy.EntityId}. Current Pos: {_localEscadreProxy.Position}, Dest: {_localEscadreProxy.CurrentDestination?.ToString() ?? "null"}, Targets: {string.Join(",", _localEscadreProxy.TargetEscadreEntityIds)}");
                _localEscadreProxy.PositionChanged += OnLocalEscadreTransformChanged_Position;
                _localEscadreProxy.RotationChanged += OnLocalEscadreTransformChanged_Rotation;
                _localEscadreProxy.OnCurrentDestinationChanged += OnProxyDestinationChanged;
                _localEscadreProxy.OnTargetEscadreEntityIdsChanged += OnProxyAttackTargetsChanged;
                
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
            UpdateAllLineStartPoints();
        }
        private void OnLocalEscadreTransformChanged_Rotation(Core.Primitives.Quaternion newRotation)
        {
             if(!_isInitialized || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed) return;
            UpdateAllLineStartPoints();
        }

        void Update() 
        {
            if (!_isInitialized || _localEscadreProxy == null || _clientLevel == null || _localEscadreProxy.IsDestroyed) return;

            if (_activeAttackLines.Any())
            {
                 if (Time.frameCount % 3 == 0) 
                 {
                    UpdateAttackLines(); // This will re-evaluate target positions and update line endpoints
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
                    ReturnLineToPool(_activeCourseLine, true); 
                    _activeCourseLine = null;
                }
                return;
            }
            
            if (_localEscadreProxy.CurrentDestination.HasValue)
            {
                if (_activeCourseLine == null)
                {
                    _activeCourseLine = GetPooledLine(true); 
                    // Ensure color and width are set using the Inspector-defined values
                    _activeCourseLine.SetColor(this.courseLineColor); 
                    _activeCourseLine.SetWidth(this.courseLineWidth);
                }
                _activeCourseLine.Show(); 
                UnityEngine.Vector3 startPos = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                UnityEngine.Vector3 endPos = new UnityEngine.Vector3(
                    _localEscadreProxy.CurrentDestination.Value.X,
                    startPos.y, 
                    _localEscadreProxy.CurrentDestination.Value.Y
                );
                _activeCourseLine.SetPoints(startPos, endPos);
            }
            else 
            {
                if (_activeCourseLine != null)
                {
                    ReturnLineToPool(_activeCourseLine, true);
                    _activeCourseLine = null;
                }
            }
        }

        private void UpdateAttackLines()
        {
            if (!_isInitialized) { Logger.LogWarning("[CommandVisualizer UpdateAttackLines] Called but not initialized."); return; }

            if (_localEscadreProxy == null || _clientLevel == null || _localEscadreProxy.IsDestroyed)
            {
                ClearAllAttackLinesVisuals();
                return;
            }

            UnityEngine.Vector3 localEscadrePosUnity = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
            var currentTargetIds = new HashSet<int>(_localEscadreProxy.TargetEscadreEntityIds);
            
            var linesToRemoveKeys = new List<int>(_activeAttackLines.Keys.Except(currentTargetIds));
            foreach (int targetIdToRemove in linesToRemoveKeys)
            {
                if (_activeAttackLines.TryGetValue(targetIdToRemove, out DashedLineRenderer lineToReturn))
                {
                    ReturnLineToPool(lineToReturn, false); 
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
                        // Ensure color and width are set using the Inspector-defined values
                        line.SetColor(this.attackLineColor); 
                        line.SetWidth(this.attackLineWidth);
                        _activeAttackLines[targetId] = line;
                    }
                    line.Show(); 
                    UnityEngine.Vector3 targetPosUnity = targetEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                    line.SetPoints(localEscadrePosUnity, targetPosUnity);
                }
                else 
                {
                    if (_activeAttackLines.TryGetValue(targetId, out DashedLineRenderer lineToReturn))
                    {
                        ReturnLineToPool(lineToReturn, false);
                        _activeAttackLines.Remove(targetId);
                    }
                }
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
            }
            
            line.gameObject.SetActive(true); 
            return line;
        }

        private void ReturnLineToPool(DashedLineRenderer line, bool isCourseLine)
        {
            if (line != null)
            {
                line.Hide(); 
                line.gameObject.SetActive(false); 
            }
        }

        private void ClearAllAttackLinesVisuals()
        {
            List<int> keys = new List<int>(_activeAttackLines.Keys);
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
                ReturnLineToPool(_activeCourseLine, true);
                _activeCourseLine = null;
            }
            ClearAllAttackLinesVisuals();
        }

        void OnDestroy()
        {
            Logger.Log("[CommandVisualizer] OnDestroy BEGIN");
            _isInitialized = false; 

            if (_clientComposer != null)
            {
                _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            }
            if (_localEscadreProxy != null)
            {
                try { _localEscadreProxy.PositionChanged -= OnLocalEscadreTransformChanged_Position; } catch {}
                try { _localEscadreProxy.RotationChanged -= OnLocalEscadreTransformChanged_Rotation; } catch {}
                try { _localEscadreProxy.OnCurrentDestinationChanged -= OnProxyDestinationChanged; } catch {}
                try { _localEscadreProxy.OnTargetEscadreEntityIdsChanged -= OnProxyAttackTargetsChanged; } catch {}
            }

            ClearAllLinesVisuals();

            foreach (var line in _courseLinePoolMasterList) if (line != null) Destroy(line.gameObject);
            _courseLinePoolMasterList.Clear();
            
            foreach (var line in _attackLinePoolMasterList) if (line != null) Destroy(line.gameObject);
            _attackLinePoolMasterList.Clear();
            
            _activeCourseLine = null;
            _activeAttackLines.Clear();

            Logger.Log("[CommandVisualizer] OnDestroy END");
        }
    }
}