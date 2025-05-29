// File: Scripts/Client/Camera/TopDownCameraController.cs
using UnityEngine;
using Core.Logging; // For Logger
using Logger = Core.Logging.Logger; // Alias

namespace Client.Camera
{
    public class TopDownCameraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The actual Unity Camera child of this rig.")]
        [SerializeField] public UnityEngine.Camera mainCamera; 

        [Header("Target Following")]
        [SerializeField] private Transform targetToFollow;
        [Tooltip("How quickly the camera rig moves to the target's XZ position.")]
        [SerializeField] private float followLerpSpeed = 5f;

        [Header("View Configuration")]
        [Tooltip("Base pitch angle of the camera in degrees (e.g., 60 for angled top-down).")]
        [SerializeField] private float basePitchAngleDegrees = 60f;
        [Tooltip("Adjusts pitch based on zoom. X-axis: Normalized zoom (0=min, 1=max). Y-axis: Pitch addition (degrees).")]
        [SerializeField] private AnimationCurve pitchAdjustmentCurve = AnimationCurve.Linear(0, 10, 1, 0); // Example: more pitched down when zoomed in
        
        [Tooltip("Minimum height of the camera from the rig (zoom in limit).")]
        [SerializeField] private float minHeight = 5f;
        [Tooltip("Maximum height of the camera from the rig (zoom out limit).")]
        [SerializeField] private float maxHeight = 50f;
        [Tooltip("Initial height of the camera.")]
        [SerializeField] private float initialHeight = 20f;
        [Tooltip("Initial yaw angle of the camera rig.")]
        [SerializeField] private float initialYawAngleDegrees = 0f;


        [Header("Manual Control Smoothing")]
        [Tooltip("How quickly the camera rig responds to manual panning input.")]
        [SerializeField] private float panLerpSpeed = 10f;
        [Tooltip("How quickly the camera responds to manual zoom input.")]
        [SerializeField] private float zoomLerpSpeed = 10f;
        [Tooltip("How quickly the camera rig responds to manual yaw input.")]
        [SerializeField] private float yawLerpSpeed = 10f;


        private ICameraInputStrategy _inputStrategy;

        // Internal state for smoothing and desired positions/rotations
        private Vector3 _desiredRigPosition;
        private Vector3 _currentSmoothedRigPosition;
        private float _desiredCameraLocalY;
        private float _currentSmoothedCameraLocalY;
        private float _desiredRigYaw;
        private float _currentSmoothedRigYaw;
        
        private bool _isInitialized = false;

        void Awake()
        {
            if (mainCamera == null)
            {
                Logger.LogError("[TopDownCameraController] Main Camera reference not set!");
                enabled = false;
                return;
            }
            if (mainCamera.transform.parent != transform)
            {
                Logger.LogError("[TopDownCameraController] Main Camera must be a child of this CameraRig GameObject!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            if (!_isInitialized && _inputStrategy != null) 
            {
                 InitializeInternalState();
            }
            else if (_inputStrategy == null)
            {
                Logger.LogWarning("[TopDownCameraController] InputStrategy not set by Start. Waiting for SetInputStrategy call.");
            }
        }
        
        private void InitializeInternalState()
        {
            _currentSmoothedRigPosition = transform.position;
            _desiredRigPosition = _currentSmoothedRigPosition;

            _currentSmoothedCameraLocalY = Mathf.Clamp(initialHeight, minHeight, maxHeight);
            _desiredCameraLocalY = _currentSmoothedCameraLocalY;

            _currentSmoothedRigYaw = initialYawAngleDegrees;
            _desiredRigYaw = initialYawAngleDegrees;
            transform.rotation = Quaternion.Euler(0, _currentSmoothedRigYaw, 0);


            ApplyCameraLocalTransform(); 
            _isInitialized = true;
            Logger.Log("[TopDownCameraController] Initialized internal state.");
        }


        public void SetInputStrategy(ICameraInputStrategy strategy)
        {
            _inputStrategy = strategy;
            Logger.Log($"[TopDownCameraController] Input strategy set to: {strategy?.GetType().Name ?? "null"}");
            if (gameObject.activeInHierarchy && enabled && !_isInitialized) 
            {
                InitializeInternalState();
            }
        }
        
        public void SetTarget(Transform newTarget, bool immediate = false)
        {
            targetToFollow = newTarget;
            if (immediate && newTarget != null && _isInitialized)
            {
                _desiredRigPosition = new Vector3(newTarget.position.x, transform.position.y, newTarget.position.z);
                _currentSmoothedRigPosition = _desiredRigPosition;
                // Rig Y and Yaw are not changed by SetTarget directly
            }
             Logger.Log(newTarget != null ? $"[TopDownCameraController] New target set: {newTarget.name}" : "[TopDownCameraController] Target cleared.");
        }


        void LateUpdate()
        {
            if (!_isInitialized || _inputStrategy == null || mainCamera == null)
            {
                return;
            }

            _inputStrategy.UpdateStrategy(Time.deltaTime);

            if (_inputStrategy.WantsToControl())
            {
                targetToFollow = null; 

                Vector2 panDelta = _inputStrategy.GetPanDelta();
                _desiredRigPosition += new Vector3(panDelta.x, 0, panDelta.y);

                float zoomDelta = _inputStrategy.GetZoomDelta();
                _desiredCameraLocalY += zoomDelta; 

                float yawDelta = _inputStrategy.GetYawDelta();
                _desiredRigYaw += yawDelta;
            }
            
            _desiredCameraLocalY = Mathf.Clamp(_desiredCameraLocalY, minHeight, maxHeight);

            // Rig Position
            if (targetToFollow != null)
            {
                _desiredRigPosition = new Vector3(targetToFollow.position.x, transform.position.y, targetToFollow.position.z);
                _currentSmoothedRigPosition = Vector3.Lerp(_currentSmoothedRigPosition, _desiredRigPosition, followLerpSpeed * Time.deltaTime);
            }
            else 
            {
                _currentSmoothedRigPosition = Vector3.Lerp(_currentSmoothedRigPosition, _desiredRigPosition, panLerpSpeed * Time.deltaTime);
            }

            // Rig Yaw
            _currentSmoothedRigYaw = Mathf.LerpAngle(_currentSmoothedRigYaw, _desiredRigYaw, yawLerpSpeed * Time.deltaTime);
            
            // Camera Height (Zoom)
            _currentSmoothedCameraLocalY = Mathf.Lerp(_currentSmoothedCameraLocalY, _desiredCameraLocalY, zoomLerpSpeed * Time.deltaTime);

            // Apply smoothed positions and rotations
            transform.position = _currentSmoothedRigPosition;
            transform.rotation = Quaternion.Euler(0, _currentSmoothedRigYaw, 0);
            ApplyCameraLocalTransform();
        }

        private void ApplyCameraLocalTransform()
        {
            if (mainCamera == null) return;

            // Calculate normalized zoom (0 at minHeight, 1 at maxHeight)
            float normalizedZoom = 0f;
            if (maxHeight - minHeight > 0.01f)
            {
                normalizedZoom = (_currentSmoothedCameraLocalY - minHeight) / (maxHeight - minHeight);
            }
            
            float pitchAdjustment = pitchAdjustmentCurve.Evaluate(normalizedZoom);
            float currentDynamicPitch = basePitchAngleDegrees + pitchAdjustment;
            currentDynamicPitch = Mathf.Clamp(currentDynamicPitch, 0f, 89.9f); // Clamp to avoid flipping over

            mainCamera.transform.localEulerAngles = new Vector3(currentDynamicPitch, 0, 0);
            
            float offsetZ = 0;
            if (Mathf.Abs(currentDynamicPitch - 90f) > 0.1f && Mathf.Abs(Mathf.Sin(currentDynamicPitch * Mathf.Deg2Rad)) > 0.001f)
            {
                offsetZ = -_currentSmoothedCameraLocalY / Mathf.Tan(currentDynamicPitch * Mathf.Deg2Rad);
            }
            
            mainCamera.transform.localPosition = new Vector3(0, _currentSmoothedCameraLocalY, offsetZ);
        }

        public Transform GetTargetToFollow()
        {
            return targetToFollow;
        }
    }
}