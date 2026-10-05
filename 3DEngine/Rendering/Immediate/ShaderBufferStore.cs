namespace Engine;

/// <summary>
/// The storage buffers a program made with <c>LoadShaderBuffer</c>, by id, which compute dispatches
/// write and drawing shaders read.
/// </summary>
/// <remarks>
/// Handed to the render world as it is, as <see cref="ShaderStore"/> is, so a draw resolves the
/// buffers its shader was given when it was recorded. A lock guards it, since systems on several
/// threads may load and free buffers while the frame is recorded.
/// </remarks>
internal sealed class ShaderBufferStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, IBuffer> _buffers = [];
    private int _next = 1;

    /// <summary>Keeps a buffer and gives it an id, never 0.</summary>
    public int Add(IBuffer buffer)
    {
        lock (_gate)
        {
            var id = _next++;
            _buffers[id] = buffer;
            return id;
        }
    }

    /// <summary>The buffer of an id, or null for one that is not loaded.</summary>
    public IBuffer? Get(int id)
    {
        lock (_gate) return _buffers.GetValueOrDefault(id);
    }

    /// <summary>Lets an id go, handing back its buffer for the caller to free.</summary>
    public bool Remove(int id, out IBuffer? buffer)
    {
        lock (_gate) return _buffers.Remove(id, out buffer);
    }

    /// <summary>Lets every buffer go, handing them back for the caller to free.</summary>
    public IBuffer[] TakeAll()
    {
        lock (_gate)
        {
            var all = _buffers.Values.ToArray();
            _buffers.Clear();
            return all;
        }
    }
}
