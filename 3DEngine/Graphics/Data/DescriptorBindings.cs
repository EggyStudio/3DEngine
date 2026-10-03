namespace Engine;

/// <summary>Descriptor resource type for layout bindings.</summary>
public enum DescriptorType
{
    /// <summary>Uniform buffer (UBO).</summary>
    UniformBuffer,
    /// <summary>Combined image sampler (texture + sampler).</summary>
    CombinedImageSampler
}

/// <summary>Describes a single binding within a descriptor set layout.</summary>
/// <param name="Binding">Shader binding slot index.</param>
/// <param name="Type">The type of descriptor resource.</param>
/// <param name="Stages">Shader stages that access this binding.</param>
/// <param name="Count">Number of descriptors in this binding (usually 1).</param>
public readonly record struct DescriptorSetLayoutBinding(uint Binding, DescriptorType Type, ShaderStageFlags Stages, uint Count = 1);

/// <summary>Binding descriptor for a uniform buffer within a descriptor set.</summary>
/// <param name="Buffer">The uniform buffer to bind.</param>
/// <param name="Binding">Shader binding slot index.</param>
/// <param name="Offset">Byte offset into the buffer.</param>
/// <param name="Size">Byte size of the bound range.</param>
public readonly record struct UniformBufferBinding(IBuffer Buffer, uint Binding, ulong Offset, ulong Size);

/// <summary>Binding descriptor for a combined image sampler within a descriptor set.</summary>
/// <param name="ImageView">The image view providing the texture data.</param>
/// <param name="Sampler">The sampler defining filtering and addressing modes.</param>
/// <param name="Binding">Shader binding slot index.</param>
public readonly record struct CombinedImageSamplerBinding(IImageView ImageView, ISampler Sampler, uint Binding);
