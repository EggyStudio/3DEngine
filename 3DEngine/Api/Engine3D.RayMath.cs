using System.Numerics;

namespace Engine;

// raylib's raymath.h, its functions that System.Numerics and the BCL have no counterpart for, with
// raymath's names and its arithmetic, Copyright (c) 2015-2026 Ramon Santamaria (@raysan5), under the
// zlib license. The rest of raymath is C#'s own under other names, which docs/compared-with-raylib.md
// lists. A raymath Matrix is a System.Numerics Matrix4x4 here, its field m(4r + c) being M(r+1)(c+1),
// so a translation is in M41 to M43 in both and a product composes in the same order.
public static partial class Engine3D
{
    // raymath's tolerance for a float that is almost another.
    private const float RayMathEpsilon = 0.000001f;

    private static bool AlmostEqual(float x, float y) =>
        MathF.Abs(x - y) <= RayMathEpsilon*MathF.Max(1.0f, MathF.Max(MathF.Abs(x), MathF.Abs(y)));

    // -- Scalars

    /// <summary>Where <paramref name="value"/> lies from <paramref name="start"/> to <paramref name="end"/>, 0 at the start and 1 at the end.</summary>
    public static float Normalize(float value, float start, float end) => (value - start)/(end - start);

    /// <summary>A value carried from one range to another, at the same place in each.</summary>
    public static float Remap(float value, float inputStart, float inputEnd, float outputStart, float outputEnd) =>
        (value - inputStart)/(inputEnd - inputStart)*(outputEnd - outputStart) + outputStart;

    /// <summary>A value wrapped into the range from <paramref name="min"/> to <paramref name="max"/>, as an angle is into a turn.</summary>
    public static float Wrap(float value, float min, float max) => value - (max - min)*MathF.Floor((value - min)/(max - min));

    /// <summary>Whether two floats are equal within a millionth of the larger, or of one where both are smaller.</summary>
    public static bool FloatEquals(float x, float y) => AlmostEqual(x, y);

    // -- Vector2

    /// <summary>The 2D cross product, the z of the 3D one, positive where <paramref name="v2"/> turns clockwise on the screen from <paramref name="v1"/>.</summary>
    public static float Vector2CrossProduct(Vector2 v1, Vector2 v2) => v1.X*v2.Y - v1.Y*v2.X;

    /// <summary>The signed angle from <paramref name="v1"/> to <paramref name="v2"/> in radians, positive clockwise on the screen, where Y is down.</summary>
    public static float Vector2Angle(Vector2 v1, Vector2 v2)
    {
        float dot = v1.X*v2.X + v1.Y*v2.Y;
        float det = v1.X*v2.Y - v1.Y*v2.X;
        return MathF.Atan2(det, dot);
    }

    /// <summary>The angle of the line from <paramref name="start"/> to <paramref name="end"/> in radians, counterclockwise on the screen from the right.</summary>
    public static float Vector2LineAngle(Vector2 start, Vector2 end) => -MathF.Atan2(end.Y - start.Y, end.X - start.X);

    /// <summary>A vector turned by <paramref name="angle"/> radians, clockwise on the screen, where Y is down.</summary>
    public static Vector2 Vector2Rotate(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector2(v.X*cos - v.Y*sin, v.X*sin + v.Y*cos);
    }

    /// <summary>A point moved toward <paramref name="target"/> by at most <paramref name="maxDistance"/>, and onto it where it is nearer.</summary>
    public static Vector2 Vector2MoveTowards(Vector2 v, Vector2 target, float maxDistance)
    {
        float dx = target.X - v.X;
        float dy = target.Y - v.Y;
        float value = dx*dx + dy*dy;

        if (value == 0 || (maxDistance >= 0 && value <= maxDistance*maxDistance)) return target;

        float dist = MathF.Sqrt(value);
        return new Vector2(v.X + dx/dist*maxDistance, v.Y + dy/dist*maxDistance);
    }

    /// <summary>A vector with its length held between <paramref name="min"/> and <paramref name="max"/>, its direction kept.</summary>
    public static Vector2 Vector2ClampValue(Vector2 v, float min, float max)
    {
        float length = v.X*v.X + v.Y*v.Y;
        if (length <= 0.0f) return v;
        length = MathF.Sqrt(length);
        float scale = length < min ? min/length : length > max ? max/length : 1;
        return v*scale;
    }

    /// <summary>Whether two vectors are equal within a millionth in each component, as <see cref="FloatEquals"/> is.</summary>
    public static bool Vector2Equals(Vector2 p, Vector2 q) => AlmostEqual(p.X, q.X) && AlmostEqual(p.Y, q.Y);

    /// <summary>
    /// The direction a ray going along <paramref name="v"/> takes through a surface of normal
    /// <paramref name="n"/>, both of length one, where <paramref name="r"/> is the ratio of the
    /// refractive index it leaves to the one it enters, or zero where it reflects whole.
    /// </summary>
    public static Vector2 Vector2Refract(Vector2 v, Vector2 n, float r)
    {
        float dot = v.X*n.X + v.Y*n.Y;
        float d = 1.0f - r*r*(1.0f - dot*dot);
        if (d < 0.0f) return Vector2.Zero;
        d = MathF.Sqrt(d);
        return new Vector2(r*v.X - (r*dot + d)*n.X, r*v.Y - (r*dot + d)*n.Y);
    }

    // -- Vector3

    /// <summary>A vector at right angles to <paramref name="v"/>, its cross product with the axis it is least along.</summary>
    public static Vector3 Vector3Perpendicular(Vector3 v)
    {
        float min = MathF.Abs(v.X);
        var cardinalAxis = Vector3.UnitX;
        if (MathF.Abs(v.Y) < min)
        {
            min = MathF.Abs(v.Y);
            cardinalAxis = Vector3.UnitY;
        }
        if (MathF.Abs(v.Z) < min) cardinalAxis = Vector3.UnitZ;
        return Vector3.Cross(v, cardinalAxis);
    }

    /// <summary>The angle between two vectors in radians, from 0 to π.</summary>
    public static float Vector3Angle(Vector3 v1, Vector3 v2) => MathF.Atan2(Vector3.Cross(v1, v2).Length(), Vector3.Dot(v1, v2));

    /// <summary>The part of <paramref name="v1"/> along <paramref name="v2"/>.</summary>
    public static Vector3 Vector3Project(Vector3 v1, Vector3 v2) => v2*(Vector3.Dot(v1, v2)/Vector3.Dot(v2, v2));

    /// <summary>The part of <paramref name="v1"/> at right angles to <paramref name="v2"/>.</summary>
    public static Vector3 Vector3Reject(Vector3 v1, Vector3 v2) => v1 - v2*(Vector3.Dot(v1, v2)/Vector3.Dot(v2, v2));

    /// <summary>
    /// Makes <paramref name="v1"/> of length one and <paramref name="v2"/> of length one at right angles
    /// to it, in the plane the two were in (Gram-Schmidt).
    /// </summary>
    public static void Vector3OrthoNormalize(ref Vector3 v1, ref Vector3 v2)
    {
        v1 = NormalizeOrKeep(v1);
        var vn1 = NormalizeOrKeep(Vector3.Cross(v1, v2));
        v2 = Vector3.Cross(vn1, v1);
    }

    // raymath's normalization, which leaves a vector of length zero as it is where C#'s gives NaN.
    private static Vector3 NormalizeOrKeep(Vector3 v)
    {
        float length = v.Length();
        return v*(1.0f/(length == 0.0f ? 1.0f : length));
    }

    /// <summary>A vector turned <paramref name="angle"/> radians about <paramref name="axis"/>, counterclockwise looking down the axis.</summary>
    public static Vector3 Vector3RotateByAxisAngle(Vector3 v, Vector3 axis, float angle)
    {
        // The Euler-Rodrigues formula, as raymath writes it.
        axis = NormalizeOrKeep(axis);
        angle /= 2.0f;
        float a = MathF.Sin(angle);
        var w = axis*a;
        a = MathF.Cos(angle);
        var wv = Vector3.Cross(w, v);
        var wwv = Vector3.Cross(w, wv);
        return v + wv*(2*a) + wwv*2;
    }

    /// <summary>A point moved toward <paramref name="target"/> by at most <paramref name="maxDistance"/>, and onto it where it is nearer.</summary>
    public static Vector3 Vector3MoveTowards(Vector3 v, Vector3 target, float maxDistance)
    {
        var d = target - v;
        float value = d.LengthSquared();
        if (value == 0 || (maxDistance >= 0 && value <= maxDistance*maxDistance)) return target;
        float dist = MathF.Sqrt(value);
        return v + d/dist*maxDistance;
    }

    /// <summary>The cubic Hermite curve between two points with their tangents at <paramref name="amount"/> from 0 to 1, as glTF interpolates.</summary>
    public static Vector3 Vector3CubicHermite(Vector3 v1, Vector3 tangent1, Vector3 v2, Vector3 tangent2, float amount)
    {
        float amountPow2 = amount*amount;
        float amountPow3 = amount*amount*amount;
        return (2*amountPow3 - 3*amountPow2 + 1)*v1 + (amountPow3 - 2*amountPow2 + amount)*tangent1
            + (-2*amountPow3 + 3*amountPow2)*v2 + (amountPow3 - amountPow2)*tangent2;
    }

    /// <summary>The barycentric coordinates (u, v, w) of <paramref name="p"/> in the triangle a, b, c, in whose plane it lies.</summary>
    public static Vector3 Vector3Barycenter(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = p - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denom = d00*d11 - d01*d01;
        float y = (d11*d20 - d01*d21)/denom;
        float z = (d00*d21 - d01*d20)/denom;
        return new Vector3(1.0f - (z + y), y, z);
    }

    /// <summary>
    /// A point in the space <paramref name="projection"/> clips carried back through it and
    /// <paramref name="view"/> into the world.
    /// </summary>
    /// <remarks>
    /// raylib's projections clip depth from -1 to 1, as OpenGL does, and this engine's from 0 to 1,
    /// as Vulkan does, so a point's depth is in the range of the projection it is given.
    /// </remarks>
    public static Vector3 Vector3Unproject(Vector3 source, Matrix4x4 projection, Matrix4x4 view)
    {
        Matrix4x4.Invert(view*projection, out var inverse);
        var transformed = Vector4.Transform(new Vector4(source, 1.0f), inverse);
        return new Vector3(transformed.X, transformed.Y, transformed.Z)/transformed.W;
    }

    /// <summary>A vector with its length held between <paramref name="min"/> and <paramref name="max"/>, its direction kept.</summary>
    public static Vector3 Vector3ClampValue(Vector3 v, float min, float max)
    {
        float length = v.LengthSquared();
        if (length <= 0.0f) return v;
        length = MathF.Sqrt(length);
        float scale = length < min ? min/length : length > max ? max/length : 1;
        return v*scale;
    }

    /// <summary>Whether two vectors are equal within a millionth in each component, as <see cref="FloatEquals"/> is.</summary>
    public static bool Vector3Equals(Vector3 p, Vector3 q) => AlmostEqual(p.X, q.X) && AlmostEqual(p.Y, q.Y) && AlmostEqual(p.Z, q.Z);

    /// <summary>
    /// The direction a ray going along <paramref name="v"/> takes through a surface of normal
    /// <paramref name="n"/>, both of length one, where <paramref name="r"/> is the ratio of the
    /// refractive index it leaves to the one it enters, or zero where it reflects whole.
    /// </summary>
    public static Vector3 Vector3Refract(Vector3 v, Vector3 n, float r)
    {
        float dot = Vector3.Dot(v, n);
        float d = 1.0f - r*r*(1.0f - dot*dot);
        if (d < 0.0f) return Vector3.Zero;
        return r*v - (r*dot + MathF.Sqrt(d))*n;
    }

    // -- Vector4

    /// <summary>A point moved toward <paramref name="target"/> by at most <paramref name="maxDistance"/>, and onto it where it is nearer.</summary>
    public static Vector4 Vector4MoveTowards(Vector4 v, Vector4 target, float maxDistance)
    {
        var d = target - v;
        float value = d.LengthSquared();
        if (value == 0 || (maxDistance >= 0 && value <= maxDistance*maxDistance)) return target;
        float dist = MathF.Sqrt(value);
        return v + d/dist*maxDistance;
    }

    /// <summary>Whether two vectors are equal within a millionth in each component, as <see cref="FloatEquals"/> is.</summary>
    public static bool Vector4Equals(Vector4 p, Vector4 q) =>
        AlmostEqual(p.X, q.X) && AlmostEqual(p.Y, q.Y) && AlmostEqual(p.Z, q.Z) && AlmostEqual(p.W, q.W);

    // -- Matrix

    /// <summary>The sum of a matrix's diagonal.</summary>
    public static float MatrixTrace(Matrix4x4 mat) => mat.M11 + mat.M22 + mat.M33 + mat.M44;

    /// <summary>
    /// The rotation by the three angles of <paramref name="angle"/> in radians, which turns a point
    /// about Z, then Y, then X, as <c>CreateRotationZ(z) * CreateRotationY(y) * CreateRotationX(x)</c> does.
    /// </summary>
    public static Matrix4x4 MatrixRotateXYZ(Vector3 angle)
    {
        float cosz = MathF.Cos(-angle.Z);
        float sinz = MathF.Sin(-angle.Z);
        float cosy = MathF.Cos(-angle.Y);
        float siny = MathF.Sin(-angle.Y);
        float cosx = MathF.Cos(-angle.X);
        float sinx = MathF.Sin(-angle.X);

        var result = Matrix4x4.Identity;
        result.M11 = cosz*cosy;
        result.M12 = (cosz*siny*sinx) - (sinz*cosx);
        result.M13 = (cosz*siny*cosx) + (sinz*sinx);
        result.M21 = sinz*cosy;
        result.M22 = (sinz*siny*sinx) + (cosz*cosx);
        result.M23 = (sinz*siny*cosx) - (cosz*sinx);
        result.M31 = -siny;
        result.M32 = cosy*sinx;
        result.M33 = cosy*cosx;
        return result;
    }

    /// <summary>
    /// The rotation by the three angles of <paramref name="angle"/> in radians, which turns a point
    /// about X, then Y, then Z, as <c>CreateRotationX(x) * CreateRotationY(y) * CreateRotationZ(z)</c> does.
    /// </summary>
    public static Matrix4x4 MatrixRotateZYX(Vector3 angle)
    {
        float cz = MathF.Cos(angle.Z);
        float sz = MathF.Sin(angle.Z);
        float cy = MathF.Cos(angle.Y);
        float sy = MathF.Sin(angle.Y);
        float cx = MathF.Cos(angle.X);
        float sx = MathF.Sin(angle.X);

        return new Matrix4x4(
            cz*cy, cy*sz, -sy, 0,
            cz*sy*sx - cx*sz, cz*cx + sz*sy*sx, cy*sx, 0,
            sz*sx + cz*cx*sy, cx*sz*sy - cz*sx, cy*cx, 0,
            0, 0, 0, 1);
    }

    /// <summary>The transform that scales by <paramref name="scale"/>, turns by <paramref name="rotation"/> and then moves by <paramref name="translation"/>.</summary>
    public static Matrix4x4 MatrixCompose(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        var right = RotateAsRayMath(Vector3.UnitX*scale.X, rotation);
        var up = RotateAsRayMath(Vector3.UnitY*scale.Y, rotation);
        var forward = RotateAsRayMath(Vector3.UnitZ*scale.Z, rotation);
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            up.X, up.Y, up.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            translation.X, translation.Y, translation.Z, 1);
    }

    // raymath's rotation of a vector by a quaternion, which Vector3.Transform matches for one of
    // length one and differs from for one that is not.
    private static Vector3 RotateAsRayMath(Vector3 v, Quaternion q) => new(
        v.X*(q.X*q.X + q.W*q.W - q.Y*q.Y - q.Z*q.Z) + v.Y*(2*q.X*q.Y - 2*q.W*q.Z) + v.Z*(2*q.X*q.Z + 2*q.W*q.Y),
        v.X*(2*q.W*q.Z + 2*q.X*q.Y) + v.Y*(q.W*q.W - q.X*q.X + q.Y*q.Y - q.Z*q.Z) + v.Z*(-2*q.W*q.X + 2*q.Y*q.Z),
        v.X*(-2*q.W*q.Y + 2*q.X*q.Z) + v.Y*(2*q.W*q.X + 2*q.Y*q.Z) + v.Z*(q.W*q.W - q.X*q.X - q.Y*q.Y + q.Z*q.Z));

    // -- Quaternion

    /// <summary>
    /// The quaternion a straight line of the way from <paramref name="q1"/> to <paramref name="q2"/>,
    /// not made of length one, where <see cref="Quaternion.Lerp"/> is raymath's QuaternionNlerp.
    /// </summary>
    public static Quaternion QuaternionLerp(Quaternion q1, Quaternion q2, float amount) =>
        new(q1.X + amount*(q2.X - q1.X), q1.Y + amount*(q2.Y - q1.Y), q1.Z + amount*(q2.Z - q1.Z), q1.W + amount*(q2.W - q1.W));

    /// <summary>The cubic Hermite spline between two rotations with their tangents at <paramref name="t"/> from 0 to 1, as glTF interpolates, of length one.</summary>
    public static Quaternion QuaternionCubicHermiteSpline(Quaternion q1, Quaternion outTangent1, Quaternion q2, Quaternion inTangent2, float t)
    {
        float t2 = t*t;
        float t3 = t2*t;
        float h00 = 2*t3 - 3*t2 + 1;
        float h10 = t3 - 2*t2 + t;
        float h01 = -2*t3 + 3*t2;
        float h11 = t3 - t2;
        var result = q1*h00 + outTangent1*h10 + q2*h01 + inTangent2*h11;
        float length = result.Length();
        return result*(1.0f/(length == 0.0f ? 1.0f : length));
    }

    /// <summary>The shortest rotation turning the direction <paramref name="from"/> to the direction <paramref name="to"/>.</summary>
    public static Quaternion QuaternionFromVector3ToVector3(Vector3 from, Vector3 to)
    {
        float cos2Theta = Vector3.Dot(from, to);
        var cross = Vector3.Cross(from, to);
        var result = new Quaternion(cross, MathF.Sqrt(cross.LengthSquared() + cos2Theta*cos2Theta) + cos2Theta);
        float length = result.Length();
        return result*(1.0f/(length == 0.0f ? 1.0f : length));
    }

    /// <summary>The axis a rotation turns about and the angle in radians it turns by, the axis X for a rotation of none.</summary>
    public static void QuaternionToAxisAngle(Quaternion q, out Vector3 outAxis, out float outAngle)
    {
        if (MathF.Abs(q.W) > 1.0f)
        {
            float length = q.Length();
            q *= 1.0f/(length == 0.0f ? 1.0f : length);
        }

        outAngle = 2.0f*MathF.Acos(q.W);
        float den = MathF.Sqrt(1.0f - q.W*q.W);
        outAxis = den > RayMathEpsilon ? new Vector3(q.X, q.Y, q.Z)/den : Vector3.UnitX;
    }

    /// <summary>
    /// The rotation by <paramref name="pitch"/> about X, <paramref name="yaw"/> about Y and
    /// <paramref name="roll"/> about Z in radians, composed as raymath composes them, which
    /// <see cref="Quaternion.CreateFromYawPitchRoll"/> does in another order.
    /// </summary>
    public static Quaternion QuaternionFromEuler(float pitch, float yaw, float roll)
    {
        float x0 = MathF.Cos(pitch*0.5f);
        float x1 = MathF.Sin(pitch*0.5f);
        float y0 = MathF.Cos(yaw*0.5f);
        float y1 = MathF.Sin(yaw*0.5f);
        float z0 = MathF.Cos(roll*0.5f);
        float z1 = MathF.Sin(roll*0.5f);

        return new Quaternion(
            x1*y0*z0 - x0*y1*z1,
            x0*y1*z0 + x1*y0*z1,
            x0*y0*z1 - x1*y1*z0,
            x0*y0*z0 + x1*y1*z1);
    }

    /// <summary>A rotation's angles about X, Y and Z in radians, as <see cref="QuaternionFromEuler"/> takes them.</summary>
    public static Vector3 QuaternionToEuler(Quaternion q)
    {
        float x0 = 2.0f*(q.W*q.X + q.Y*q.Z);
        float x1 = 1.0f - 2.0f*(q.X*q.X + q.Y*q.Y);
        float y0 = Math.Clamp(2.0f*(q.W*q.Y - q.Z*q.X), -1.0f, 1.0f);
        float z0 = 2.0f*(q.W*q.Z + q.X*q.Y);
        float z1 = 1.0f - 2.0f*(q.Y*q.Y + q.Z*q.Z);
        return new Vector3(MathF.Atan2(x0, x1), MathF.Asin(y0), MathF.Atan2(z0, z1));
    }

    /// <summary>A quaternion's four components carried through a matrix as a vector's are.</summary>
    public static Quaternion QuaternionTransform(Quaternion q, Matrix4x4 mat)
    {
        var v = Vector4.Transform(new Vector4(q.X, q.Y, q.Z, q.W), mat);
        return new Quaternion(v.X, v.Y, v.Z, v.W);
    }

    /// <summary>Whether two rotations are equal within a millionth, as a quaternion and its negative turn alike.</summary>
    public static bool QuaternionEquals(Quaternion p, Quaternion q) =>
        (AlmostEqual(p.X, q.X) && AlmostEqual(p.Y, q.Y) && AlmostEqual(p.Z, q.Z) && AlmostEqual(p.W, q.W))
        || (AlmostEqual(p.X, -q.X) && AlmostEqual(p.Y, -q.Y) && AlmostEqual(p.Z, -q.Z) && AlmostEqual(p.W, -q.W));
}
