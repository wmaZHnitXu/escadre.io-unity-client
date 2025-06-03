// File: Client/InputServices/PlayerInputController.cs
using UnityEngine;
using UnityEngine.EventSystems; // Required for UI interaction check
using Core.Client;
using Core.Logging;
using Core.Network.Proxies;
using Logger = Core.Logging.Logger; // For EscadreProxy.ClientProxy
using Client.Presentation; // For OceanPresentation (optional for TapResolverService context)

namespace Client.InputServices
{
    public class PlayerInputController : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Assign the main camera used for raycasting.")]
        [SerializeField] private UnityEngine.Camera mainCamera;
        [Tooltip("Reference to the ClientComposer to access client-side services.")]
        [SerializeField] private ClientComposer clientComposer;

        [Header("Tap Detection Settings")]
        [Tooltip("Maximum duration for a touch to be considered a tap (seconds).")]
        [SerializeField] private float maxTapDuration = 0.25f;
        [Tooltip("Maximum movement distance (squared) for a touch to be considered a tap.")]
        [SerializeField] private float maxTapMovementSqr = 225f; // 15*15 pixels

        private ClientGameActions _gameActions;
        private ClientLevel _clientLevel;
        private EscadreProxy.ClientProxy _localEscadreProxy;
        // private OceanPresentation _oceanPresentation; // Keep if TapResolverService needs dynamic ocean Y

        private bool _isInitialized = false;

        // Tap state tracking
        private Vector2 _touchStartPos;
        private float _touchStartTime;
        private bool _isPotentialTap;


        void Start()
        {
            if (clientComposer != null && mainCamera != null && !_isInitialized)
            {
                OceanPresentation oceanPres = FindObjectOfType<OceanPresentation>(); // Try to find OceanPresentation
                Initialize(clientComposer, mainCamera, oceanPres);
            }
            else if (clientComposer == null)
            {
                Logger.LogWarning($"[PlayerInputController] ClientComposer not assigned. Input will be disabled.");
            }
            else if (mainCamera == null)
            {
                Logger.LogError($"[PlayerInputController] Main Camera not assigned. Input will be disabled.");
            }
        }

        public void Initialize(ClientComposer composer, UnityEngine.Camera cam, OceanPresentation oceanPresentation)
        {
            if (_isInitialized) return;

            clientComposer = composer ?? throw new System.ArgumentNullException(nameof(composer));
            mainCamera = cam ?? throw new System.ArgumentNullException(nameof(cam));
            // _oceanPresentation = oceanPresentation; // Store if TapResolverService needs it

            _gameActions = clientComposer.GameActions;
            _clientLevel = clientComposer.ClientLevel;

            clientComposer.OnLocalEscadreProxyChanged += HandleLocalEscadreProxyChanged;
            HandleLocalEscadreProxyChanged(clientComposer.LocalEscadreProxy);

            if (_gameActions == null) Logger.LogError("[PlayerInputController] GameActions not available from ClientComposer.");
            if (_clientLevel == null) Logger.LogError("[PlayerInputController] ClientLevel not available from ClientComposer.");

            _isInitialized = true;
            Logger.Log("[PlayerInputController] Initialized.");
        }
        
        private void HandleLocalEscadreProxyChanged(EscadreProxy.ClientProxy newProxy)
        {
            _localEscadreProxy = newProxy;
        }


        void Update()
        {
            if (!_isInitialized || _gameActions == null || _clientLevel == null || mainCamera == null)
            {
                return;
            }

            // If Ocean Y can change dynamically and TapResolverService needs it:
            // if (_oceanPresentation != null)
            // {
            //    TapResolverService.UpdateOceanPlaneHeight(_oceanPresentation.oceanYLevel);
            // }

            bool tapDetected = false;
            UnityEngine.Vector3 tapScreenPosition = Vector3.zero;

            // Handle Mouse Input (primarily for editor/desktop)
            if (Input.GetMouseButtonDown(0))
            {
                _isPotentialTap = true;
                _touchStartPos = Input.mousePosition;
                _touchStartTime = Time.time;
            }

            if (Input.GetMouseButtonUp(0) && _isPotentialTap)
            {
                _isPotentialTap = false;
                if (EventSystem.current.IsPointerOverGameObject()) // Check for UI element on mouse up
                {
                    return;
                }

                float tapDuration = Time.time - _touchStartTime;
                float tapMovementSqr = (new Vector2(Input.mousePosition.x, Input.mousePosition.y) - _touchStartPos).sqrMagnitude;

                if (tapDuration <= maxTapDuration && tapMovementSqr <= maxTapMovementSqr)
                {
                    tapDetected = true;
                    tapScreenPosition = Input.mousePosition;
                }
            }

            // Handle Touch Input
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _isPotentialTap = true;
                    _touchStartPos = touch.position;
                    _touchStartTime = Time.time;
                }
                else if (touch.phase == TouchPhase.Ended && _isPotentialTap)
                {
                    _isPotentialTap = false;
                    // Pass fingerId for IsPointerOverGameObject when dealing with touch
                    if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    {
                        return;
                    }

                    float tapDuration = Time.time - _touchStartTime;
                    float tapMovementSqr = (touch.position - _touchStartPos).sqrMagnitude;

                    if (tapDuration <= maxTapDuration && tapMovementSqr <= maxTapMovementSqr)
                    {
                        tapDetected = true;
                        tapScreenPosition = touch.position;
                    }
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (_isPotentialTap) // If still a potential tap, check if it moved too far
                    {
                        float tapMovementSqr = (touch.position - _touchStartPos).sqrMagnitude;
                        if (tapMovementSqr > maxTapMovementSqr)
                        {
                            _isPotentialTap = false; // No longer a tap if moved too much
                        }
                    }
                }
                else if (touch.phase == TouchPhase.Canceled)
                {
                    _isPotentialTap = false;
                }
            }


            if (tapDetected)
            {
                UnityEngine.Ray ray = mainCamera.ScreenPointToRay(tapScreenPosition);

                ResolvedTapTarget targetInfo = TapResolverService.Resolve(
                    ray,
                    _clientLevel,
                    _localEscadreProxy
                );

                HandleResolvedTap(targetInfo);
            }
        }

        private void HandleResolvedTap(ResolvedTapTarget targetInfo)
        {
            if (_localEscadreProxy == null || _localEscadreProxy.IsDestroyed)
            {
                Logger.LogWarning("[PlayerInputController] No local escadre or it's destroyed. Cannot issue commands.");
                return;
            }

            switch (targetInfo.TargetType)
            {
                case ResolvedTapType.AttackEscadre:
                    if (targetInfo.HitEscadreProxy != null && !targetInfo.HitEscadreProxy.IsDestroyed)
                    {
                        _gameActions.SendAttackEscadre(targetInfo.HitEscadreProxy.EntityId);
                        Logger.Log($"[PlayerInputController] Action: Ordered ATTACK on Escadre {targetInfo.HitEscadreProxy.EntityId}");
                    }
                    else
                    {
                        Logger.LogError("[PlayerInputController] AttackEscadre resolved but HitEscadreProxy is null or destroyed.");
                    }
                    break;
                case ResolvedTapType.MoveToPoint:
                    Core.Primitives.Vector2 courseTarget = new Core.Primitives.Vector2(targetInfo.HitOceanPoint.X, targetInfo.HitOceanPoint.Z);
                    _gameActions.SendSetCourse(courseTarget);
                    Logger.Log($"[PlayerInputController] Action: Ordered MOVE to {courseTarget}");
                    break;
                case ResolvedTapType.NoTarget:
                    break;
                default:
                    Logger.LogWarning($"[PlayerInputController] Unhandled ResolvedTapType: {targetInfo.TargetType}");
                    break;
            }
        }

        public void RequestCancelAllAttacks()
        {
            if (!_isInitialized || _gameActions == null || _localEscadreProxy == null || _localEscadreProxy.IsDestroyed)
            {
                Logger.LogWarning("[PlayerInputController] Cannot cancel attacks: Not initialized or no local escadre.");
                return;
            }
            _gameActions.SendCancelAttack();
            Logger.Log("[PlayerInputController] Action: Requested CANCEL ALL ATTACKS.");
        }

        void OnDestroy()
        {
            if (clientComposer != null)
            {
                clientComposer.OnLocalEscadreProxyChanged -= HandleLocalEscadreProxyChanged;
            }
        }
    }
}