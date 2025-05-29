// File: Scripts/Client/Camera/MobileCameraInput.cs
using UnityEngine;
using System.Collections.Generic;

namespace Client.Camera
{
    public class MobileCameraInput : ICameraInputStrategy
    {
        private readonly UnityEngine.Camera _cameraComponent;
        
        private Vector2 _currentPanDelta;
        private float _currentZoomDelta;
        private bool _isInteracting; // True if any touch input is active

        // Configuration
        public float PanSensitivity { get; set; } = 0.05f; 
        public float PinchZoomSensitivity { get; set; } = 0.5f;

        private Plane _panningPlane;
        private Dictionary<int, Vector2> _lastTouchPositions = new Dictionary<int, Vector2>();
        private float _initialPinchDistance;


        public MobileCameraInput(UnityEngine.Camera cameraComponent)
        {
            _cameraComponent = cameraComponent;
            _panningPlane = new Plane(Vector3.up, Vector3.zero);
        }

        public void UpdateStrategy(float deltaTime)
        {
            _currentPanDelta = Vector2.zero;
            _currentZoomDelta = 0f;
            _isInteracting = Input.touchCount > 0;

            if (Input.touchCount == 1)
            {
                HandleSingleTouchPan();
            }
            else if (Input.touchCount == 2)
            {
                HandleTwoTouchPinchZoom();
            }
            else
            {
                _lastTouchPositions.Clear(); 
            }
        }

        private void HandleSingleTouchPan()
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                 if (_cameraComponent.transform.parent != null)
                {
                     _panningPlane.distance = -_cameraComponent.transform.parent.position.y;
                }
                _lastTouchPositions[touch.fingerId] = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved && _lastTouchPositions.ContainsKey(touch.fingerId))
            {
                Vector2 lastPos = _lastTouchPositions[touch.fingerId];
                Vector2 currentPos = touch.position;
                Vector2 screenDelta = currentPos - lastPos;
                _lastTouchPositions[touch.fingerId] = currentPos;

                if (screenDelta.sqrMagnitude > 0.01f)
                {
                    Ray prevRay = _cameraComponent.ScreenPointToRay(currentPos - screenDelta); 
                    Ray currentRay = _cameraComponent.ScreenPointToRay(currentPos);

                    if (_panningPlane.Raycast(prevRay, out float prevDist) && _panningPlane.Raycast(currentRay, out float currentDist))
                    {
                        Vector3 prevPointOnPlane = prevRay.GetPoint(prevDist);
                        Vector3 currentPointOnPlane = currentRay.GetPoint(currentDist);
                        
                        Vector3 worldPanAmount = prevPointOnPlane - currentPointOnPlane;
                        _currentPanDelta = new Vector2(worldPanAmount.x, worldPanAmount.z);
                    }
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _lastTouchPositions.Remove(touch.fingerId);
            }
        }

        private void HandleTwoTouchPinchZoom()
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                _initialPinchDistance = Vector2.Distance(touch0.position, touch1.position);
                _lastTouchPositions[touch0.fingerId] = touch0.position;
                _lastTouchPositions[touch1.fingerId] = touch1.position;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);
                float deltaDistance = currentPinchDistance - _initialPinchDistance;
                
                _currentZoomDelta = -deltaDistance * PinchZoomSensitivity; 

                _initialPinchDistance = currentPinchDistance; 

                _lastTouchPositions[touch0.fingerId] = touch0.position;
                _lastTouchPositions[touch1.fingerId] = touch1.position;
            }
             else if (touch0.phase == TouchPhase.Ended || touch0.phase == TouchPhase.Canceled ||
                     touch1.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Canceled)
            {
                _lastTouchPositions.Clear(); 
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
            return 0f; // Mobile input doesn't control yaw in this design
        }

        public bool WantsToControl()
        {
            return _isInteracting;
        }
    }
}