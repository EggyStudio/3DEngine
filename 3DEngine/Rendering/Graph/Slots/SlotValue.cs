namespace Engine;

/// <summary>Runtime value carried through a slot edge between render graph nodes.</summary>
public readonly struct SlotValue
{
    private readonly object? _value;

    /// <summary>The type of data stored in this slot value.</summary>
    public readonly SlotType Type;

    private SlotValue(object? value, SlotType type) { _value = value; Type = type; }

    /// <summary>Creates a slot value wrapping an image view.</summary>
    public static SlotValue TextureView(IImageView view) => new(view, SlotType.TextureView);

    /// <summary>Creates a slot value wrapping a sampler.</summary>
    public static SlotValue Sampler(ISampler sampler) => new(sampler, SlotType.Sampler);

    /// <summary>Creates a slot value wrapping a buffer.</summary>
    public static SlotValue Buffer(IBuffer buffer) => new(buffer, SlotType.Buffer);

    /// <summary>Creates a slot value wrapping an entity identifier.</summary>
    public static SlotValue Entity(int entity) => new(entity, SlotType.Entity);

    /// <summary>Extracts the stored image view. Throws if the type is wrong.</summary>
    public IImageView AsTextureView() => (IImageView)_value!;

    /// <summary>Extracts the stored sampler. Throws if the type is wrong.</summary>
    public ISampler AsSampler() => (ISampler)_value!;

    /// <summary>Extracts the stored buffer. Throws if the type is wrong.</summary>
    public IBuffer AsBuffer() => (IBuffer)_value!;

    /// <summary>Extracts the stored entity identifier. Throws if the type is wrong.</summary>
    public int AsEntity() => (int)_value!;
}
