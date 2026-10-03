namespace Engine;

/// <summary>Types of data that can flow through render graph slot edges.</summary>
public enum SlotType
{
    /// <summary>Default / unset - no value has been assigned to this slot.</summary>
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
