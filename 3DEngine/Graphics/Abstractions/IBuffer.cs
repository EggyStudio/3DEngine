namespace Engine;

/// <summary>Handle to a GPU buffer resource.</summary>
internal interface IBuffer : IDisposable
{
    /// <summary>The descriptor used to create this buffer.</summary>
    BufferDesc Description { get; }
}
