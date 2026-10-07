using System.Text;
using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Wraps a Vulkan shader module with its creation descriptor.</summary>
    /// <seealso cref="IShader"/>
    private sealed class VulkanShader : IShader
    {
        private readonly GraphicsDevice _device;

        /// <inheritdoc />
        public ShaderDesc Description { get; }

        /// <summary>The underlying Vulkan shader module handle.</summary>
        internal VkShaderModule Module;

        /// <summary>How many color attachments a fragment stage writes, one past its highest output location.</summary>
        internal int Outputs { get; }

        /// <summary>Creates a new Vulkan shader wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="desc">The shader creation descriptor.</param>
        /// <param name="module">The compiled Vulkan shader module.</param>
        public VulkanShader(GraphicsDevice device, ShaderDesc desc, VkShaderModule module)
        {
            _device = device;
            Description = desc;
            Module = module;
            Outputs = desc.Stage == ShaderStage.Fragment ? ShaderProgram.OutputLocations(desc.Bytecode.Span) : 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Module.Handle != 0)
            {
                _device._deviceApi.vkDestroyShaderModule(Module);
                Module = default;
            }
        }
    }

    /// <summary>Wraps a Vulkan graphics pipeline and its pipeline layout.</summary>
    /// <seealso cref="IPipeline"/>
    private sealed class VulkanGraphicsPipeline : IPipeline
    {
        private readonly GraphicsDevice _device;

        /// <summary>The underlying Vulkan pipeline handle.</summary>
        internal VkPipeline Pipeline;

        /// <summary>The pipeline layout describing descriptor set and push constant bindings.</summary>
        internal VkPipelineLayout Layout;

        /// <summary>Creates a new Vulkan graphics pipeline wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="pipeline">The Vulkan pipeline handle.</param>
        /// <param name="layout">The Vulkan pipeline layout handle.</param>
        public VulkanGraphicsPipeline(GraphicsDevice device, VkPipeline pipeline, VkPipelineLayout layout)
        {
            _device = device;
            Pipeline = pipeline;
            Layout = layout;
            Interlocked.Increment(ref device._livePipelines);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Pipeline.Handle != 0)
            {
                _device._deviceApi.vkDestroyPipeline(Pipeline);
                Pipeline = default;
                Interlocked.Decrement(ref _device._livePipelines);
            }
            if (Layout.Handle != 0)
            {
                _device._deviceApi.vkDestroyPipelineLayout(Layout);
                Layout = default;
            }
        }
    }

    /// <inheritdoc />
    public IShader CreateShader(ShaderDesc desc)
    {
        fixed (byte* codePtr = desc.Bytecode.Span)
        {
            VkShaderModuleCreateInfo info = new()
            {
                codeSize = (nuint)desc.Bytecode.Length,
                pCode = (uint*)codePtr
            };

            _deviceApi.vkCreateShaderModule(&info, null, out VkShaderModule module).CheckResult();
            return new VulkanShader(this, desc, module);
        }
    }

    /// <inheritdoc />
    public IPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        if (desc.RenderPass is not VulkanRenderPass pass)
            throw new ArgumentException("RenderPass must originate from this GraphicsDevice.", nameof(desc));

        var vs = (VulkanShader)desc.VertexShader;
        var fs = (VulkanShader?)desc.FragmentShader;
        bool depthOnly = pass.DepthOnly;

        VkUtf8ReadOnlyString entryName = Encoding.UTF8.GetBytes(desc.VertexShader.Description.EntryPoint);

        VkPipelineShaderStageCreateInfo* stages = stackalloc VkPipelineShaderStageCreateInfo[2];
        stages[0] = new VkPipelineShaderStageCreateInfo
        {
            stage = VkShaderStageFlags.Vertex,
            module = vs.Module,
            pName = entryName
        };
        // A pipeline with no fragment stage writes depth only.
        if (fs is not null)
            stages[1] = new VkPipelineShaderStageCreateInfo
            {
                stage = VkShaderStageFlags.Fragment,
                module = fs.Module,
                pName = entryName
            };

        // The vertex input, from the bindings and attributes given when there are any.
        var vertexBindingCount = desc.VertexBindings?.Length ?? 0;
        var vertexAttributeCount = desc.VertexAttributes?.Length ?? 0;

        VkVertexInputBindingDescription* vkBindings = stackalloc VkVertexInputBindingDescription[Math.Max(vertexBindingCount, 1)];
        VkVertexInputAttributeDescription* vkAttributes = stackalloc VkVertexInputAttributeDescription[Math.Max(vertexAttributeCount, 1)];

        for (int i = 0; i < vertexBindingCount; i++)
        {
            var b = desc.VertexBindings![i];
            vkBindings[i] = new VkVertexInputBindingDescription
            {
                binding = b.Binding,
                stride = b.Stride,
                inputRate = b.PerInstance ? VkVertexInputRate.Instance : VkVertexInputRate.Vertex
            };
        }

        for (int i = 0; i < vertexAttributeCount; i++)
        {
            var a = desc.VertexAttributes![i];
            vkAttributes[i] = new VkVertexInputAttributeDescription
            {
                location = a.Location,
                binding = a.Binding,
                format = ToVkFormat(a.Format),
                offset = a.Offset
            };
        }

        VkPipelineVertexInputStateCreateInfo vertexInput = new()
        {
            vertexBindingDescriptionCount = (uint)vertexBindingCount,
            pVertexBindingDescriptions = vertexBindingCount > 0 ? vkBindings : null,
            vertexAttributeDescriptionCount = (uint)vertexAttributeCount,
            pVertexAttributeDescriptions = vertexAttributeCount > 0 ? vkAttributes : null
        };

        VkPipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            topology = desc.Topology == PrimitiveTopology.LineList
                ? VkPrimitiveTopology.LineList
                : VkPrimitiveTopology.TriangleList
        };

        VkViewport viewport = new(0, 0, _swapchainExtent.width, _swapchainExtent.height, 0, 1);
        VkRect2D scissor = new(new VkOffset2D(0, 0), _swapchainExtent);

        VkPipelineViewportStateCreateInfo viewportState = new()
        {
            viewportCount = 1,
            pViewports = &viewport,
            scissorCount = 1,
            pScissors = &scissor
        };

        // Lines of one sample a pixel by the diamond rule, as OpenGL draws raylib's, where the device
        // has it. With several samples they stay the driver's, whose edges the samples smooth, where
        // the rule would fill every sample of each pixel it takes.
        VkPipelineRasterizationLineStateCreateInfo lineState = new() { lineRasterizationMode = VkLineRasterizationMode.Bresenham };
        VkPipelineRasterizationStateCreateInfo rasterizer = new()
        {
            pNext = desc.Topology == PrimitiveTopology.LineList && CanDrawBresenhamLines && pass.Samples == VkSampleCountFlags.Count1 ? &lineState : null,
            polygonMode = desc.Points && CanDrawPoints ? VkPolygonMode.Point : VkPolygonMode.Fill,
            cullMode = desc.Cull switch { CullMode.Back => VkCullModeFlags.Back, CullMode.Front => VkCullModeFlags.Front, _ => VkCullModeFlags.None },
            frontFace = VkFrontFace.CounterClockwise,
            lineWidth = 1.0f
        };

        // At the samples of the pass it draws in, which a multisampled window's and targets' are.
        VkPipelineMultisampleStateCreateInfo multisample = new()
        {
            rasterizationSamples = pass.Samples
        };

        // The color's factors by the blend mode, raylib's, and alpha laid over in every mode.
        var (srcColor, dstColor, colorOp) = (desc.PremultipliedAlpha ? BlendMode.AlphaPremultiply : desc.Blend) switch
        {
            BlendMode.Additive => (VkBlendFactor.SrcAlpha, VkBlendFactor.One, VkBlendOp.Add),
            BlendMode.Multiplied => (VkBlendFactor.DstColor, VkBlendFactor.OneMinusSrcAlpha, VkBlendOp.Add),
            BlendMode.AddColors => (VkBlendFactor.One, VkBlendFactor.One, VkBlendOp.Add),
            BlendMode.SubtractColors => (VkBlendFactor.One, VkBlendFactor.One, VkBlendOp.Subtract),
            BlendMode.AlphaPremultiply => (VkBlendFactor.One, VkBlendFactor.OneMinusSrcAlpha, VkBlendOp.Add),
            _ => (VkBlendFactor.SrcAlpha, VkBlendFactor.OneMinusSrcAlpha, VkBlendOp.Add),
        };
        var (srcAlpha, dstAlpha, alphaOp) = (VkBlendFactor.One, VkBlendFactor.OneMinusSrcAlpha, VkBlendOp.Add);
        if (desc.Blend is BlendMode.Custom or BlendMode.CustomSeparate)
        {
            var f = desc.Factors;
            (srcColor, dstColor, colorOp) = (ToVkBlendFactor(f.SrcColor), ToVkBlendFactor(f.DstColor), ToVkBlendOp(f.ColorEquation));
            (srcAlpha, dstAlpha, alphaOp) = (ToVkBlendFactor(f.SrcAlpha), ToVkBlendFactor(f.DstAlpha), ToVkBlendOp(f.AlphaEquation));
        }
        // Each color attachment blended alike, and one past those the fragment stage writes left
        // as it is, so a shader of one output draws into a target of several without filling the
        // rest with what it never wrote. A device without independentBlend takes one state for
        // every attachment, and there the rest hold what the driver writes for an output never given.
        var colorCount = pass.ColorCount;
        var colorBlendAttachments = stackalloc VkPipelineColorBlendAttachmentState[Math.Max(1, colorCount)];
        for (int i = 0; i < colorCount; i++)
        {
            var written = i == 0 || fs is null || i < fs.Outputs || !CanBlendEachAttachment;
            colorBlendAttachments[i] = new VkPipelineColorBlendAttachmentState
            {
                colorWriteMask = written ? VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A : 0,
                blendEnable = desc.BlendEnabled && written,
                srcColorBlendFactor = srcColor,
                dstColorBlendFactor = dstColor,
                colorBlendOp = colorOp,
                srcAlphaBlendFactor = srcAlpha,
                dstAlphaBlendFactor = dstAlpha,
                alphaBlendOp = alphaOp
            };
        }

        VkPipelineColorBlendStateCreateInfo colorBlend = new()
        {
            attachmentCount = (uint)colorCount,
            pAttachments = colorCount > 0 ? colorBlendAttachments : null
        };

        // Depth-stencil state
        VkPipelineDepthStencilStateCreateInfo depthStencil = new()
        {
            depthTestEnable = desc.DepthTestEnabled,
            depthWriteEnable = desc.DepthWriteEnabled,
            depthCompareOp = desc.DepthTestEnabled ? ToVkCompareOp(desc.DepthCompareOp) : VkCompareOp.Always,
            depthBoundsTestEnable = false,
            stencilTestEnable = false
        };

        VkDynamicState* dynamics = stackalloc VkDynamicState[2];
        dynamics[0] = VkDynamicState.Viewport;
        dynamics[1] = VkDynamicState.Scissor;

        VkPipelineDynamicStateCreateInfo dynamicState = new()
        {
            dynamicStateCount = 2,
            pDynamicStates = dynamics
        };

        int setLayoutCount;
        int maxSetLayouts = desc.DescriptorSetLayouts is { Length: > 0 } ? desc.DescriptorSetLayouts.Length : 1;
        VkDescriptorSetLayout* setLayouts = stackalloc VkDescriptorSetLayout[maxSetLayouts];
        if (desc.DescriptorSetLayouts is { Length: > 0 } customLayouts)
        {
            CheckStageLimits(customLayouts.OfType<VulkanDescriptorSetLayout>().SelectMany(layout => layout.Bindings), "a pipeline");
            setLayoutCount = customLayouts.Length;
            for (int i = 0; i < setLayoutCount; i++)
            {
                if (customLayouts[i] is VulkanDescriptorSetLayout vkDsl)
                    setLayouts[i] = vkDsl.Handle;
                else
                    throw new ArgumentException("Descriptor set layout was not created by this device.", nameof(desc));
            }
        }
        else
        {
            setLayoutCount = 1;
            setLayouts[0] = _cameraSetLayout;
        }

        // Push constant ranges
        var pcCount = desc.PushConstantRanges?.Length ?? 0;
        VkPushConstantRange* vkPcRanges = stackalloc VkPushConstantRange[Math.Max(pcCount, 1)];
        for (int i = 0; i < pcCount; i++)
        {
            var pc = desc.PushConstantRanges![i];
            vkPcRanges[i] = new VkPushConstantRange
            {
                stageFlags = ToVkShaderStageFlags(pc.StageFlags),
                offset = pc.Offset,
                size = pc.Size
            };
        }

        VkPipelineLayoutCreateInfo layoutInfo = new()
        {
            setLayoutCount = (uint)setLayoutCount,
            pSetLayouts = setLayouts,
            pushConstantRangeCount = (uint)pcCount,
            pPushConstantRanges = pcCount > 0 ? vkPcRanges : null
        };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out VkPipelineLayout layout).CheckResult();

        // Drawn by dynamic rendering, so the pipeline names the formats it writes rather than a
        // render pass.
        var colorFormats = stackalloc VkFormat[Math.Max(1, colorCount)];
        for (int i = 0; i < colorCount; i++) colorFormats[i] = pass.ColorFormatAt(i);
        var rendering = new VkPipelineRenderingCreateInfo
        {
            colorAttachmentCount = (uint)colorCount,
            pColorAttachmentFormats = colorCount > 0 ? colorFormats : null,
            depthAttachmentFormat = pass.DepthFormat,
        };
        VkGraphicsPipelineCreateInfo pipelineInfo = new()
        {
            pNext = &rendering,
            stageCount = fs is null ? 1u : 2u,
            pStages = stages,
            pVertexInputState = &vertexInput,
            pInputAssemblyState = &inputAssembly,
            pViewportState = &viewportState,
            pRasterizationState = &rasterizer,
            pMultisampleState = &multisample,
            pDepthStencilState = &depthStencil,
            pColorBlendState = &colorBlend,
            pDynamicState = &dynamicState,
            layout = layout
        };

        VkPipeline pipeline;
        _deviceApi.vkCreateGraphicsPipelines(default, 1, &pipelineInfo, null, &pipeline).CheckResult();
        return new VulkanGraphicsPipeline(this, pipeline, layout);
    }

    /// <inheritdoc />
    public void BindGraphicsPipeline(ICommandBuffer commandBuffer, IPipeline pipeline)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (pipeline is not VulkanGraphicsPipeline vkPipeline)
            throw new ArgumentException("Pipeline was not created by this device.", nameof(pipeline));

        _deviceApi.vkCmdBindPipeline(vkCmd.Handle, VkPipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    /// <inheritdoc />
    public void BindDescriptorSet(ICommandBuffer commandBuffer, IPipeline pipeline, IDescriptorSet descriptorSet) =>
        BindDescriptorSet(commandBuffer, pipeline, descriptorSet, 0);

    /// <inheritdoc />
    public void BindDescriptorSet(ICommandBuffer commandBuffer, IPipeline pipeline, IDescriptorSet descriptorSet, uint index) =>
        BindDescriptorSet(commandBuffer, pipeline, descriptorSet, index, []);

    /// <inheritdoc />
    public void BindDescriptorSet(ICommandBuffer commandBuffer, IPipeline pipeline, IDescriptorSet descriptorSet, uint index, ReadOnlySpan<uint> dynamicOffsets)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (pipeline is not VulkanGraphicsPipeline vkPipeline)
            throw new ArgumentException("Pipeline was not created by this device.", nameof(pipeline));
        if (descriptorSet is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("Descriptor set was not created by this device.", nameof(descriptorSet));

        VkDescriptorSet* sets = stackalloc VkDescriptorSet[1];
        sets[0] = vkSet.Handle;

        fixed (uint* offsets = dynamicOffsets)
            _deviceApi.vkCmdBindDescriptorSets(vkCmd.Handle, VkPipelineBindPoint.Graphics, vkPipeline.Layout, index, 1, sets,
                (uint)dynamicOffsets.Length, offsets);
    }

    /// <inheritdoc />
    public void Draw(ICommandBuffer commandBuffer, uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));

        _deviceApi.vkCmdDraw(vkCmd.Handle, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    IShader IGraphicsDevice.CreateShader(ShaderDesc desc) => CreateShader(desc);
    IPipeline IGraphicsDevice.CreateGraphicsPipeline(GraphicsPipelineDesc desc) => CreateGraphicsPipeline(desc);

    // ---- Format / flag helpers ----

    /// <summary>Maps an engine <see cref="VertexFormat"/> to the Vulkan <c>VkFormat</c> equivalent.</summary>
    private static VkFormat ToVkFormat(VertexFormat format) => format switch
    {
        VertexFormat.Float2 => VkFormat.R32G32Sfloat,
        VertexFormat.Float3 => VkFormat.R32G32B32Sfloat,
        VertexFormat.Float4 => VkFormat.R32G32B32A32Sfloat,
        VertexFormat.UNormR8G8B8A8 => VkFormat.R8G8B8A8Unorm,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    /// <summary>Converts engine <see cref="ShaderStageFlags"/> to Vulkan <c>VkShaderStageFlags</c>.</summary>
    private static VkShaderStageFlags ToVkShaderStageFlags(ShaderStageFlags flags)
    {
        VkShaderStageFlags result = 0;
        if (flags.HasFlag(ShaderStageFlags.Vertex)) result |= VkShaderStageFlags.Vertex;
        if (flags.HasFlag(ShaderStageFlags.Fragment)) result |= VkShaderStageFlags.Fragment;
        return result;
    }

    /// <summary>Maps an engine <see cref="CompareOp"/> to the Vulkan <c>VkCompareOp</c> equivalent.</summary>
    private static VkCompareOp ToVkCompareOp(CompareOp op) => op switch
    {
        CompareOp.Never => VkCompareOp.Never,
        CompareOp.Less => VkCompareOp.Less,
        CompareOp.Equal => VkCompareOp.Equal,
        CompareOp.LessOrEqual => VkCompareOp.LessOrEqual,
        CompareOp.Greater => VkCompareOp.Greater,
        CompareOp.NotEqual => VkCompareOp.NotEqual,
        CompareOp.GreaterOrEqual => VkCompareOp.GreaterOrEqual,
        CompareOp.Always => VkCompareOp.Always,
        _ => VkCompareOp.Less
    };

    // rlgl's blend factors and equations, OpenGL's, as Vulkan names them.
    private static VkBlendFactor ToVkBlendFactor(RlBlendFactor factor) => factor switch
    {
        RlBlendFactor.Zero => VkBlendFactor.Zero,
        RlBlendFactor.SrcColor => VkBlendFactor.SrcColor,
        RlBlendFactor.OneMinusSrcColor => VkBlendFactor.OneMinusSrcColor,
        RlBlendFactor.SrcAlpha => VkBlendFactor.SrcAlpha,
        RlBlendFactor.OneMinusSrcAlpha => VkBlendFactor.OneMinusSrcAlpha,
        RlBlendFactor.DstAlpha => VkBlendFactor.DstAlpha,
        RlBlendFactor.OneMinusDstAlpha => VkBlendFactor.OneMinusDstAlpha,
        RlBlendFactor.DstColor => VkBlendFactor.DstColor,
        RlBlendFactor.OneMinusDstColor => VkBlendFactor.OneMinusDstColor,
        RlBlendFactor.SrcAlphaSaturate => VkBlendFactor.SrcAlphaSaturate,
        _ => VkBlendFactor.One,
    };

    private static VkBlendOp ToVkBlendOp(RlBlendEquation equation) => equation switch
    {
        RlBlendEquation.Min => VkBlendOp.Min,
        RlBlendEquation.Max => VkBlendOp.Max,
        RlBlendEquation.FuncSubtract => VkBlendOp.Subtract,
        RlBlendEquation.FuncReverseSubtract => VkBlendOp.ReverseSubtract,
        _ => VkBlendOp.Add,
    };

    // ---- Extended draw commands ----

    /// <inheritdoc />
    public void DrawIndexed(ICommandBuffer commandBuffer, uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        _deviceApi.vkCmdDrawIndexed(vkCmd.Handle, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    /// <inheritdoc />
    public void BindVertexBuffers(ICommandBuffer commandBuffer, uint firstBinding, IBuffer[] buffers, ulong[] offsets)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));

        VkBuffer* vkBuffers = stackalloc VkBuffer[buffers.Length];
        ulong* vkOffsets = stackalloc ulong[offsets.Length];
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] is not VulkanBuffer vb)
                throw new ArgumentException("Buffer was not created by this device.", nameof(buffers));
            vkBuffers[i] = vb.Buffer;
            vkOffsets[i] = offsets[i];
        }

        _deviceApi.vkCmdBindVertexBuffers(vkCmd.Handle, firstBinding, (uint)buffers.Length, vkBuffers, vkOffsets);
    }

    /// <inheritdoc />
    public void BindIndexBuffer(ICommandBuffer commandBuffer, IBuffer buffer, ulong offset, IndexType indexType)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("Buffer was not created by this device.", nameof(buffer));

        var vkIndexType = indexType == IndexType.UInt16 ? VkIndexType.Uint16 : VkIndexType.Uint32;
        _deviceApi.vkCmdBindIndexBuffer(vkCmd.Handle, vkBuffer.Buffer, offset, vkIndexType);
    }

    /// <inheritdoc />
    public void SetViewport(ICommandBuffer commandBuffer, float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));

        VkViewport viewport = new(x, y, width, height, minDepth, maxDepth);
        _deviceApi.vkCmdSetViewport(vkCmd.Handle, 0, 1, &viewport);
    }

    /// <inheritdoc />
    public void SetScissor(ICommandBuffer commandBuffer, int x, int y, uint width, uint height)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));

        VkRect2D scissor = new(new VkOffset2D(x, y), new VkExtent2D(width, height));
        _deviceApi.vkCmdSetScissor(vkCmd.Handle, 0, 1, &scissor);
    }

    /// <inheritdoc />
    public void PushConstants(ICommandBuffer commandBuffer, IPipeline pipeline, ShaderStageFlags stageFlags, uint offset, ReadOnlySpan<byte> data)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (pipeline is not VulkanGraphicsPipeline vkPipeline)
            throw new ArgumentException("Pipeline was not created by this device.", nameof(pipeline));

        fixed (byte* pData = data)
        {
            _deviceApi.vkCmdPushConstants(vkCmd.Handle, vkPipeline.Layout, ToVkShaderStageFlags(stageFlags), offset, (uint)data.Length, pData);
        }
    }
}
