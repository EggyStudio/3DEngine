namespace Engine;

/// <summary>
/// The picture the scene is drawn as and how it stands on the window, which the render settings
/// hold and <c>E3D.SetPicture</c> sets whole. The scene is drawn at <see cref="Size"/> where a
/// game holds one, and otherwise at the window's size in pixels times <see cref="Scale"/>, the
/// largest picture of <see cref="Aspect"/>'s shape inside it, and the picture is then brought onto
/// the window, where the interface is drawn at the window's own size.
/// </summary>
public readonly record struct Picture
{
    /// <summary>The picture at the window's size in its shape, fitted and filtered
    /// linearly.</summary>
    public Picture() { }

    /// <summary>
    /// The share of the window's size in pixels the scene is drawn at, from 0.5 to 2
    /// (<c>render.scale</c>), kept and not applied while <see cref="Size"/> holds a size.
    /// </summary>
    public float Scale { get; init; } = 1f;

    /// <summary>
    /// The size in pixels the scene is drawn at whatever the window's size and <see cref="Scale"/>,
    /// which only a game's code sets, or null where the scale gives the size.
    /// </summary>
    public (int Width, int Height)? Size { get; init; }

    /// <summary>
    /// The picture's shape as its width to its height, the largest picture of it standing inside
    /// the size the scale or <see cref="Size"/> gives, or null for that size's own shape
    /// (<c>render.aspect</c>).
    /// </summary>
    public (int Width, int Height)? Aspect { get; init; }

    /// <summary>How the picture covers the window where their shapes differ
    /// (<c>render.fit</c>).</summary>
    public PictureFit Fit { get; init; }

    /// <summary>How the picture is brought up onto a larger window
    /// (<c>render.filter</c>).</summary>
    public PictureFilter Filter { get; init; }
}
