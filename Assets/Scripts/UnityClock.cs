// File: Scripts/Client/Time/UnityClock.cs (or a shared Time location if server also runs in Unity)
using UnityEngine;
using Core.Time;

/// <summary>
/// Unity-specific implementation of IClock using UnityEngine.Time.time.
/// </summary>
public class UnityClock : IClock
{
    public float CurrentTime => Time.time;
}