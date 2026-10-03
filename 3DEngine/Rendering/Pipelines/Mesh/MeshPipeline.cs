using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>Creates and owns a mesh graphics pipeline with vertex input and push constants.</summary>
/// <remarks>
/// The pipeline accepts a single vertex binding (vec3 position at location 0) and uses
/// push constants for the per-object model matrix and albedo color.  The camera UBO is
/// bound at set 0 binding 0 via the default descriptor set layout.
/// </remarks>
/// <seealso cref="MeshPushConstants"/>
/// <seealso cref="MainPassNode"/>
public sealed class MeshPipeline : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Renderer.Mesh");

    private readonly IShader _vertexShader;
    private readonly IShader _fragmentShader;
    private readonly IPipeline _pipeline;

    /// <summary>The compiled graphics pipeline handle.</summary>
    public IPipeline Pipeline => _pipeline;

    /// <summary>Creates a new mesh pipeline from pre-compiled SPIR-V bytecode.</summary>
    /// <param name="graphics">The graphics device to create GPU resources on.</param>
    /// <param name="renderPass">The render pass the pipeline must be compatible with.</param>
    /// <param name="vertexSpirv">SPIR-V bytecode for the mesh vertex shader.</param>
    /// <param name="fragmentSpirv">SPIR-V bytecode for the mesh fragment shader.</param>
    /// <param name="pipelineCache">Optional pipeline cache for deduplication.</param>
    public MeshPipeline(IGraphicsDevice graphics, IRenderPass renderPass,
        ReadOnlyMemory<byte> vertexSpirv, ReadOnlyMemory<byte> fragmentSpirv,
        PipelineCache? pipelineCache = null)
    {
        Logger.Debug($"Compiling mesh shaders (vertex={vertexSpirv.Length} bytes, fragment={fragmentSpirv.Length} bytes)...");
        var vsDesc = new ShaderDesc(ShaderStage.Vertex, vertexSpirv, "main");
        var fsDesc = new ShaderDesc(ShaderStage.Fragment, fragmentSpirv, "main");

        _vertexShader = graphics.CreateShader(vsDesc);
        _fragmentShader = graphics.CreateShader(fsDesc);

        // Vertex input: binding 0 = vec3 position (12 bytes stride)
        var vertexBindings = new[]
        {
            new VertexInputBindingDesc(Binding: 0, Stride: (uint)Marshal.SizeOf<Vector3>())
        };
        var vertexAttributes = new[]
        {
            new VertexInputAttributeDesc(Location: 0, Binding: 0, Format: VertexFormat.Float3, Offset: 0)
        };

        // Push constants: mat4 Model (64) + vec4 Albedo (16) = 80 bytes
        var pushConstantRanges = new[]
        {
            new PushConstantRange(ShaderStageFlags.All, 0, (uint)Marshal.SizeOf<MeshPushConstants>())
        };

        var pipelineDesc = new GraphicsPipelineDesc(
            renderPass,
            _vertexShader,
            _fragmentShader,
            BlendEnabled: false,
            CullBackFace: true,
            VertexBindings: vertexBindings,
            VertexAttributes: vertexAttributes,
            PushConstantRanges: pushConstantRanges,
            DepthTestEnabled: true,
            DepthWriteEnabled: true);

        if (pipelineCache is not null)
        {
            Logger.Debug("Looking up mesh pipeline in cache...");
            _pipeline = pipelineCache.GetOrCreate(pipelineDesc);
        }
        else
        {
            Logger.Debug("Creating mesh graphics pipeline (no cache)...");
            _pipeline = graphics.CreateGraphicsPipeline(pipelineDesc);
        }
        Logger.Debug("Mesh pipeline ready.");
    }

    /// <summary>Disposes the vertex and fragment shader modules.</summary>
    public void Dispose()
    {
        Logger.Debug("Disposing mesh pipeline shaders...");
        _fragmentShader.Dispose();
        _vertexShader.Dispose();
    }
}
