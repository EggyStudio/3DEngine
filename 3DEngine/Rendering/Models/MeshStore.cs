using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>One vertex of the model pass: a position, a normal and a texture coordinate, 32 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ModelVertex(Vector3 Position, Vector3 Normal, Vector2 Uv);

/// <summary>
/// The meshes the flat API has made or loaded, by id, with what is waiting to reach the GPU and
/// what has been unloaded, until <see cref="GpuMeshesPrepare"/> takes them on the render thread.
/// </summary>
/// <remarks>
/// The same queue as <see cref="TextureStore"/>, for vertex and index buffers. Id 0 is never given
/// out, so a default <see cref="ModelMesh"/> is recognizably not loaded.
/// </remarks>
public sealed class MeshStore
{
    /// <summary>A mesh waiting to be uploaded.</summary>
    public sealed record Upload(int Id, ModelVertex[] Vertices, uint[] Indices);

    private readonly object _gate = new();
    private readonly HashSet<int> _live = [];
    private readonly List<Upload> _uploads = [];
    private readonly List<int> _removals = [];
    private int _next = 1;

    /// <summary>How many meshes are loaded.</summary>
    public int Count
    {
        get { lock (_gate) return _live.Count; }
    }

    /// <summary>Queues a mesh of triangles and returns its id.</summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, or one is out of range.</exception>
    public int Add(ModelVertex[] vertices, uint[] indices)
    {
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("A mesh needs a whole number of triangles.", nameof(indices));
        foreach (var index in indices)
            if (index >= vertices.Length)
                throw new ArgumentException($"Index {index} is past the {vertices.Length} vertices.", nameof(indices));

        lock (_gate)
        {
            var id = _next++;
            _live.Add(id);
            _uploads.Add(new Upload(id, vertices, indices));
            return id;
        }
    }

    /// <summary>Whether <paramref name="id"/> names a loaded mesh.</summary>
    public bool Contains(int id)
    {
        lock (_gate) return _live.Contains(id);
    }

    /// <summary>Unloads a mesh. Its buffers are destroyed once no frame in flight can use them.</summary>
    public bool Remove(int id)
    {
        lock (_gate)
        {
            if (!_live.Remove(id)) return false;
            _uploads.RemoveAll(u => u.Id == id);
            _removals.Add(id);
            return true;
        }
    }

    /// <summary>Hands the queued uploads and removals to the renderer and empties the queues.</summary>
    internal (Upload[] Uploads, int[] Removals) Take()
    {
        lock (_gate)
        {
            var taken = (_uploads.ToArray(), _removals.ToArray());
            _uploads.Clear();
            _removals.Clear();
            return taken;
        }
    }
}

/// <summary>The vertex and index buffers of every mesh in the <see cref="MeshStore"/>.</summary>
public sealed class GpuMeshes : IDisposable
{
    /// <summary>One mesh's buffers.</summary>
    public sealed record Entry(IBuffer Vertices, IBuffer Indices, uint IndexCount);

    private readonly Dictionary<int, Entry> _entries = [];
    private readonly List<(long Frame, Entry Entry)> _retired = [];
    private long _frame;

    /// <summary>The buffers of mesh <paramref name="id"/>, or <c>null</c> when it is not loaded.</summary>
    public Entry? Get(int id) => _entries.GetValueOrDefault(id);

    /// <summary>Uploads queued meshes, retires unloaded ones, and destroys what has been retired long enough.</summary>
    public void Update(IGraphicsDevice gfx, MeshStore? store)
    {
        _frame++;
        if (store is not null)
        {
            var (uploads, removals) = store.Take();
            foreach (var id in removals)
                if (_entries.Remove(id, out var gone))
                    _retired.Add((_frame, gone));

            foreach (var upload in uploads)
                _entries[upload.Id] = new Entry(
                    Buffer(gfx, MemoryMarshal.AsBytes(upload.Vertices.AsSpan()), BufferUsage.Vertex),
                    Buffer(gfx, MemoryMarshal.AsBytes(upload.Indices.AsSpan()), BufferUsage.Index),
                    (uint)upload.Indices.Length);
        }

        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            Destroy(_retired[i].Entry);
            _retired.RemoveAt(i);
        }
    }

    // Host-visible buffers written through a mapping. A device-local buffer behind a staging copy
    // would draw faster, which RENDERING.md lists with the move to the memory allocator.
    private static IBuffer Buffer(IGraphicsDevice gfx, ReadOnlySpan<byte> data, BufferUsage usage)
    {
        var buffer = gfx.CreateBuffer(new BufferDesc((ulong)data.Length, usage, CpuAccessMode.Write));
        data.CopyTo(gfx.Map(buffer));
        gfx.Unmap(buffer);
        return buffer;
    }

    private static void Destroy(Entry entry)
    {
        entry.Vertices.Dispose();
        entry.Indices.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, entry) in _retired) Destroy(entry);
        foreach (var entry in _entries.Values) Destroy(entry);
        _retired.Clear();
        _entries.Clear();
    }
}

/// <summary>Prepare system that brings <see cref="GpuMeshes"/> up to date and hands it to the render world.</summary>
public sealed class GpuMeshesPrepare : IPrepareSystem, IDisposable
{
    private readonly GpuMeshes _meshes = new();

    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        _meshes.Update(renderContext.Device, renderWorld.TryGet<MeshStore>());
        renderWorld.Set(_meshes);
    }

    /// <inheritdoc />
    public void Dispose() => _meshes.Dispose();
}
