using System.Numerics;

namespace Engine;

/// <summary>The kind of a <see cref="Joint"/>.</summary>
public enum JointKind
{
    /// <summary>Free to turn about the point, as a ball in a socket.</summary>
    Ball,
    /// <summary>Turning only about the axis, as a door.</summary>
    Hinge,
    /// <summary>Rigid, as the two bodies are placed.</summary>
    Weld,
    /// <summary>Kept between two distances, as a rope or a rod.</summary>
    Distance,
    /// <summary>Sliding along the joint's up and not turning, as a drawer or a lift.</summary>
    Slider,
}
