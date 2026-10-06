using System.Numerics;
using BepuPhysics;

namespace Engine;

/// <summary>Which of a vehicle's wheels the engine turns.</summary>
public enum VehicleDrive
{
    /// <summary>The back two, as most cars.</summary>
    Rear,
    /// <summary>The front two.</summary>
    Front,
    /// <summary>All four.</summary>
    All,
}
