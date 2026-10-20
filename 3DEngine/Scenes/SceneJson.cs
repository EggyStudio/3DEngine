using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine;

/// <summary>The JSON forms of the values scene files hold, which the generated code calls.</summary>
public static class SceneJson
{
    /// <summary>
    /// The key a component's object holds the version of its layout under, written where its
    /// <see cref="DataVersionAttribute"/> is past 1.
    /// </summary>
    public const string VersionName = "$version";

    /// <summary>
    /// A component's object brought up to <paramref name="version"/> through
    /// <paramref name="migrate"/> from the version it holds, or itself where it holds that version
    /// or a later one.
    /// </summary>
    /// <remarks>
    /// An object with no <see cref="VersionName"/> is version 1. <paramref name="migrate"/> is
    /// called once, with the version the object holds and the object without its
    /// <see cref="VersionName"/>, and gives it back as <paramref name="version"/> holds it.
    /// </remarks>
    /// <param name="e">The component's object as the file holds it.</param>
    /// <param name="version">The version the program reads.</param>
    /// <param name="migrate">The type's <c>Migrate</c>.</param>
    /// <param name="type">The type's name, for the message of a migration that fails.</param>
    /// <exception cref="InvalidDataException">The version is not a whole number from 1, or a migration threw or gave nothing back.</exception>
    public static JsonElement Migrated(JsonElement e, int version, Func<int, JsonObject, JsonObject> migrate, string type)
    {
        var from = VersionOf(e, type);
        if (from >= version) return e;

        var value = JsonObject.Create(e)!;
        value.Remove(VersionName);
        try
        {
            value = migrate(from, value);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"{type}'s migration from version {from} failed: {ex.Message}", ex);
        }
        if (value is null) throw new InvalidDataException($"{type}'s migration from version {from} gave back nothing.");

        // Written out and parsed again, since a reader takes an element. A node a migration made
        // from a value writes it through the converter it was made with, which needs no reflection.
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) value.WriteTo(writer);
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    // The version a component's object holds, 1 where it holds none.
    internal static int VersionOf(JsonElement e, string type)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(VersionName, out var held)) return 1;
        return held.ValueKind == JsonValueKind.Number && held.TryGetInt32(out var number) && number >= 1
            ? number
            : throw new InvalidDataException($"{type}'s \"{VersionName}\" is {held.GetRawText()}, not a whole number from 1.");
    }

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
