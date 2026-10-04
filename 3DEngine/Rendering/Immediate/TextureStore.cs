namespace Engine;

/// <summary>How a texture is sampled between its pixels.</summary>
public enum TextureFilter
{
    /// <summary>The nearest pixel, so pixel art stays sharp.</summary>
    Point,

    /// <summary>A blend of the four nearest pixels, and of the two nearest mip levels where there are mip levels.</summary>
    Bilinear,

    /// <summary>Bilinear, with up to 4 samples along the direction a slanted texture is squashed in, so it stays sharp at a glancing angle.</summary>
    Anisotropic4x,

    /// <summary>Bilinear, with up to 8 samples along the squashed direction.</summary>
    Anisotropic8x,

    /// <summary>Bilinear, with up to 16 samples along the squashed direction, or as many as the device allows.</summary>
    Anisotropic16x,
}

/// <summary>What a texture shows past its edges, where a texture coordinate leaves 0 to 1.</summary>
/// <remarks>
/// raylib's mirror clamp is left out, since Vulkan has it only where the device turns on a feature
/// for it.
/// </remarks>
public enum TextureWrap
{
    /// <summary>The texture again, tiled, as a floor of repeated tiles is drawn.</summary>
    Repeat,
    /// <summary>The edge pixel stretched on, so a sprite's edge does not take color from its far side.</summary>
    Clamp,
    /// <summary>The texture again, mirrored across each edge.</summary>
    MirrorRepeat,
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
    /// <summary>A change waiting to reach a texture's GPU objects: new pixels, a new filter or wrap, or both.</summary>
    /// <param name="Id">The texture's id.</param>
    /// <param name="Rgba">Four bytes per pixel, rows from the top, or <c>null</c> when only the filter changed.</param>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    /// <param name="Filter">How the texture is sampled.</param>
    /// <param name="Target">Whether the texture is a render target, drawn into rather than uploaded.</param>
    /// <param name="Mipmaps">
    /// Whether the texture has mip levels. With <paramref name="Rgba"/> <c>null</c> on a texture
    /// already uploaded without them, they are made from what is on the GPU.
    /// </param>
    /// <param name="DepthOf">The render target whose depth the texture samples, or 0 when it is not one's depth.</param>
    /// <param name="Wrap">What the texture shows past its edges.</param>
    /// <param name="Offset">
    /// Where <paramref name="Rgba"/>, <paramref name="Width"/> by <paramref name="Height"/> pixels,
    /// goes in a texture already on the GPU, or null when it is the whole texture.
    /// </param>
    public sealed record Upload(int Id, byte[]? Rgba, int Width, int Height, TextureFilter Filter, bool Target = false, bool Mipmaps = false,
        int DepthOf = 0, TextureWrap Wrap = TextureWrap.Repeat, (int X, int Y)? Offset = null);

    private readonly object _gate = new();
    private readonly Dictionary<int, (int Width, int Height, TextureFilter Filter, bool Mipmaps, TextureWrap Wrap)> _live = [];

    // The textures with a pixel neither clear nor solid, which a blended material draws with
    // what is behind it. A texture that is only clear or solid cuts out as it is.
    private readonly HashSet<int> _translucent = [];
    private readonly List<Upload> _uploads = [];
    private readonly List<int> _removals = [];
    private int _next = 1;

    /// <summary>How many textures are loaded.</summary>
    public int Count
    {
        get { lock (_gate) return _live.Count; }
    }

    /// <summary>Queues a new texture, with mip levels when <paramref name="mipmaps"/> is set, and returns its id.</summary>
    /// <exception cref="ArgumentException">The pixel array does not hold <paramref name="width"/> by <paramref name="height"/> pixels.</exception>
    public int Add(byte[] rgba, int width, int height, TextureFilter filter = TextureFilter.Bilinear, bool mipmaps = false)
    {
        Validate(rgba, width, height);
        lock (_gate)
        {
            var id = _next++;
            _live[id] = (width, height, filter, mipmaps, TextureWrap.Repeat);
            _uploads.Add(new Upload(id, rgba, width, height, filter, Mipmaps: mipmaps));
            if (HasPartialAlpha(rgba)) _translucent.Add(id);
            return id;
        }
    }

    /// <summary>Gives a loaded texture mip levels, made on the GPU from its pixels.</summary>
    /// <returns>Whether the texture is loaded and can have them. A render target cannot.</returns>
    public bool GenerateMipmaps(int id)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var texture)) return false;
            if (_uploads.Any(u => u.Id == id && u.Target)) return false;
            if (texture.Mipmaps) return true;
            _live[id] = texture with { Mipmaps = true };

            // Pixels still waiting to go up are sent with mip levels instead of being copied twice.
            var pending = _uploads.FindLastIndex(u => u.Id == id && u.Rgba is not null);
            if (pending >= 0) _uploads[pending] = _uploads[pending] with { Mipmaps = true };
            else _uploads.Add(new Upload(id, null, texture.Width, texture.Height, texture.Filter, Mipmaps: true, Wrap: texture.Wrap));
            return true;
        }
    }

    /// <summary>Whether a loaded texture has mip levels.</summary>
    public bool HasMipmaps(int id)
    {
        lock (_gate) return _live.TryGetValue(id, out var texture) && texture.Mipmaps;
    }

    /// <summary>Queues a render target of the given size and returns its id, which is also its texture's id.</summary>
    public int AddTarget(int width, int height, TextureFilter filter = TextureFilter.Bilinear)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "A render target needs a size.");
        lock (_gate)
        {
            var id = _next++;
            _live[id] = (width, height, filter, false, TextureWrap.Repeat);
            _uploads.Add(new Upload(id, null, width, height, filter, Target: true));
            return id;
        }
    }

    /// <summary>Queues a texture that samples the depth of render target <paramref name="target"/>, and returns its id.</summary>
    /// <remarks>It is point filtered, since a depth blended with the background's is a distance nothing is at.</remarks>
    public int AddTargetDepth(int target)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(target, out var color)) throw new ArgumentException("No render target has that id.", nameof(target));
            var id = _next++;
            _live[id] = (color.Width, color.Height, TextureFilter.Point, false, TextureWrap.Repeat);
            _uploads.Add(new Upload(id, null, color.Width, color.Height, TextureFilter.Point, DepthOf: target));
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
            _uploads.Add(new Upload(id, rgba, texture.Width, texture.Height, texture.Filter, Mipmaps: texture.Mipmaps, Wrap: texture.Wrap));
            if (HasPartialAlpha(rgba)) _translucent.Add(id);
            else _translucent.Remove(id);
            return true;
        }
    }

    /// <summary>Queues new pixels for a rectangle of a loaded texture, the rest kept.</summary>
    /// <returns>Whether the texture is loaded and the rectangle lies inside it.</returns>
    public bool UpdateRegion(int id, byte[] rgba, int x, int y, int width, int height)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var texture) || width <= 0 || height <= 0 || x < 0 || y < 0
                || x + width > texture.Width || y + height > texture.Height || rgba.Length != width * height * 4)
                return false;
            _uploads.Add(new Upload(id, rgba, width, height, texture.Filter, Mipmaps: texture.Mipmaps, Wrap: texture.Wrap, Offset: (x, y)));
            // A rectangle with partial alpha makes the texture translucent, and one without
            // leaves it as it was, since the pixels outside it are not known here.
            if (HasPartialAlpha(rgba)) _translucent.Add(id);
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
            _uploads.Add(new Upload(id, null, texture.Width, texture.Height, filter, Mipmaps: texture.Mipmaps, Wrap: texture.Wrap));
            return true;
        }
    }

    /// <summary>Changes what a loaded texture shows past its edges. The pixels are kept.</summary>
    /// <returns>Whether the texture is loaded.</returns>
    public bool SetWrap(int id, TextureWrap wrap)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var texture)) return false;
            _live[id] = texture with { Wrap = wrap };
            _uploads.Add(new Upload(id, null, texture.Width, texture.Height, texture.Filter, Mipmaps: texture.Mipmaps, Wrap: wrap));
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
            _translucent.Remove(id);
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

    /// <summary>Whether texture <paramref name="id"/> has a pixel neither clear nor solid.</summary>
    public bool IsTranslucent(int id)
    {
        lock (_gate) return _translucent.Contains(id);
    }

    private static bool HasPartialAlpha(byte[] rgba)
    {
        for (int i = 3; i < rgba.Length; i += 4)
            if (rgba[i] is not (0 or 255)) return true;
        return false;
    }
}
