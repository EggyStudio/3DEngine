namespace Engine;

/// <summary>Handle to a GPU image resource (texture, render target, depth buffer).</summary>
public interface IImage : IDisposable
{
    /// <summary>The descriptor used to create this image.</summary>
    ImageDesc Description { get; }
}
