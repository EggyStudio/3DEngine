namespace Engine;

/// <summary>Depth comparison function used when depth testing is enabled.</summary>
public enum CompareOp
{
    /// <summary>Never passes.</summary>
    Never,
    /// <summary>Passes if the incoming depth is less than the stored depth.</summary>
    Less,
    /// <summary>Passes if the incoming depth is equal to the stored depth.</summary>
    Equal,
    /// <summary>Passes if the incoming depth is less than or equal to the stored depth.</summary>
    LessOrEqual,
    /// <summary>Passes if the incoming depth is greater than the stored depth.</summary>
    Greater,
    /// <summary>Passes if the incoming depth is not equal to the stored depth.</summary>
    NotEqual,
    /// <summary>Passes if the incoming depth is greater than or equal to the stored depth.</summary>
    GreaterOrEqual,
    /// <summary>Always passes.</summary>
    Always
}

/// <summary>Descriptor for creating a graphics pipeline.</summary>
/// <param name="RenderPass">The render pass this pipeline will be used with.</param>
/// <param name="VertexShader">The compiled vertex shader.</param>
/// <param name="FragmentShader">The compiled fragment shader.</param>
/// <param name="BlendEnabled">Whether alpha blending is enabled.</param>
/// <param name="CullBackFace">Whether back-face culling is enabled.</param>
/// <param name="VertexBindings">Optional vertex buffer binding descriptions.</param>
/// <param name="VertexAttributes">Optional vertex attribute descriptions.</param>
/// <param name="PushConstantRanges">Optional push constant range descriptions.</param>
/// <param name="DescriptorSetLayouts">Optional custom descriptor set layouts. When provided, these replace the global camera layout in the pipeline layout.</param>
/// <param name="PremultipliedAlpha">When <c>true</c> and <paramref name="BlendEnabled"/> is <c>true</c>,
/// uses <c>srcColor=One</c> instead of <c>srcColor=SrcAlpha</c> for pre-multiplied alpha compositing.</param>
/// <param name="DepthTestEnabled">Whether depth testing is enabled. Defaults to <c>false</c> for backward compatibility with overlay pipelines.</param>
/// <param name="DepthWriteEnabled">Whether depth writes are enabled. Only meaningful when <paramref name="DepthTestEnabled"/> is <c>true</c>.</param>
/// <param name="DepthCompareOp">The comparison function for depth testing. Defaults to <see cref="CompareOp.Less"/>.</param>
public readonly record struct GraphicsPipelineDesc(
    IRenderPass RenderPass,
    IShader VertexShader,
    IShader FragmentShader,
    bool BlendEnabled = false,
    bool CullBackFace = true,
    VertexInputBindingDesc[]? VertexBindings = null,
    VertexInputAttributeDesc[]? VertexAttributes = null,
    PushConstantRange[]? PushConstantRanges = null,
    IDescriptorSetLayout[]? DescriptorSetLayouts = null,
    bool PremultipliedAlpha = false,
    bool DepthTestEnabled = false,
    bool DepthWriteEnabled = false,
    CompareOp DepthCompareOp = CompareOp.Less);
