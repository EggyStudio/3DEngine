// Functions of raylib's raymath.h that C#'s System.Numerics has no counterpart for, written again
// in C# for the examples that call them, Copyright (c) 2015-2026 Ramon Santamaria (@raysan5),
// under the zlib license. Altered from the original, which is C.

using System.Numerics;

namespace Engine.Examples;

public static class RayMath
{
    /// <summary>The angle from one vector to another, in radians, positive turning from the first toward the second.</summary>
    public static float Vector2Angle(Vector2 v1, Vector2 v2)
    {
        float dot = v1.X*v2.X + v1.Y*v2.Y;
        float det = v1.X*v2.Y - v1.Y*v2.X;
        return MathF.Atan2(det, dot);
    }

    /// <summary>The angle of the line from one point to another, in radians, counterclockwise on the screen from the right.</summary>
    public static float Vector2LineAngle(Vector2 start, Vector2 end) => -MathF.Atan2(end.Y - start.Y, end.X - start.X);
}
