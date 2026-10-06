namespace Engine;

/// <summary>Descriptor resource type for layout bindings.</summary>
internal enum DescriptorType
{
    /// <summary>Uniform buffer (UBO).</summary>
    UniformBuffer,
    /// <summary>Combined image sampler (texture + sampler).</summary>
    CombinedImageSampler,
    /// <summary>
    /// Uniform buffer whose offset is given when the set is bound, so one set serves every draw
    /// that reads its own slice of one buffer.
    /// </summary>
    UniformBufferDynamic,
    /// <summary>Storage buffer (SSBO), which a shader declares as a <c>StructuredBuffer</c>.</summary>
    StorageBuffer,
    /// <summary>Storage image, which a shader declares as a <c>RWTexture2D</c> and writes.</summary>
    StorageImage,
    /// <summary>A texture a shader declares as a <c>Texture2D</c>, read through a sampler declared apart from it.</summary>
    SampledImage,
    /// <summary>A sampler a shader declares on its own, as a <c>SamplerState</c>.</summary>
    Sampler,
}

/// <summary>Describes a single binding within a descriptor set layout.</summary>
/// <param name="Binding">Shader binding slot index.</param>
/// <param name="Type">The type of descriptor resource.</param>
/// <param name="Stages">Shader stages that access this binding.</param>
/// <param name="Count">Number of descriptors in this binding (usually 1).</param>
internal readonly record struct DescriptorSetLayoutBinding(uint Binding, DescriptorType Type, ShaderStageFlags Stages, uint Count = 1);

/// <summary>Binding descriptor for a uniform buffer within a descriptor set.</summary>
/// <param name="Buffer">The uniform buffer to bind.</param>
/// <param name="Binding">Shader binding slot index.</param>
/// <param name="Offset">Byte offset into the buffer.</param>
/// <param name="Size">Byte size of the bound range.</param>
/// <param name="Dynamic">Whether the binding is a <see cref="DescriptorType.UniformBufferDynamic"/>, whose bind adds an offset to <paramref name="Offset"/>.</param>
internal readonly record struct UniformBufferBinding(IBuffer Buffer, uint Binding, ulong Offset, ulong Size, bool Dynamic = false);

/// <summary>Binding descriptor for a whole storage buffer within a descriptor set.</summary>
/// <param name="Buffer">The storage buffer to bind.</param>
/// <param name="Binding">Shader binding slot index.</param>
internal readonly record struct StorageBufferBinding(IBuffer Buffer, uint Binding);

/// <summary>Binding descriptor for a texture and its sampler within a descriptor set.</summary>
/// <param name="ImageView">The image view providing the texture data.</param>
/// <param name="Sampler">The sampler defining filtering and addressing modes.</param>
/// <param name="Binding">Shader binding slot index.</param>
/// <param name="Type">
/// How the shader declares the binding: both as a combined image sampler, the image alone as a
/// <see cref="DescriptorType.SampledImage"/>, or the sampler alone as a <see cref="DescriptorType.Sampler"/>.
/// </param>
internal readonly record struct CombinedImageSamplerBinding(IImageView ImageView, ISampler Sampler, uint Binding,
    DescriptorType Type = DescriptorType.CombinedImageSampler);
