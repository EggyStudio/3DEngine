using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Engine;

/// <summary>Writes meshes as a glTF 2.0 file, its JSON and its buffer, one primitive a material.</summary>
/// <remarks>
/// <para>
/// The buffer holds each primitive's positions, normals and texture coordinates as floats and its
/// triangles as 32-bit indices, each a view of its own on a four-byte boundary, and is written as a
/// <c>.bin</c> file beside the JSON or into the JSON as a data URI. A position accessor carries its
/// bounds, which the format asks of it. Texture coordinates run down from the top of an image, as
/// glTF's do and the engine's do, so they are written as they are.
/// </para>
/// <para>
/// Each material is a base color, not metal and fully rough, named as its slot is.
/// </para>
/// </remarks>
internal static class GltfWriter
{
    /// <summary>One primitive, its material's name and color, and its vertices and triangles.</summary>
    public sealed record Primitive(string Material, Vector4 Color, ModelVertex[] Vertices, uint[] Indices);

    /// <summary>Writes the primitives to a <c>.gltf</c> file, and its buffer beside it unless <paramref name="embed"/> puts it in the file.</summary>
    /// <remarks>Each file is written under a name of its own and moved into place, so a reader sees the old file or the new one whole.</remarks>
    public static void Write(string path, string name, IReadOnlyList<Primitive> primitives, bool embed)
    {
        // The buffer, each primitive's attributes and then its indices.
        var buffer = new MemoryStream();
        var views = new List<(int Offset, int Length, int Target)>();
        int View(ReadOnlySpan<byte> bytes, int target)
        {
            while (buffer.Length % 4 != 0) buffer.WriteByte(0);
            views.Add(((int)buffer.Length, bytes.Length, target));
            buffer.Write(bytes);
            return views.Count - 1;
        }

        const int ArrayBuffer = 34962, ElementArrayBuffer = 34963;
        var accessors = new List<Action<Utf8JsonWriter>>();
        var meshes = new List<(int Position, int Normal, int Uv, int Indices, int Material)>();
        for (int p = 0; p < primitives.Count; p++)
        {
            var (vertices, indices) = (primitives[p].Vertices, primitives[p].Indices);
            var positions = vertices.Select(vertex => vertex.Position).ToArray();
            var normals = vertices.Select(vertex => vertex.Normal).ToArray();
            var uvs = vertices.Select(vertex => vertex.Uv).ToArray();
            var (low, high) = (positions.Aggregate(Vector3.Min), positions.Aggregate(Vector3.Max));

            int Accessor(int view, int componentType, int count, string type, Vector3? min = null, Vector3? max = null)
            {
                accessors.Add(json =>
                {
                    json.WriteStartObject();
                    json.WriteNumber("bufferView", view);
                    json.WriteNumber("componentType", componentType);
                    json.WriteNumber("count", count);
                    json.WriteString("type", type);
                    if (min is { } at)
                    {
                        WriteVector(json, "min", at);
                        WriteVector(json, "max", max!.Value);
                    }
                    json.WriteEndObject();
                });
                return accessors.Count - 1;
            }

            const int Float = 5126, UnsignedInt = 5125;
            var position = Accessor(View(MemoryMarshal.AsBytes(positions.AsSpan()), ArrayBuffer), Float, positions.Length, "VEC3", low, high);
            var normal = Accessor(View(MemoryMarshal.AsBytes(normals.AsSpan()), ArrayBuffer), Float, normals.Length, "VEC3");
            var uv = Accessor(View(MemoryMarshal.AsBytes(uvs.AsSpan()), ArrayBuffer), Float, uvs.Length, "VEC2");
            var index = Accessor(View(MemoryMarshal.AsBytes(indices.AsSpan()), ElementArrayBuffer), UnsignedInt, indices.Length, "SCALAR");
            meshes.Add((position, normal, uv, index, p));
        }
        while (buffer.Length % 4 != 0) buffer.WriteByte(0);

        var full = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        if (Path.GetDirectoryName(full) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var bin = Path.ChangeExtension(full, ".bin");
        var uri = embed ? "data:application/octet-stream;base64," + Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length) : Path.GetFileName(bin);

        var text = new MemoryStream();
        using (var json = new Utf8JsonWriter(text, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteStartObject("asset");
            json.WriteString("version", "2.0");
            json.WriteString("generator", "3DEngine");
            json.WriteEndObject();
            json.WriteNumber("scene", 0);
            json.WriteStartArray("scenes");
            json.WriteStartObject();
            json.WriteStartArray("nodes");
            json.WriteNumberValue(0);
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteStartArray("nodes");
            json.WriteStartObject();
            json.WriteString("name", name);
            json.WriteNumber("mesh", 0);
            json.WriteEndObject();
            json.WriteEndArray();

            json.WriteStartArray("meshes");
            json.WriteStartObject();
            json.WriteString("name", name);
            json.WriteStartArray("primitives");
            foreach (var (position, normal, uv, index, material) in meshes)
            {
                json.WriteStartObject();
                json.WriteStartObject("attributes");
                json.WriteNumber("POSITION", position);
                json.WriteNumber("NORMAL", normal);
                json.WriteNumber("TEXCOORD_0", uv);
                json.WriteEndObject();
                json.WriteNumber("indices", index);
                json.WriteNumber("material", material);
                json.WriteNumber("mode", 4);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();

            json.WriteStartArray("materials");
            foreach (var primitive in primitives)
            {
                json.WriteStartObject();
                json.WriteString("name", primitive.Material);
                json.WriteStartObject("pbrMetallicRoughness");
                json.WriteStartArray("baseColorFactor");
                foreach (var channel in new[] { primitive.Color.X, primitive.Color.Y, primitive.Color.Z, primitive.Color.W }) json.WriteNumberValue(channel);
                json.WriteEndArray();
                json.WriteNumber("metallicFactor", 0);
                json.WriteNumber("roughnessFactor", 1);
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("buffers");
            json.WriteStartObject();
            json.WriteNumber("byteLength", buffer.Length);
            json.WriteString("uri", uri);
            json.WriteEndObject();
            json.WriteEndArray();

            json.WriteStartArray("bufferViews");
            foreach (var (offset, length, target) in views)
            {
                json.WriteStartObject();
                json.WriteNumber("buffer", 0);
                json.WriteNumber("byteOffset", offset);
                json.WriteNumber("byteLength", length);
                json.WriteNumber("target", target);
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("accessors");
            foreach (var accessor in accessors) accessor(json);
            json.WriteEndArray();
            json.WriteEndObject();
        }

        if (!embed) Replace(bin, buffer.ToArray());
        Replace(full, text.ToArray());
    }

    private static void WriteVector(Utf8JsonWriter json, string name, Vector3 value)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(value.X);
        json.WriteNumberValue(value.Y);
        json.WriteNumberValue(value.Z);
        json.WriteEndArray();
    }

    // Writes a file under a name of its own and moves it into place.
    private static void Replace(string path, byte[] bytes)
    {
        var part = path + ".part";
        File.WriteAllBytes(part, bytes);
        File.Move(part, path, overwrite: true);
    }
}
