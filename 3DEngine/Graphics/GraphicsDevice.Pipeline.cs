using System.Text;
using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
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

        /// <summary>Creates a new Vulkan shader wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="desc">The shader creation descriptor.</param>
        /// <param name="module">The compiled Vulkan shader module.</param>
        public VulkanShader(GraphicsDevice device, ShaderDesc desc, VkShaderModule module)
        {
            _device = device;
            Description = desc;
            Module = module;
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
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Pipeline.Handle != 0)
            {
                _device._deviceApi.vkDestroyPipeline(Pipeline);
                Pipeline = default;
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
        VkRenderPass rpHandle;
        if (desc.RenderPass is VulkanRenderPass rpWrapper)
            rpHandle = rpWrapper.Handle;
        else if (desc.RenderPass is VulkanOffscreenRenderPass offscreenRp)
            rpHandle = offscreenRp.Handle;
        else
            throw new ArgumentException("RenderPass must originate from this GraphicsDevice.", nameof(desc));


        var vs = (VulkanShader)desc.VertexShader;
        var fs = (VulkanShader)desc.FragmentShader;

        VkUtf8ReadOnlyString entryName = Encoding.UTF8.GetBytes(desc.VertexShader.Description.EntryPoint);

        VkPipelineShaderStageCreateInfo* stages = stackalloc VkPipelineShaderStageCreateInfo[2];
        stages[0] = new VkPipelineShaderStageCreateInfo
        {
            stage = VkShaderStageFlags.Vertex,
            module = vs.Module,
            pName = entryName
        };
        stages[1] = new VkPipelineShaderStageCreateInfo
        {
            stage = VkShaderStageFlags.Fragment,
            module = fs.Module,
            pName = entryName
        };

        // Vertex input state - use custom bindings/attributes if provided
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
                inputRate = VkVertexInputRate.Vertex
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
            topology = VkPrimitiveTopology.TriangleList
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

        VkPipelineRasterizationStateCreateInfo rasterizer = new()
        {
            polygonMode = VkPolygonMode.Fill,
            cullMode = desc.CullBackFace ? VkCullModeFlags.Back : VkCullModeFlags.None,
            frontFace = VkFrontFace.CounterClockwise,
            lineWidth = 1.0f
        };

        VkPipelineMultisampleStateCreateInfo multisample = new()
        {
            rasterizationSamples = VkSampleCountFlags.Count1
        };

        VkPipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A,
            blendEnable = desc.BlendEnabled,
            srcColorBlendFactor = desc.PremultipliedAlpha ? VkBlendFactor.One : VkBlendFactor.SrcAlpha,
            dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha,
            colorBlendOp = VkBlendOp.Add,
            srcAlphaBlendFactor = VkBlendFactor.One,
            dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha,
            alphaBlendOp = VkBlendOp.Add
        };

        VkPipelineColorBlendStateCreateInfo colorBlend = new()
        {
            attachmentCount = 1,
            pAttachments = &colorBlendAttachment
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

        VkGraphicsPipelineCreateInfo pipelineInfo = new()
        {
            stageCount = 2,
            pStages = stages,
            pVertexInputState = &vertexInput,
            pInputAssemblyState = &inputAssembly,
            pViewportState = &viewportState,
            pRasterizationState = &rasterizer,
            pMultisampleState = &multisample,
            pDepthStencilState = &depthStencil,
            pColorBlendState = &colorBlend,
            pDynamicState = &dynamicState,
            layout = layout,
            renderPass = rpHandle,
            subpass = 0
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
    public void BindDescriptorSet(ICommandBuffer commandBuffer, IPipeline pipeline, IDescriptorSet descriptorSet)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (pipeline is not VulkanGraphicsPipeline vkPipeline)
            throw new ArgumentException("Pipeline was not created by this device.", nameof(pipeline));
        if (descriptorSet is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("Descriptor set was not created by this device.", nameof(descriptorSet));

        VkDescriptorSet* sets = stackalloc VkDescriptorSet[1];
        sets[0] = vkSet.Handle;

        _deviceApi.vkCmdBindDescriptorSets(vkCmd.Handle, VkPipelineBindPoint.Graphics, vkPipeline.Layout, 0, 1, sets, 0, null);
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
