using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    // The picture's draw onto the window's pass (scaled.slang) where the frames are drawn at a size
    // of their own. Its pipeline is made the first time it draws, for the window's pass, and its
    // sets, one for each frame image, made again with them.
    private byte[]? _sharpVertex, _sharpFragment;
    private VkDescriptorSetLayout _sharpSetLayout;
    private VkPipelineLayout _sharpLayout;
    private VkPipeline _sharpPipeline;
    private VulkanRenderPass? _sharpPass;
    private VkDescriptorPool _sharpPool;
    private VkDescriptorSet[] _sharpSets = [];

    // The bytes of scaled.slang's PicturePush, the picture's corner on the window, the ratio and
    // the filter.
    private const uint PicturePush = 20;

    private static readonly VkDescriptorType[] SharpDescriptors = [VkDescriptorType.SampledImage];

    /// <summary>
    /// Takes <c>scaled.slang</c>'s stages, which the picture is drawn onto the window with.
    /// </summary>
    public void InitializePictureShader(ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> fragment)
    {
        if (!IsInitialized || _sharpVertex is not null) return;
        (_sharpVertex, _sharpFragment) = (vertex.ToArray(), fragment.ToArray());
        var binding = new VkDescriptorSetLayoutBinding
        {
            binding = 0, descriptorType = VkDescriptorType.SampledImage, descriptorCount = 1, stageFlags = VkShaderStageFlags.Fragment,
        };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 1, pBindings = &binding };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out _sharpSetLayout).CheckResult();
        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Fragment, offset = 0, size = PicturePush };
        var setLayout = _sharpSetLayout;
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out _sharpLayout).CheckResult();
    }

    // Whether the picture can be drawn this frame, its pipeline made for the window's pass and its
    // sets for the frame images, made here where they are not yet.
    private bool PictureReady()
    {
        if (_sharpVertex is null || _sharpFragment is null) return false;
        if (_sharpPipeline.Handle == 0 || _sharpPass != WindowPass)
        {
            DestroySharpPipeline();
            _sharpPipeline = MakeSharpPipeline(WindowPass);
            _sharpPass = WindowPass;
        }
        if (_sharpSets.Length == _swapchainImages.Length) return true;
        _sharpPool = MakeDescriptorPool([(SharpDescriptors, _swapchainImages.Length)]);
        _sharpSets = new VkDescriptorSet[_swapchainImages.Length];
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
        return true;
    }

    private VkPipeline MakeSharpPipeline(VulkanRenderPass pass)
    {
        var format = pass.ColorFormat;
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
            var multisample = new VkPipelineMultisampleStateCreateInfo { rasterizationSamples = pass.Samples };
            var depthStencil = new VkPipelineDepthStencilStateCreateInfo();
            var attachment = new VkPipelineColorBlendAttachmentState
            {
                colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A,
            };
            var colorBlend = new VkPipelineColorBlendStateCreateInfo { attachmentCount = 1, pAttachments = &attachment };
            var dynamics = stackalloc VkDynamicState[2] { VkDynamicState.Viewport, VkDynamicState.Scissor };
            var dynamicState = new VkPipelineDynamicStateCreateInfo { dynamicStateCount = 2, pDynamicStates = dynamics };
            var rendering = new VkPipelineRenderingCreateInfo
            {
                colorAttachmentCount = 1, pColorAttachmentFormats = &format, depthAttachmentFormat = pass.DepthFormat,
            };
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
                pDepthStencilState = &depthStencil,
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

    // The sets made for the frame images, let go with them.
    private void DestroySharpFrames()
    {
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
        _sharpPass = null;
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
