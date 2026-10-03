using System.Collections.Concurrent;
using System.Numerics;

namespace Engine;

/// <summary>GPU resource registry mapping logical mesh entity IDs to uploaded vertex buffers.</summary>
/// <seealso cref="Mesh"/>
/// <seealso cref="IBuffer"/>
public sealed class MeshGpuRegistry
{
    private readonly ConcurrentDictionary<int, IBuffer> _vertexBuffers = new();

    /// <summary>Attempts to retrieve an existing vertex buffer for the given entity.</summary>
    /// <param name="entityId">The entity ID owning the mesh.</param>
    /// <param name="buffer">When returning <c>true</c>, the vertex buffer; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
    public bool TryGet(int entityId, out IBuffer buffer) => 
        _vertexBuffers.TryGetValue(entityId, out buffer!);

    /// <summary>Gets the existing vertex buffer for <paramref name="entityId"/>, or creates and uploads a new one.</summary>
    /// <param name="entityId">The entity ID owning the mesh.</param>
    /// <param name="mesh">The mesh data to upload if no buffer exists yet.</param>
    /// <param name="device">The graphics device for buffer creation and upload.</param>
    /// <returns>The vertex buffer (existing or newly created).</returns>
    public IBuffer GetOrCreate(int entityId, Mesh mesh, IGraphicsDevice device) =>
        _vertexBuffers.GetOrAdd(entityId, _ => CreateVertexBuffer(mesh, device));

    private static IBuffer CreateVertexBuffer(Mesh mesh, IGraphicsDevice device)
    {
        if (mesh.Positions is null || mesh.Positions.Length == 0)
            throw new InvalidOperationException("Mesh has no vertex positions.");

        int count = mesh.Positions.Length;
        int stride = System.Runtime.InteropServices.Marshal.SizeOf<Vector3>();
        ulong size = (ulong)(count * stride);

        var desc = new BufferDesc(size, BufferUsage.Vertex | BufferUsage.TransferDst, CpuAccessMode.None);
        var buffer = device.CreateBuffer(desc);

        if (device is GraphicsDevice gd)
        {
            Span<byte> temp = stackalloc byte[(int)size];
            var spanVec = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Vector3>(temp);
            mesh.Positions.AsSpan().CopyTo(spanVec);
            gd.UploadBuffer(buffer, temp, 0);
        }

        return buffer;
    }
}