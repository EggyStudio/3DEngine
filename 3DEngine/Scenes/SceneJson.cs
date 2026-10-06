using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>The JSON forms of the values scene files hold, which the generated code calls.</summary>
public static class SceneJson
{
    /// <summary>
    /// Writes a vector as an array of its components, as an element of the open array when
    /// <paramref name="name"/> is null.
    /// </summary>
    public static void Write(Utf8JsonWriter writer, string? name, Vector2 v) => Floats(writer, name, v.X, v.Y);

    /// <inheritdoc cref="Write(Utf8JsonWriter, string, Vector2)"/>
    public static void Write(Utf8JsonWriter writer, string? name, Vector3 v) => Floats(writer, name, v.X, v.Y, v.Z);

    /// <inheritdoc cref="Write(Utf8JsonWriter, string, Vector2)"/>
    public static void Write(Utf8JsonWriter writer, string? name, Vector4 v) => Floats(writer, name, v.X, v.Y, v.Z, v.W);

    /// <summary>Writes a rotation as [x, y, z, w].</summary>
    public static void Write(Utf8JsonWriter writer, string? name, Quaternion q) => Floats(writer, name, q.X, q.Y, q.Z, q.W);

    /// <summary>Writes a matrix as its sixteen numbers, row by row.</summary>
    public static void Write(Utf8JsonWriter writer, string? name, Matrix4x4 m) =>
        Floats(writer, name, m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

    /// <summary>Writes a color as [r, g, b, a], each from 0 to 255.</summary>
    public static void Write(Utf8JsonWriter writer, string? name, Color c)
    {
        Start(writer, name);
        writer.WriteNumberValue(c.R);
        writer.WriteNumberValue(c.G);
        writer.WriteNumberValue(c.B);
        writer.WriteNumberValue(c.A);
        writer.WriteEndArray();
    }

    /// <summary>Reads a vector written by <see cref="Write(Utf8JsonWriter, string, Vector2)"/>.</summary>
    public static Vector2 ReadVector2(JsonElement e) => new(At(e, 0), At(e, 1));

    /// <summary>Reads a vector written by <see cref="Write(Utf8JsonWriter, string, Vector3)"/>.</summary>
    public static Vector3 ReadVector3(JsonElement e) => new(At(e, 0), At(e, 1), At(e, 2));

    /// <summary>Reads a vector written by <see cref="Write(Utf8JsonWriter, string, Vector4)"/>.</summary>
    public static Vector4 ReadVector4(JsonElement e) => new(At(e, 0), At(e, 1), At(e, 2), At(e, 3));

    /// <summary>Reads a rotation written by <see cref="Write(Utf8JsonWriter, string, Quaternion)"/>.</summary>
    public static Quaternion ReadQuaternion(JsonElement e) => new(At(e, 0), At(e, 1), At(e, 2), At(e, 3));

    /// <summary>Reads a matrix written by <see cref="Write(Utf8JsonWriter, string, Matrix4x4)"/>.</summary>
    public static Matrix4x4 ReadMatrix4x4(JsonElement e) => new(
        At(e, 0), At(e, 1), At(e, 2), At(e, 3), At(e, 4), At(e, 5), At(e, 6), At(e, 7),
        At(e, 8), At(e, 9), At(e, 10), At(e, 11), At(e, 12), At(e, 13), At(e, 14), At(e, 15));

    /// <summary>Reads a color written by <see cref="Write(Utf8JsonWriter, string, Color)"/>.</summary>
    public static Color ReadColor(JsonElement e) =>
        new((byte)At(e, 0), (byte)At(e, 1), (byte)At(e, 2), e.GetArrayLength() > 3 ? (byte)At(e, 3) : (byte)255);

    private static void Floats(Utf8JsonWriter writer, string? name, params ReadOnlySpan<float> values)
    {
        Start(writer, name);
        foreach (var v in values) writer.WriteNumberValue(v);
        writer.WriteEndArray();
    }

    private static void Start(Utf8JsonWriter writer, string? name)
    {
        if (name is null) writer.WriteStartArray();
        else writer.WriteStartArray(name);
    }

    private static float At(JsonElement e, int index) => index < e.GetArrayLength() ? e[index].GetSingle() : 0f;
}
