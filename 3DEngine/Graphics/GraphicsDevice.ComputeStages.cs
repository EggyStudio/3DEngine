using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    // One compute stage a part of the device records, its pipeline, layout and the set layout of
    // its bindings, with the type of each binding, one descriptor each, which a pool of its sets is
    // counted from.
    internal readonly record struct ComputeStage(VkPipeline Pipeline, VkPipelineLayout Layout, VkDescriptorSetLayout SetLayout)
    {
        public VkDescriptorType[] Descriptors { get; init; } = [];
    }

    internal ComputeStage MakeComputeStage(ReadOnlySpan<byte> spirv, VkDescriptorType[] types, uint pushSize, uint[]? counts = null)
    {
        var bindings = stackalloc VkDescriptorSetLayoutBinding[types.Length];
        for (int b = 0; b < types.Length; b++)
            bindings[b] = new VkDescriptorSetLayoutBinding
            {
                binding = (uint)b, descriptorType = types[b], descriptorCount = counts is { } c && b < c.Length ? c[b] : 1, stageFlags = VkShaderStageFlags.Compute,
            };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = (uint)types.Length, pBindings = bindings };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();

        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Compute, offset = 0, size = pushSize };
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out var layout).CheckResult();

        VkShaderModule module;
        fixed (byte* code = spirv)
        {
            var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
            _deviceApi.vkCreateShaderModule(&moduleInfo, null, out module).CheckResult();
        }
        VkPipeline pipeline;
        fixed (byte* name = "main"u8)
        {
            var info = new VkComputePipelineCreateInfo
            {
                stage = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Compute, module = module, pName = name },
                layout = layout,
            };
            pipeline = CreateComputePipeline(&info, spirv);
            DeviceObjects.Made(DeviceObjects.Kind.Pipeline);
        }
        _deviceApi.vkDestroyShaderModule(module);
        return new ComputeStage(pipeline, layout, setLayout) { Descriptors = types };
    }
}
