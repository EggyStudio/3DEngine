namespace Engine;

/// <summary>Handle to a GPU sampler resource.</summary>
public interface ISampler : IDisposable
{
    /// <summary>The descriptor used to create this sampler.</summary>
    SamplerDesc Description { get; }
}
