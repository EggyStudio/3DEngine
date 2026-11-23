using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    // The sharp scaling's pass (scaled.slang), drawn into the window's image in place of the copy
    // where the frames are drawn at a size of their own and the window is larger. Its pipeline is
    // made the first time it draws, for the window's format, and its sets, one for each frame
    // image, and the views of the window's images with the frame images, made again with them.
    private byte[]? _sharpVertex, _sharpFragment;
    private VkDescriptorSetLayout _sharpSetLayout;
    private VkPipelineLayout _sharpLayout;
    private VkPipeline _sharpPipeline;
    private VkFormat _sharpFormat;
    private VkDescriptorPool _sharpPool;
    private VkDescriptorSet[] _sharpSets = [];
    private VkImageView[] _shownViews = [];

    private static readonly VkDescriptorType[] SharpDescriptors = [VkDescriptorType.SampledImage];

    /// <summary>Takes <c>scaled.slang</c>'s stages, which the sharp scaling draws with.</summary>
    public void InitializeSharpScaling(ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> fragment)
    {
        if (!IsInitialized || _sharpVertex is not null) return;
        (_sharpVertex, _sharpFragment) = (vertex.ToArray(), fragment.ToArray());
        var binding = new VkDescriptorSetLayoutBinding
        {
            binding = 0, descriptorType = VkDescriptorType.SampledImage, descriptorCount = 1, stageFlags = VkShaderStageFlags.Fragment,
        };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 1, pBindings = &binding };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out _sharpSetLayout).CheckResult();
        // The frame's corner on the window and the ratio, as scaled.slang's SharpPush.
        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Fragment, offset = 0, size = 16 };
        var setLayout = _sharpSetLayout;
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out _sharpLayout).CheckResult();
    }

    // Whether the sharp scaling can draw this frame, its pipeline made for the window's format and
    // its sets and views for the frame images, made here where they are not yet.
    private bool SharpReady()
    {
        if (_sharpVertex is null || _sharpFragment is null) return false;
        if (_sharpPipeline.Handle == 0 || _sharpFormat != _swapchainFormat)
        {
            DestroySharpPipeline();
            _sharpPipeline = MakeSharpPipeline(_swapchainFormat);
            _sharpFormat = _swapchainFormat;
        }
        if (_sharpSets.Length == _swapchainImages.Length) return true;
        _sharpPool = MakeDescriptorPool([(SharpDescriptors, _swapchainImages.Length)]);
        _sharpSets = new VkDescriptorSet[_swapchainImages.Length];
        _shownViews = new VkImageView[_shownImages.Length];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            var layout = _sharpSetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = _sharpPool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            _sharpSets[i] = set;
            var image = new VkDescriptorImageInfo { imageView = _swapchainImageViews[i], imageLayout = VkImageLayout.ShaderReadOnlyOptimal };
            var write = new VkWriteDescriptorSet
            {
                dstSet = set, dstBinding = 0, descriptorCount = 1, descriptorType = VkDescriptorType.SampledImage, pImageInfo = &image,
            };
            _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }
        for (int i = 0; i < _shownImages.Length; i++)
        {
            var viewInfo = new VkImageViewCreateInfo
            {
                image = _shownImages[i],
                viewType = VkImageViewType.Image2D,
                format = _swapchainFormat,
                subresourceRange = ColorLevels(0, 1),
            };
            _deviceApi.vkCreateImageView(&viewInfo, null, out _shownViews[i]).CheckResult();
        }
        return true;
    }

    private VkPipeline MakeSharpPipeline(VkFormat format)
    {
        var modules = stackalloc VkShaderModule[2];
        var stages = stackalloc VkPipelineShaderStageCreateInfo[2];
        var main = "main"u8;
        VkPipeline pipeline;
        fixed (byte* name = main)
        fixed (byte* vertex = _sharpVertex)
        fixed (byte* fragment = _sharpFragment)
        {
            var vertexInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)_sharpVertex!.Length, pCode = (uint*)vertex };
            _deviceApi.vkCreateShaderModule(&vertexInfo, null, out modules[0]).CheckResult();
            var fragmentInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)_sharpFragment!.Length, pCode = (uint*)fragment };
            _deviceApi.vkCreateShaderModule(&fragmentInfo, null, out modules[1]).CheckResult();
            stages[0] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Vertex, module = modules[0], pName = name };
            stages[1] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Fragment, module = modules[1], pName = name };
            var vertexInput = new VkPipelineVertexInputStateCreateInfo();
            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo { topology = VkPrimitiveTopology.TriangleList };
            var viewportState = new VkPipelineViewportStateCreateInfo { viewportCount = 1, scissorCount = 1 };
            var rasterizer = new VkPipelineRasterizationStateCreateInfo
            {
                polygonMode = VkPolygonMode.Fill, cullMode = VkCullModeFlags.None, frontFace = VkFrontFace.CounterClockwise, lineWidth = 1f,
            };
            var multisample = new VkPipelineMultisampleStateCreateInfo { rasterizationSamples = VkSampleCountFlags.Count1 };
            var attachment = new VkPipelineColorBlendAttachmentState
            {
                colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A,
            };
            var colorBlend = new VkPipelineColorBlendStateCreateInfo { attachmentCount = 1, pAttachments = &attachment };
            var dynamics = stackalloc VkDynamicState[2] { VkDynamicState.Viewport, VkDynamicState.Scissor };
            var dynamicState = new VkPipelineDynamicStateCreateInfo { dynamicStateCount = 2, pDynamicStates = dynamics };
            var rendering = new VkPipelineRenderingCreateInfo { colorAttachmentCount = 1, pColorAttachmentFormats = &format };
            var info = new VkGraphicsPipelineCreateInfo
            {
                pNext = &rendering,
                stageCount = 2,
                pStages = stages,
                pVertexInputState = &vertexInput,
                pInputAssemblyState = &inputAssembly,
                pViewportState = &viewportState,
                pRasterizationState = &rasterizer,
                pMultisampleState = &multisample,
                pColorBlendState = &colorBlend,
                pDynamicState = &dynamicState,
                layout = _sharpLayout,
            };
            _deviceApi.vkCreateGraphicsPipelines(VkPipelineCache.Null, 1, &info, null, &pipeline).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.Pipeline);
        }
        _deviceApi.vkDestroyShaderModule(modules[0]);
        _deviceApi.vkDestroyShaderModule(modules[1]);
        return pipeline;
    }

    // Records the frame image drawn onto the window's image by the sharp scaling into the rectangle
    // at x, y of width by height, the rest black, the window's image left ready to present and the
    // frame image ready to be copied from as the frame's passes leave it.
    private void RecordSharp(VkCommandBuffer cmd, uint imageIndex, int x, int y, int width, int height)
    {
        var frame = _swapchainImages[imageIndex];
        var shown = _shownImages[imageIndex];
        var whole = ColorLevels(0, 1);
        PipelineBarrier(cmd, [
            ImageBarrier(frame, whole, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ColorAttachmentWrite,
                VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderSampledRead),
            ImageBarrier(shown, whole, VkImageLayout.Undefined, VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput, VkAccessFlags2.None,
                VkPipelineStageFlags2.ColorAttachmentOutput, VkAccessFlags2.ColorAttachmentWrite),
        ]);
        var color = new VkRenderingAttachmentInfo
        {
            imageView = _shownViews[imageIndex],
            imageLayout = VkImageLayout.ColorAttachmentOptimal,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            clearValue = new VkClearValue(0f, 0f, 0f, 1f),
        };
        var info = new VkRenderingInfo
        {
            renderArea = new VkRect2D(0, 0, _shownExtent.width, _shownExtent.height),
            layerCount = 1,
            colorAttachmentCount = 1,
            pColorAttachments = &color,
        };
        _deviceApi.vkCmdBeginRendering(cmd, &info);
        var viewport = new VkViewport(x, y, width, height, 0f, 1f);
        _deviceApi.vkCmdSetViewport(cmd, 0, 1, &viewport);
        var scissor = new VkRect2D(x, y, (uint)width, (uint)height);
        _deviceApi.vkCmdSetScissor(cmd, 0, 1, &scissor);
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Graphics, _sharpPipeline);
        var set = _sharpSets[imageIndex];
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Graphics, _sharpLayout, 0, 1, &set, 0, null);
        var push = stackalloc float[4]
        {
            x, y, (float)width / _swapchainExtent.width, (float)height / _swapchainExtent.height,
        };
        _deviceApi.vkCmdPushConstants(cmd, _sharpLayout, VkShaderStageFlags.Fragment, 0, 16, push);
        _deviceApi.vkCmdDraw(cmd, 3, 1, 0, 0);
        _deviceApi.vkCmdEndRendering(cmd);
        PipelineBarrier(cmd, [
            ImageBarrier(frame, whole, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.None, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.None),
            ImageBarrier(shown, whole, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.PresentSrcKHR,
                VkPipelineStageFlags2.ColorAttachmentOutput, VkAccessFlags2.ColorAttachmentWrite, VkPipelineStageFlags2.None, VkAccessFlags2.None),
        ]);
    }

    // The sets and views made for the frame images, let go with them.
    private void DestroySharpFrames()
    {
        foreach (var view in _shownViews)
            if (view.Handle != 0) _deviceApi.vkDestroyImageView(view);
        _shownViews = [];
        DestroyDescriptorPool(_sharpPool);
        _sharpPool = default;
        _sharpSets = [];
    }

    private void DestroySharpPipeline()
    {
        if (_sharpPipeline.Handle == 0) return;
        DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
        _deviceApi.vkDestroyPipeline(_sharpPipeline);
        _sharpPipeline = default;
    }

    // Runs before the device goes.
    private void DestroySharp()
    {
        DestroySharpFrames();
        DestroySharpPipeline();
        if (_sharpLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_sharpLayout);
        if (_sharpSetLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_sharpSetLayout);
        (_sharpLayout, _sharpSetLayout, _sharpVertex, _sharpFragment) = (default, default, null, null);
    }
}
