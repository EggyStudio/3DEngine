namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    private (uint Samplers, uint SampledImages)? _stageLimits;

    /// <summary>
    /// How many samplers and how many sampled images one shader stage may read, as the device
    /// allows, where a combined image sampler counts as one of each. Metal allows a stage sixteen
    /// samplers, which no setting lifts, so a pass of many textures reads them through a few
    /// samplers bound apart.
    /// </summary>
    internal (uint Samplers, uint SampledImages) StageLimits
    {
        get
        {
            if (_stageLimits is { } known) return known;
            _instanceApi.vkGetPhysicalDeviceProperties(_physicalDevice, out var properties);
            var limits = (properties.limits.maxPerStageDescriptorSamplers, properties.limits.maxPerStageDescriptorSampledImages);
            _stageLimits = limits;
            return limits;
        }
    }

    /// <summary>
    /// Refuses a layout whose stages read more samplers or sampled images than the device allows a
    /// stage, saying how many and of what, where the driver would draw nothing and only the
    /// validation layer would say why.
    /// </summary>
    /// <exception cref="InvalidOperationException">A stage reads more than the device allows.</exception>
    internal void CheckStageLimits(IEnumerable<DescriptorSetLayoutBinding> bindings, string what)
    {
        var all = bindings.ToArray();
        foreach (var stage in new[] { ShaderStageFlags.Vertex, ShaderStageFlags.Fragment })
            CheckStage(stage.ToString().ToLowerInvariant(), all.Where(b => b.Stages.HasFlag(stage)), what);
    }

    /// <summary>The same for a compute shader's one stage, which reads every texture it binds.</summary>
    /// <exception cref="InvalidOperationException">The stage reads more than the device allows.</exception>
    internal void CheckComputeLimits(IEnumerable<DescriptorType> textures, string what) =>
        CheckStage("compute", textures.Select((type, i) => new DescriptorSetLayoutBinding((uint)i, type, default)), what);

    private void CheckStage(string stage, IEnumerable<DescriptorSetLayoutBinding> read, string what)
    {
        var (samplers, images) = StageLimits;
        if (OverLimits(stage, read, samplers, images, what) is { } refusal) throw new InvalidOperationException(refusal);
    }

    /// <summary>
    /// What a stage reading <paramref name="read"/> is refused with where it reads more samplers or
    /// sampled images than <paramref name="samplers"/> and <paramref name="images"/>, a combined
    /// image sampler counting as one of each, or null where it fits.
    /// </summary>
    internal static string? OverLimits(string stage, IEnumerable<DescriptorSetLayoutBinding> read, uint samplers, uint images, string what)
    {
        var bindings = read.ToArray();
        var samplerCount = bindings.Where(b => b.Type is DescriptorType.Sampler or DescriptorType.CombinedImageSampler).Sum(b => (long)b.Count);
        var imageCount = bindings.Where(b => b.Type is DescriptorType.SampledImage or DescriptorType.CombinedImageSampler).Sum(b => (long)b.Count);
        if (samplerCount > samplers)
            return $"The {stage} stage of {what} reads {samplerCount} samplers, and this GPU allows a stage {samplers}. "
                   + "Read textures through fewer samplers, declaring a Texture2D apart from a SamplerState that several share.";
        if (imageCount > images)
            return $"The {stage} stage of {what} reads {imageCount} textures, and this GPU allows a stage {images}.";
        return null;
    }
}
