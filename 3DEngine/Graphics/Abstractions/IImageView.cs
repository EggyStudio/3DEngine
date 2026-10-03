namespace Engine;

/// <summary>Handle to a GPU image view (typed view into an <see cref="IImage"/>).</summary>
public interface IImageView : IDisposable
{
    /// <summary>The underlying image this view references.</summary>
    IImage Image { get; }
}
