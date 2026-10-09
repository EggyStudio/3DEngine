using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// A path-traced reference of the window's picture: each pixel's light, linear, red, green and blue
/// a pixel with rows from the top, and what its first ray met, a copy's index in the ray scene times
/// eight and the axis its face turns toward most (+x, -x, +y, -y, +z, -z), plus one, or 0 for nothing.
/// </summary>
internal sealed record TracedReference(int Width, int Height, float[] Light, uint[] Regions);

internal sealed unsafe partial class GraphicsDevice
{
    private ComputeStage _referenceStage;

    // gi_reference.slang's push block.
    [StructLayout(LayoutKind.Sequential)]
    private struct ReferencePush
    {
        public Matrix4x4 InverseViewProjection;
        public uint Width, Height, Samples, Bounces;
        public uint First, FromProbe, Unused0, Unused1;
        public Vector4 Probe;
    }

    /// <summary>The bytes of gi_reference.slang's lights: four rows, then sixteen lamps of four rows each.</summary>
    internal const int ReferenceLightsBytes = 64 + 16 * 64;

    /// <summary>
    /// Traces <paramref name="samples"/> paths a pixel through <paramref name="scene"/>, as the
    /// frame drew it, from the camera whose clip space <paramref name="inverseViewProjection"/>
    /// turns into the world, at <paramref name="width"/> by <paramref name="height"/>, with
    /// <c>gi_reference.slang</c>'s <paramref name="spirv"/>, its <paramref name="lights"/>, and the
    /// environment a path that meets nothing reads where they say to.
    /// </summary>
    /// <remarks>
    /// The frames in flight are waited for first, and the samples are added a few a submission, each
    /// waited for, so no submission runs long enough for a driver to take the device for lost. With
    /// <paramref name="probe"/> the paths start at that point instead, a texel of the
    /// <paramref name="width"/> by <paramref name="height"/> image a texel of a probe's octahedron,
    /// and every face they meet is lit as the light that bounces lights one, which gives the light
    /// arriving at a probe from each way.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The device traces no rays.</exception>
    public TracedReference TraceReference(ReadOnlySpan<byte> spirv, GpuRayScene scene, Matrix4x4 inverseViewProjection, int width, int height,
        int samples, ReadOnlySpan<byte> lights, IImageView environment, ISampler environmentSampler, int bounces = 32, int perSubmission = 4,
        Vector3? probe = null)
    {
        if (!CanQueryRays) throw new InvalidOperationException("The device traces no rays.");
        if (_referenceStage.Pipeline.Handle == 0)
            _referenceStage = MakeComputeStage(spirv, [VkDescriptorType.AccelerationStructureKHR, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageBuffer, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler], (uint)Marshal.SizeOf<ReferencePush>());
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();

        var (w, h) = ((uint)Math.Max(1, width), (uint)Math.Max(1, height));
        var (sums, sumsMemory, sumsView) = StorageImage2D(w, h, VkFormat.R32G32B32A32Sfloat);
        var (regions, regionsMemory, regionsView) = StorageImage2D(w, h, VkFormat.R32Uint);
        var lightBuffer = (VulkanBuffer)CreateBuffer(new BufferDesc(ReferenceLightsBytes, BufferUsage.Uniform, CpuAccessMode.Write));
        lights[..Math.Min(lights.Length, ReferenceLightsBytes)].CopyTo(Map(lightBuffer));
        Unmap(lightBuffer);
        var readSums = (VulkanBuffer)CreateBuffer(new BufferDesc(w * h * 16, BufferUsage.TransferDst, CpuAccessMode.Read));
        var readRegions = (VulkanBuffer)CreateBuffer(new BufferDesc(w * h * 4, BufferUsage.TransferDst, CpuAccessMode.Read));

        var sizes = stackalloc VkDescriptorPoolSize[5];
        sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.AccelerationStructureKHR, descriptorCount = 1 };
        sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = 2 };
        sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = 2 };
        sizes[3] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 1 };
        sizes[4] = new VkDescriptorPoolSize { type = VkDescriptorType.CombinedImageSampler, descriptorCount = 1 };
        var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = 1, poolSizeCount = 5, pPoolSizes = sizes };
        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out var pool).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.DescriptorPool);
        try
        {
            var setLayout = _referenceStage.SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            WriteReferenceSet(set, scene, sumsView, regionsView, lightBuffer, environment, environmentSampler);

            // The sums and the regions cleared, and kept in the general layout the passes and the
            // copy all take.
            var cmd = BeginSingleTimeCommands();
            var whole = ColorLevels(0, 1);
            PipelineBarrier(cmd, [
                ImageBarrier(sums, whole, VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                    VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
                ImageBarrier(regions, whole, VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                    VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            ]);
            var nothing = new VkClearColorValue(0f, 0f, 0f, 0f);
            _deviceApi.vkCmdClearColorImage(cmd, sums, VkImageLayout.General, &nothing, 1, &whole);
            var none = new VkClearColorValue(0u, 0u, 0u, 0u);
            _deviceApi.vkCmdClearColorImage(cmd, regions, VkImageLayout.General, &none, 1, &whole);
            MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
            EndSingleTimeCommands(cmd);

            var (pipeline, layout, _) = _referenceStage;
            for (int first = 0; first < samples; first += perSubmission)
            {
                cmd = BeginSingleTimeCommands();
                _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
                _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
                var push = new ReferencePush
                {
                    InverseViewProjection = inverseViewProjection,
                    Width = w, Height = h,
                    Samples = (uint)Math.Min(perSubmission, samples - first),
                    Bounces = (uint)Math.Max(1, bounces),
                    First = (uint)first,
                    FromProbe = probe is null ? 0u : 1u,
                    Probe = new Vector4(probe ?? Vector3.Zero, 0),
                };
                _deviceApi.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, (uint)sizeof(ReferencePush), &push);
                _deviceApi.vkCmdDispatch(cmd, (w + 7) / 8, (h + 7) / 8, 1);
                MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                    VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferRead);
                EndSingleTimeCommands(cmd);
            }

            cmd = BeginSingleTimeCommands();
            var copy = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(w, h, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, sums, VkImageLayout.General, readSums.Buffer, 1, &copy);
            _deviceApi.vkCmdCopyImageToBuffer(cmd, regions, VkImageLayout.General, readRegions.Buffer, 1, &copy);
            EndSingleTimeCommands(cmd);

            var summed = MemoryMarshal.Cast<byte, Vector4>(Map(readSums));
            var light = new float[w * h * 3];
            for (int i = 0; i < summed.Length && i * 3 < light.Length; i++)
            {
                var s = summed[i];
                var count = Math.Max(s.W, 1);
                (light[i * 3], light[i * 3 + 1], light[i * 3 + 2]) = (s.X / count, s.Y / count, s.Z / count);
            }
            var met = MemoryMarshal.Cast<byte, uint>(Map(readRegions)).ToArray();
            return new TracedReference((int)w, (int)h, light, met);
        }
        finally
        {
            DeviceObjects.Gone(DeviceObjects.Kind.DescriptorPool);
            _deviceApi.vkDestroyDescriptorPool(pool);
            readSums.Dispose();
            readRegions.Dispose();
            lightBuffer.Dispose();
            foreach (var (image, memory, view) in new[] { (sums, sumsMemory, sumsView), (regions, regionsMemory, regionsView) })
            {
                _deviceApi.vkDestroyImageView(view);
                _deviceApi.vkDestroyImage(image);
                _deviceApi.vkFreeMemory(memory);
                DeviceObjects.Gone(DeviceObjects.Kind.Image);
                DeviceObjects.Gone(DeviceObjects.Kind.Memory);
            }
        }
    }

    // The reference's set: the scene's top-level structure, its copies and its corners, the sums
    // and the regions, the lights and the environment.
    private void WriteReferenceSet(VkDescriptorSet set, GpuRayScene scene, VkImageView sums, VkImageView regions, VulkanBuffer lights,
        IImageView environment, ISampler sampler)
    {
        var top = scene.Top;
        var structure = new VkWriteDescriptorSetAccelerationStructureKHR { accelerationStructureCount = 1, pAccelerationStructures = &top };
        var surfaces = scene.Surfaces[scene.Slot]!;
        var corners = scene.Corners ?? surfaces;
        var buffers = stackalloc VkDescriptorBufferInfo[3];
        buffers[0] = new VkDescriptorBufferInfo { buffer = surfaces.Buffer, offset = 0, range = surfaces.Size };
        buffers[1] = new VkDescriptorBufferInfo { buffer = corners.Buffer, offset = 0, range = corners.Size };
        buffers[2] = new VkDescriptorBufferInfo { buffer = lights.Buffer, offset = 0, range = ReferenceLightsBytes };
        var images = stackalloc VkDescriptorImageInfo[3];
        images[0] = new VkDescriptorImageInfo { imageView = sums, imageLayout = VkImageLayout.General };
        images[1] = new VkDescriptorImageInfo { imageView = regions, imageLayout = VkImageLayout.General };
        images[2] = new VkDescriptorImageInfo
        {
            imageView = ((VulkanImageView)environment).View,
            sampler = ((VulkanSampler)sampler).Sampler,
            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        var writes = stackalloc VkWriteDescriptorSet[7];
        writes[0] = new VkWriteDescriptorSet { pNext = &structure, dstSet = set, dstBinding = 0, descriptorCount = 1, descriptorType = VkDescriptorType.AccelerationStructureKHR };
        writes[1] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 1, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &buffers[0] };
        writes[2] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 2, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &buffers[1] };
        writes[3] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 3, descriptorCount = 1, descriptorType = VkDescriptorType.StorageImage, pImageInfo = &images[0] };
        writes[4] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 4, descriptorCount = 1, descriptorType = VkDescriptorType.StorageImage, pImageInfo = &images[1] };
        writes[5] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 5, descriptorCount = 1, descriptorType = VkDescriptorType.UniformBuffer, pBufferInfo = &buffers[2] };
        writes[6] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 6, descriptorCount = 1, descriptorType = VkDescriptorType.CombinedImageSampler, pImageInfo = &images[2] };
        _deviceApi.vkUpdateDescriptorSets(7, writes, 0, null);
    }

    // A 2D image of a format a compute shader writes and a copy reads, with its view.
    private (VkImage Image, VkDeviceMemory Memory, VkImageView View) StorageImage2D(uint width, uint height, VkFormat format)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image2D,
            format = format,
            extent = new VkExtent3D(width, height, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined,
        };
        _deviceApi.vkCreateImage(&info, null, out VkImage image).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Image);
        _deviceApi.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        _deviceApi.vkAllocateMemory(&allocation, null, out VkDeviceMemory memory).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        _deviceApi.vkBindImageMemory(image, memory, 0).CheckResult();
        var viewInfo = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.Image2D,
            format = format,
            components = VkComponentMapping.Rgba,
            subresourceRange = ColorLevels(0, 1),
        };
        _deviceApi.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
        return (image, memory, view);
    }

    // Runs before the device goes.
    private void DestroyReference()
    {
        if (_referenceStage.Pipeline.Handle == 0) return;
        DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
        _deviceApi.vkDestroyPipeline(_referenceStage.Pipeline);
        _deviceApi.vkDestroyPipelineLayout(_referenceStage.Layout);
        _deviceApi.vkDestroyDescriptorSetLayout(_referenceStage.SetLayout);
        _referenceStage = default;
    }
}
