namespace Engine;

/// <summary>Types of data that can flow through render graph slot edges.</summary>
public enum SlotType
{
    /// <summary>The default, when no value has been assigned to the slot.</summary>
    None = 0,
    /// <summary>A GPU image view (texture).</summary>
    TextureView,
    /// <summary>A GPU sampler.</summary>
    Sampler,
    /// <summary>A GPU buffer.</summary>
    Buffer,
    /// <summary>An entity identifier.</summary>
    Entity
}
