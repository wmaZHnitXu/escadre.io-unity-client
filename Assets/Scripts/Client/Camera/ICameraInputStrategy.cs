// File: Scripts/Client/Camera/ICameraInputStrategy.cs
using UnityEngine;

namespace Client.Camera
{
    public interface ICameraInputStrategy
    {
        /// <summary>
        /// Called every frame to allow the strategy to process continuous input
        /// or update its internal state.
        /// </summary>
        /// <param name="deltaTime">Time since last frame.</param>
        void UpdateStrategy(float deltaTime);

        /// <summary>
        /// Gets the desired panning delta in world space (XZ plane) for the camera rig.
        /// </summary>
        /// <returns>A Vector2 representing (deltaX, deltaZ).</returns>
        Vector2 GetPanDelta();

        /// <summary>
        /// Gets the desired zoom delta (change in camera's height or distance).
        /// Positive values typically mean zoom out (increase height/distance), negative mean zoom in.
        /// </summary>
        /// <returns>A float representing the zoom delta.</returns>
        float GetZoomDelta();

        /// <summary>
        /// Gets the desired yaw rotation delta in degrees for the camera rig.
        /// </summary>
        /// <returns>A float representing the change in yaw angle.</returns>
        float GetYawDelta();

        /// <summary>
        /// Indicates if the input strategy has active panning, zooming, or yawing input this frame
        /// that should override target following.
        /// </summary>
        /// <returns>True if manual control is active, false otherwise.</returns>
        bool WantsToControl();
    }
}