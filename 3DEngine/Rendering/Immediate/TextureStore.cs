namespace Engine;

/// <summary>How a texture is sampled between its pixels.</summary>
public enum TextureFilter
{
    /// <summary>The nearest pixel, so pixel art stays sharp.</summary>
    Point,

    /// <summary>A blend of the four nearest pixels.</summary>
    Bilinear,
}

/// <summary>
/// The textures the flat API has loaded, by id. Holds what is waiting to reach the GPU, and the
/// ids that have been unloaded, until <see cref="ImmediateNode"/> takes them on the render thread.
/// </summary>
/// <remarks>
/// <para>
/// Loading and unloading happen wherever the program calls them, while the GPU objects belong to
/// the renderer, which creates and destroys them in <see cref="Stage.Last"/>. The store is the
/// queue between the two, and every member takes a lock, because a system that loads a texture
/// may run on a worker thread.
/// </para>
/// <para>
/// Id 0 is never given out, so a default <see cref="Texture2D"/> is recognizably not loaded, and
/// the node uses 0 for the white texture untextured shapes sample.
/// </para>
/// </remarks>
public sealed class TextureStore
{
    /// <summary>A change waiting to reach a texture's GPU objects: new pixels, a new filter, or both.</summary>
    /// <param name="Id">The texture's id.</param>
    /// <param name="Rgba">Four bytes per pixel, rows from the top, or <c>null</c> when only the filter changed.</param>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    /// <param name="Filter">How the texture is sampled.</param>
    public sealed record Upload(int Id, byte[]? Rgba, int Width, int Height, TextureFilter Filter);

    private readonly object _gate = new();
    private readonly Dictionary<int, (int Width, int Height, TextureFilter Filter)> _live = [];
    private readonly List<Upload> _uploads = [];
    private readonly List<int> _removals = [];
    private int _next = 1;

    /// <summary>How many textures are loaded.</summary>
    public int Count
    {
        get { lock (_gate) return _live.Count; }
    }

    /// <summary>Queues a new texture and returns its id.</summary>
    /// <exception cref="ArgumentException">The pixel array does not hold <paramref name="width"/> by <paramref name="height"/> pixels.</exception>
    public int Add(byte[] rgba, int width, int height, TextureFilter filter = TextureFilter.Bilinear)
    {
        Validate(rgba, width, height);
        lock (_gate)
        {
            var id = _next++;
            _live[id] = (width, height, filter);
            _uploads.Add(new Upload(id, rgba, width, height, filter));
            return id;
        }
    }

    /// <summary>Queues new pixels for a loaded texture of the same size.</summary>
    /// <returns>Whether the texture is loaded and the size matched.</returns>
    public bool Update(int id, byte[] rgba)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var texture) || rgba.Length != texture.Width * texture.Height * 4)
                return false;
            _uploads.Add(new Upload(id, rgba, texture.Width, texture.Height, texture.Filter));
            return true;
        }
    }

    /// <summary>Changes how a loaded texture is sampled. The pixels are kept.</summary>
    /// <returns>Whether the texture is loaded.</returns>
    public bool SetFilter(int id, TextureFilter filter)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var texture)) return false;
            _live[id] = texture with { Filter = filter };
            _uploads.Add(new Upload(id, null, texture.Width, texture.Height, filter));
            return true;
        }
    }

    /// <summary>Whether <paramref name="id"/> names a loaded texture.</summary>
    public bool Contains(int id)
    {
        lock (_gate) return _live.ContainsKey(id);
    }

    /// <summary>Unloads a texture. Its GPU objects are destroyed once no frame in flight can use them.</summary>
    /// <returns>Whether the texture was loaded.</returns>
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

    private static void Validate(byte[] rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != width * height * 4)
            throw new ArgumentException($"Expected {width} by {height} pixels of four bytes, and got {rgba.Length} bytes.", nameof(rgba));
    }
}
