// File: Scripts/Client/Camera/DesktopCameraInput.cs
using UnityEngine;

namespace Client.Camera
{
    public class DesktopCameraInput : ICameraInputStrategy
    {
        private readonly UnityEngine.Camera _cameraComponent;
        private Vector3 _lastMousePosition;
        private bool _isMouseButtonPanningActive; // Renamed for clarity

        private Vector2 _currentPanDelta;
        private float _currentZoomDelta;
        private float _currentYawDelta;

        // --- Configuration ---
        // Mouse Drag Pan
        public float MousePanSensitivity { get; set; } = 0.1f;
        public int PanMouseButton { get; set; } = 1; // 0=Left, 1=Right, 2=Middle

        // Scroll Zoom
        public float ScrollZoomSensitivity { get; set; } = 20f;

        // Keyboard Pan & Yaw
        public float KeyboardPanSpeed { get; set; } = 50f;
        public float KeyboardYawSpeed { get; set; } = 90f; // Degrees per second
        
        // Screen Edge Pan
        public float MaxScreenEdgePanSpeed { get; set; } = 80f;
        // Percentage of screen (from center to edge) that is the dead zone.
        // 0.0 = no dead zone, panning starts from center.
        // 0.8 = dead zone is 80% of half-screen, active zone is outer 20%.
        public float ScreenEdgeDeadZoneNormalized { get; set; } = 1.0f; // e.g., 70% from center is dead zone

        private Plane _panningPlane;

        // State for continuous screen edge panning when mouse leaves window
        private Vector2 _lastScreenEdgePanWorldDirection; // Normalized world-space direction
        private float _lastScreenEdgePanSpeedFactor;    // Speed factor (0 to 1)
        private bool _wasScreenEdgePanningLastFrame;

        public DesktopCameraInput(UnityEngine.Camera cameraComponent)
        {
            _cameraComponent = cameraComponent;
            _panningPlane = new Plane(Vector3.up, Vector3.zero);
        }

        public void UpdateStrategy(float deltaTime)
        {
            _currentPanDelta = Vector2.zero;
            _currentZoomDelta = 0f;
            _currentYawDelta = 0f;

            Transform rigTransform = _cameraComponent.transform.parent;

            bool explicitInputThisFrame = false;

            // 1. Zoom Input (Mouse Scroll Wheel)
            _currentZoomDelta = -Input.mouseScrollDelta.y * ScrollZoomSensitivity;
            if (_currentZoomDelta != 0f) explicitInputThisFrame = true;

            // 2. Pan Input (Mouse Drag)
            if (Input.GetMouseButtonDown(PanMouseButton))
            {
                _isMouseButtonPanningActive = true;
                _lastMousePosition = Input.mousePosition;
                if (rigTransform != null)
                {
                    _panningPlane.distance = -rigTransform.position.y;
                }
                explicitInputThisFrame = true;
            }

            if (Input.GetMouseButtonUp(PanMouseButton))
            {
                _isMouseButtonPanningActive = false;
                explicitInputThisFrame = true; // يعتبر إفلات الزر إدخالاً
            }

            if (_isMouseButtonPanningActive && Input.GetMouseButton(PanMouseButton))
            {
                Vector3 mouseDeltaPixels = Input.mousePosition - _lastMousePosition;
                _lastMousePosition = Input.mousePosition;

                if (mouseDeltaPixels.sqrMagnitude > 0.01f)
                {
                    Ray prevRay = _cameraComponent.ScreenPointToRay(Input.mousePosition - mouseDeltaPixels);
                    Ray currentRay = _cameraComponent.ScreenPointToRay(Input.mousePosition);

                    if (_panningPlane.Raycast(prevRay, out float prevDist) && _panningPlane.Raycast(currentRay, out float currentDist))
                    {
                        Vector3 prevPointOnPlane = prevRay.GetPoint(prevDist);
                        Vector3 currentPointOnPlane = currentRay.GetPoint(currentDist);
                        Vector3 worldPanAmount = prevPointOnPlane - currentPointOnPlane;
                        _currentPanDelta += new Vector2(worldPanAmount.x, worldPanAmount.z);
                    }
                }
                explicitInputThisFrame = true;
            }

            // 3. Pan Input (Keyboard)
            float horizontalKey = Input.GetAxis("Horizontal");
            float verticalKey = Input.GetAxis("Vertical");

            if (Mathf.Abs(horizontalKey) > 0.01f || Mathf.Abs(verticalKey) > 0.01f)
            {
                Vector3 forward = rigTransform != null ? rigTransform.forward : Vector3.forward;
                Vector3 right = rigTransform != null ? rigTransform.right : Vector3.right;
                forward.y = 0; forward.Normalize();
                right.y = 0; right.Normalize();
                Vector3 keyboardPan = (right * horizontalKey + forward * verticalKey) * KeyboardPanSpeed * deltaTime;
                _currentPanDelta += new Vector2(keyboardPan.x, keyboardPan.z);
                explicitInputThisFrame = true;
            }
            
            // 4. Yaw Input (Keyboard)
            if (Input.GetKey(KeyCode.Q))
            {
                _currentYawDelta -= KeyboardYawSpeed * deltaTime;
                explicitInputThisFrame = true;
            }
            if (Input.GetKey(KeyCode.E))
            {
                _currentYawDelta += KeyboardYawSpeed * deltaTime;
                explicitInputThisFrame = true;
            }

            // If any explicit input occurred, reset screen edge panning persistence
            if (explicitInputThisFrame)
            {
                _wasScreenEdgePanningLastFrame = false;
                _lastScreenEdgePanWorldDirection = Vector2.zero;
                _lastScreenEdgePanSpeedFactor = 0f;
            }

            // 5. Pan Input (Screen Edge) - Only if no other explicit input is active this frame
            bool currentFrameScreenEdgeActuallyPanning = false;
            if (!explicitInputThisFrame && Application.isFocused) // Check Application.isFocused here
            {
                Vector3 mousePos = Input.mousePosition;
                int screenWidth = Screen.width;
                int screenHeight = Screen.height;
                bool mouseInWindow = mousePos.x >= 0 && mousePos.x < screenWidth && mousePos.y >= 0 && mousePos.y < screenHeight;

                if (mouseInWindow)
                {
                    Vector2 screenCenter = new Vector2(screenWidth / 2f, screenHeight / 2f);
                    Vector2 mouseFromCenter = new Vector2(mousePos.x, mousePos.y) - screenCenter;

                    float deadZoneRadiusX = screenCenter.x * ScreenEdgeDeadZoneNormalized;
                    float deadZoneRadiusY = screenCenter.y * ScreenEdgeDeadZoneNormalized;

                    if (Mathf.Abs(mouseFromCenter.x) > deadZoneRadiusX || Mathf.Abs(mouseFromCenter.y) > deadZoneRadiusY)
                    {
                        Vector2 panDirectionScreenSpace = mouseFromCenter.normalized;

                        float effectiveDistX = Mathf.Max(0, Mathf.Abs(mouseFromCenter.x) - deadZoneRadiusX);
                        float effectiveDistY = Mathf.Max(0, Mathf.Abs(mouseFromCenter.y) - deadZoneRadiusY);
                        
                        float rampZoneWidthX = screenCenter.x - deadZoneRadiusX; // Distance from dead zone edge to screen edge
                        float rampZoneWidthY = screenCenter.y - deadZoneRadiusY;

                        float factorX = (rampZoneWidthX > 1e-3f) ? Mathf.Clamp01(effectiveDistX / rampZoneWidthX) : (effectiveDistX > 0 ? 1f: 0f) ;
                        float factorY = (rampZoneWidthY > 1e-3f) ? Mathf.Clamp01(effectiveDistY / rampZoneWidthY) : (effectiveDistY > 0 ? 1f: 0f) ;
                        
                        _lastScreenEdgePanSpeedFactor = Mathf.Max(factorX, factorY); // Use the stronger axis influence for speed

                        // Convert screen direction to world direction based on rig's orientation
                        Vector3 rigForward = rigTransform != null ? rigTransform.forward : Vector3.forward;
                        Vector3 rigRight = rigTransform != null ? rigTransform.right : Vector3.right;
                        rigForward.y = 0; rigForward.Normalize();
                        rigRight.y = 0; rigRight.Normalize();
                        
                        Vector3 worldDir3D = (rigRight * panDirectionScreenSpace.x + rigForward * panDirectionScreenSpace.y).normalized;
                        _lastScreenEdgePanWorldDirection = new Vector2(worldDir3D.x, worldDir3D.z);
                        
                        currentFrameScreenEdgeActuallyPanning = true;
                    }
                    else // Mouse in dead zone
                    {
                        _lastScreenEdgePanWorldDirection = Vector2.zero;
                        _lastScreenEdgePanSpeedFactor = 0f;
                    }
                    _wasScreenEdgePanningLastFrame = currentFrameScreenEdgeActuallyPanning;
                }
                else if (_wasScreenEdgePanningLastFrame && _lastScreenEdgePanWorldDirection != Vector2.zero)
                {
                    // Mouse left window, but was edge panning: continue with last direction and speed
                    currentFrameScreenEdgeActuallyPanning = true;
                }
                else // Mouse out of window and wasn't edge panning before
                {
                    _wasScreenEdgePanningLastFrame = false;
                    _lastScreenEdgePanWorldDirection = Vector2.zero;
                    _lastScreenEdgePanSpeedFactor = 0f;
                }

                if (currentFrameScreenEdgeActuallyPanning && _lastScreenEdgePanWorldDirection != Vector2.zero)
                {
                    Vector2 worldPanAmount = _lastScreenEdgePanWorldDirection * _lastScreenEdgePanSpeedFactor * MaxScreenEdgePanSpeed * deltaTime;
                    _currentPanDelta += worldPanAmount;
                }
            }
             // If screen edge panning was not active this frame (due to explicit input or mouse in deadzone/out of window without prior panning),
             // ensure the persistence flags are cleared if they weren't by explicit input.
            if (!currentFrameScreenEdgeActuallyPanning && !explicitInputThisFrame) {
                _wasScreenEdgePanningLastFrame = false;
                _lastScreenEdgePanWorldDirection = Vector2.zero;
                _lastScreenEdgePanSpeedFactor = 0f;
            }
        }

        public Vector2 GetPanDelta()
        {
            return _currentPanDelta;
        }

        public float GetZoomDelta()
        {
            return _currentZoomDelta;
        }

        public float GetYawDelta()
        {
            return _currentYawDelta;
        }

        public bool WantsToControl()
        {
            // Control is wanted if any explicit input is active OR if screen edge panning is currently producing a delta
            bool explicitInputActive = _isMouseButtonPanningActive ||
                                       _currentZoomDelta != 0f ||
                                       Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f ||
                                       Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f ||
                                       Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E);

            bool screenEdgePanningProducingDelta = _lastScreenEdgePanWorldDirection != Vector2.zero && _lastScreenEdgePanSpeedFactor > 0f;
            
            return explicitInputActive || screenEdgePanningProducingDelta;
        }
    }
}