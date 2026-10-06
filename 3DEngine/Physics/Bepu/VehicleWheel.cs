using System.Numerics;
using BepuPhysics;

namespace Engine;

/// <summary>One of a vehicle's wheels as its last step left it, for drawing it and for sounds.</summary>
/// <param name="Center">Where the wheel's middle is, in the world.</param>
/// <param name="Contact">Where it touches the ground, when it does.</param>
/// <param name="Grounded">Whether it touches the ground.</param>
/// <param name="Slip">How fast the tyre slides sideways over the ground, in units a second, which a skid's sound follows.</param>
/// <param name="Spin">How far it has turned about its axle, in radians.</param>
/// <param name="Steer">How far it is turned to steer, in radians, 0 for a wheel that does not steer.</param>
public readonly record struct VehicleWheel(Vector3 Center, Vector3 Contact, bool Grounded, float Slip, float Spin, float Steer);
