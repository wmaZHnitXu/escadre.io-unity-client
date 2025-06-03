// File: Client/Presentation/CommandVisualizer.cs
using UnityEngine; // Fully qualify: UnityEngine.Vector3, Color
using Core.Client;
using Core.Network.Proxies;
using System.Collections.Generic;
using System.Linq;
using Core.Logging;
using Core.Network;
using Logger = Core.Logging.Logger;

namespace Client.Presentation
{
    public class CommandVisualizer : MonoBehaviour
    {
        // Dependencies will be injected by ClientComposer during Initialize
        private ClientComposer _clientComposer; // To get LocalEscadreProxy and ClientLevel
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
        private List<DashedLineRenderer> _linePool = new List<DashedLineRenderer>();

        private bool _isInitialized = false;

        // Initialize is called by ClientComposer after instantiation
        public void Initialize(ClientComposer clientComposerInstance)
        {
            if (_isInitialized) return;

            _clientComposer = clientComposerInstance ?? throw new System.ArgumentNullException(nameof(clientComposerInstance));
            _clientLevel = _clientComposer.ClientLevel;

            if (courseLinePrefab == null || attackLinePrefab == null)
            {
                Logger.LogError("[CommandVisualizer] Line prefabs not assigned. Cannot visualize commands.");
                enabled = false; // Disable this component if prefabs are missing
                Destroy(gameObject); // Destroy this visualizer instance if it cannot function
                return;
            }
            if (_clientLevel == null)
            {
                Logger.LogError("[CommandVisualizer] ClientLevel is null. Cannot initialize.");
                enabled = false;
                Destroy(gameObject);
                return;
            }

            _clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
            HandleLocalEscadreProxyChanged(_clientComposer.LocalEscadreProxy); // Initial setup with current proxy

            _isInitialized = true;
            Logger.Log("[CommandVisualizer] Initialized.");
        }

        private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
        {
            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.PositionChanged -= OnLocalEscadreMoved;
                _localEscadreProxy.RotationChanged -= OnLocalEscadreRotated;
                _localEscadreProxy.OnCurrentDestinationChanged -= UpdateCourseLine;
                _localEscadreProxy.OnTargetEscadreEntityIdsChanged -= UpdateAttackLines;
            }

            _localEscadreProxy = newProxy;
            ClearAllLines();

            if (_localEscadreProxy != null)
            {
                _localEscadreProxy.PositionChanged += OnLocalEscadreMoved;
                _localEscadreProxy.RotationChanged += OnLocalEscadreRotated;
                _localEscadreProxy.OnCurrentDestinationChanged += UpdateCourseLine;
                _localEscadreProxy.OnTargetEscadreEntityIdsChanged += UpdateAttackLines;
                UpdateAllLines();
            }
            // Logger.Log($"[CommandVisualizer] LocalEscadreProxy updated to: {(_localEscadreProxy != null ? _localEscadreProxy.EntityId.ToString() : "null")}");
        }

        private void OnLocalEscadreMoved(Core.Primitives.Vector3 newPosition)
        {
            if (!_isInitialized || _localEscadreProxy == null) return;
            UpdateAllLines();
        }

        private void OnLocalEscadreRotated(Core.Primitives.Quaternion newRotation)
        {
             if (!_isInitialized || _localEscadreProxy == null) return;
             UpdateAllLines();
        }

        void Update()
        {
            if (!_isInitialized || _localEscadreProxy == null || _clientLevel == null) return;

            if (_activeAttackLines.Count > 0)
            {
                bool needsFullAttackLineUpdate = false;
                foreach (var pair in _activeAttackLines)
                {
                    if (_clientLevel.TryGetProxy(pair.Key, out IClientProxy targetEscadreIProxy) &&
                        targetEscadreIProxy is EscadreProxy.ClientProxy targetEscadreProxy &&
                        !targetEscadreProxy.IsDestroyed)
                    {
                        // Check if target's presentation moved (simplified check on proxy position)
                        // A more robust way: targetEscadreProxy.PositionChanged could trigger this update too.
                        // For now, simple check against LineRenderer endpoint.
                        var lineRenderer = pair.Value.GetComponent<LineRenderer>();
                        if(lineRenderer.positionCount == 2) {
                            UnityEngine.Vector3 currentEnd = lineRenderer.GetPosition(1);
                            UnityEngine.Vector3 newTargetEnd = targetEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                            if(UnityEngine.Vector3.SqrMagnitude(currentEnd - newTargetEnd) > 0.01f) {
                                needsFullAttackLineUpdate = true;
                                break;
                            }
                        }
                    }
                }
                 if(needsFullAttackLineUpdate) UpdateAttackLines();
            }
        }

        private void UpdateAllLines()
        {
            if (!_isInitialized) return;
            UpdateCourseLine();
            UpdateAttackLines();
        }

        private void UpdateCourseLine()
        {
            if (!_isInitialized || _localEscadreProxy == null)
            {
                if (_activeCourseLine != null) _activeCourseLine.Hide();
                return;
            }

            if (_localEscadreProxy.CurrentDestination.HasValue)
            {
                if (_activeCourseLine == null)
                {
                    _activeCourseLine = GetPooledLine(courseLinePrefab);
                    _activeCourseLine.SetColor(courseLineColor);
                    _activeCourseLine.SetWidth(courseLineWidth);
                }
                _activeCourseLine.Show();
                UnityEngine.Vector3 startPos = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                UnityEngine.Vector3 endPos = new UnityEngine.Vector3(
                    _localEscadreProxy.CurrentDestination.Value.X,
                    startPos.y, // Keep Y level for a 2D line on map
                    _localEscadreProxy.CurrentDestination.Value.Y
                );
                _activeCourseLine.SetPoints(startPos, endPos);
            }
            else
            {
                if (_activeCourseLine != null)
                {
                    _activeCourseLine.Hide();
                }
            }
        }

        private void UpdateAttackLines()
        {
            if (!_isInitialized || _localEscadreProxy == null || _clientLevel == null)
            {
                ClearAttackLines();
                return;
            }

            UnityEngine.Vector3 localEscadrePosUnity = _localEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
            var currentTargetIds = new HashSet<int>(_localEscadreProxy.TargetEscadreEntityIds);
            var linesToRemove = new List<int>();

            foreach (var pair in _activeAttackLines)
            {
                if (currentTargetIds.Contains(pair.Key) &&
                    _clientLevel.TryGetProxy(pair.Key, out IClientProxy targetEscadreIProxy) &&
                    targetEscadreIProxy is EscadreProxy.ClientProxy targetEscadreProxy &&
                    !targetEscadreProxy.IsDestroyed)
                {
                    UnityEngine.Vector3 targetPosUnity = targetEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                    pair.Value.SetPoints(localEscadrePosUnity, targetPosUnity);
                    pair.Value.Show();
                }
                else
                {
                    pair.Value.Hide();
                    linesToRemove.Add(pair.Key);
                }
            }

            foreach (int idToRemove in linesToRemove)
            {
                ReturnLineToPool(_activeAttackLines[idToRemove]);
                _activeAttackLines.Remove(idToRemove);
            }

            foreach (int targetId in currentTargetIds)
            {
                if (!_activeAttackLines.ContainsKey(targetId))
                {
                    if (_clientLevel.TryGetProxy(targetId, out IClientProxy targetEscadreIProxy) &&
                        targetEscadreIProxy is EscadreProxy.ClientProxy targetEscadreProxy &&
                        !targetEscadreProxy.IsDestroyed)
                    {
                        DashedLineRenderer newLine = GetPooledLine(attackLinePrefab);
                        newLine.SetColor(attackLineColor);
                        newLine.SetWidth(attackLineWidth);
                        UnityEngine.Vector3 targetPosUnity = targetEscadreProxy.Position.ToUnityVector() + UnityEngine.Vector3.up * lineYOffset;
                        newLine.SetPoints(localEscadrePosUnity, targetPosUnity);
                        newLine.Show();
                        _activeAttackLines[targetId] = newLine;
                    }
                }
            }
        }

        private DashedLineRenderer GetPooledLine(DashedLineRenderer prefab)
        {
            DashedLineRenderer line = null;
            for (int i = 0; i < _linePool.Count; i++)
            {
                // A more robust check might involve comparing original prefab reference if lines are complex.
                // For now, if name matches (assuming prefab.name is unique for course/attack) or simply take any inactive.
                if (!_linePool[i].gameObject.activeSelf && _linePool[i].name.StartsWith(prefab.name))
                {
                    line = _linePool[i];
                    _linePool.RemoveAt(i);
                    break;
                }
            }

            if (line == null) // No suitable inactive line found, instantiate new
            {
                line = Instantiate(prefab, transform); // Parent to this visualizer
                line.name = $"{prefab.name}_Instance_{_activeAttackLines.Count + (_activeCourseLine != null ? 1 : 0) + _linePool.Count}";
            }
            line.gameObject.SetActive(true);
            return line;
        }

        private void ReturnLineToPool(DashedLineRenderer line)
        {
            if (line != null)
            {
                line.Hide();
                line.gameObject.SetActive(false);
                if (!_linePool.Contains(line))
                {
                    _linePool.Add(line);
                }
            }
        }

        private void ClearAttackLines()
        {
            foreach (var line in _activeAttackLines.Values)
            {
                ReturnLineToPool(line);
            }
            _activeAttackLines.Clear();
        }

        private void ClearAllLines()
        {
            if (_activeCourseLine != null)
            {
                ReturnLineToPool(_activeCourseLine);
                _activeCourseLine = null;
            }
            ClearAttackLines();
        }

        void OnDestroy()
        {
            if (_clientComposer != null) // Check if _clientComposer was set
            {
                _clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            }
            if (_localEscadreProxy != null) // Check if _localEscadreProxy was set
            {
                _localEscadreProxy.PositionChanged -= OnLocalEscadreMoved;
                _localEscadreProxy.RotationChanged -= OnLocalEscadreRotated;
                _localEscadreProxy.OnCurrentDestinationChanged -= UpdateCourseLine;
                _localEscadreProxy.OnTargetEscadreEntityIdsChanged -= UpdateAttackLines;
            }

            ClearAllLines();
            foreach (var line in _linePool)
            {
                if (line != null && line.gameObject != null) Destroy(line.gameObject);
            }
            _linePool.Clear();
            _isInitialized = false;
        }
    }
}