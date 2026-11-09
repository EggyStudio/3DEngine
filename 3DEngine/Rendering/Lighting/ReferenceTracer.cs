using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// A path-traced reference of the window's picture, each pixel's light, linear, red, green and blue
/// a pixel with rows from the top, and what its first ray met, a copy's index in the ray scene times
/// eight and the axis its face turns toward most (+x, -x, +y, -y, +z, -z), plus one, or 0 for nothing.
/// </summary>
internal sealed record TracedReference(int Width, int Height, float[] Light, uint[] Regions, float[]? Met = null);

/// <summary>
/// The path-traced reference on the device, its compute stage and the passes that trace it, a part
/// the device keeps (<see cref="GraphicsDevice.Part{T}"/>) and lets go with itself.
/// </summary>
internal sealed unsafe class ReferenceTracer(GraphicsDevice device) : IDisposable
{
    private ComputeStage _referenceStage;

    // gi_reference.slang's push block.
    [StructLayout(LayoutKind.Sequential)]
    private struct ReferencePush
    {
        public Matrix4x4 InverseViewProjection;
        public uint Width, Height, Samples, Bounces;
        public uint First, FromProbe, Through, Specular;
        public Vector4 Probe;
    }

    /// <summary>The bytes of gi_reference.slang's lights, four rows and then sixteen lamps of four rows each.</summary>
    internal const int ReferenceLightsBytes = 64 + 16 * 64;

    /// <summary>The textures the reference reads its copies' colors and normals from at most, as <c>gi_reference.slang</c>'s <c>ReferenceTextures</c> has them, the white one first.</summary>
    internal const int ReferenceTextures = 256;

    /// <summary>The bytes of a copy's look as <c>gi_reference.slang</c>'s <c>CopyLook</c> reads it, its textures' slots and its strength, then its color before the texture.</summary>
    internal const int ReferenceLookBytes = 32;

    /// <summary>
    /// Traces <paramref name="samples"/> paths a pixel through <paramref name="scene"/>, as the
    /// frame drew it, from the camera whose clip space <paramref name="inverseViewProjection"/>
    /// turns into the world, at <paramref name="width"/> by <paramref name="height"/>, with
    /// <c>gi_reference.slang</c>'s <paramref name="spirv"/>, its <paramref name="lights"/>, and the
    /// environment a path that meets nothing reads where they say to.
    /// </summary>
    /// <remarks>
    /// The frames in flight are waited for first, and the samples are added a few a submission,
    /// each waited for, so no submission runs long enough for a driver to take the device for lost.
    /// With <paramref name="probe"/> the paths start at that point instead, a texel of the
    /// <paramref name="width"/> by <paramref name="height"/> image a texel of a probe's octahedron,
    /// and every face they meet is lit as global illumination lights one, which gives the light
    /// arriving at a probe from each way. A path crosses a copy that lets light through by its
    /// share where the scene holds one, and a scene that holds none is traced as it was before.
    /// <paramref name="gloss"/> says which faces of a camera's path reflect the indirect light by
    /// their specular share too, a way drawn about its mirror way by GGX's visible normals, and of a
    /// probe's paths every face where it asks for every face, as the probes' rays light what they
    /// meet by its gloss, and none otherwise. Each copy's <paramref name="looks"/>, in the order of
    /// the scene's copies, name its textures' slots in <paramref name="textures"/>, the white one
    /// first and in every slot past them, which a path reads at each point it meets.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The device traces no rays, or indexes no array of textures.</exception>
    public TracedReference TraceReference(ReadOnlySpan<byte> spirv, GpuRayScene scene, Matrix4x4 inverseViewProjection, int width, int height,
        int samples, ReadOnlySpan<byte> lights, IImageView environment, ISampler environmentSampler, IReadOnlyList<(IImageView View, ISampler Sampler)> textures,
        ReadOnlySpan<byte> looks, int bounces = 32, int perSubmission = 4, Vector3? probe = null, ReferenceGloss gloss = ReferenceGloss.None)
    {
        if (!device.CanQueryRays) throw new InvalidOperationException("The device traces no rays.");
        if (!device.CanIndexTextures) throw new InvalidOperationException("The device indexes no array of textures, which the reference reads its copies' textures through.");
        var through = scene.Copies.Any(copy => copy.LightThrough > 0);
        if (_referenceStage.Pipeline.Handle == 0)
            _referenceStage = device.MakeComputeStage(spirv, [VkDescriptorType.AccelerationStructureKHR, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageBuffer, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer, VkDescriptorType.CombinedImageSampler],
                (uint)Marshal.SizeOf<ReferencePush>(), [1, 1, 1, 1, 1, 1, 1, 1, 1, ReferenceTextures]);
        device.FlushUploads();
        device.Api.vkDeviceWaitIdle().CheckResult();

        var (w, h) = ((uint)Math.Max(1, width), (uint)Math.Max(1, height));
        var (sums, sumsMemory, sumsView) = StorageImage2D(w, h, VkFormat.R32G32B32A32Sfloat);
        // Each pixel's region and how far its first sample went before it met it, -1 for nowhere.
        var (regions, regionsMemory, regionsView) = StorageImage2D(w, h, VkFormat.R32G32Uint);
        var lightBuffer = (VulkanBuffer)device.CreateBuffer(new BufferDesc(ReferenceLightsBytes, BufferUsage.Uniform, CpuAccessMode.Write));
        lights[..Math.Min(lights.Length, ReferenceLightsBytes)].CopyTo(device.Map(lightBuffer));
        device.Unmap(lightBuffer);
        var readSums = (VulkanBuffer)device.CreateBuffer(new BufferDesc(w * h * 16, BufferUsage.TransferDst, CpuAccessMode.Read));
        var readRegions = (VulkanBuffer)device.CreateBuffer(new BufferDesc(w * h * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
        var lookBuffer = (VulkanBuffer)device.CreateBuffer(new BufferDesc((ulong)Math.Max(ReferenceLookBytes, looks.Length), BufferUsage.Storage, CpuAccessMode.Write));
        looks.CopyTo(device.Map(lookBuffer));
        device.Unmap(lookBuffer);

        // The array of textures takes a descriptor of the pool's for each of its slots.
        var pool = device.MakeDescriptorPool([([.. _referenceStage.Descriptors, .. Enumerable.Repeat(VkDescriptorType.CombinedImageSampler, ReferenceTextures - 1)], 1)]);
        try
        {
            var setLayout = _referenceStage.SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
            VkDescriptorSet set;
            device.Api.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            WriteReferenceSet(set, scene, sumsView, regionsView, lightBuffer, environment, environmentSampler, lookBuffer, textures);

            // The sums and the regions cleared, and kept in the general layout the passes and the
            // copy all take.
            var cmd = device.BeginSingleTimeCommands();
            var whole = ColorLevels(0, 1);
            device.PipelineBarrier(cmd, [
                ImageBarrier(sums, whole, VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                    VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
                ImageBarrier(regions, whole, VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                    VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            ]);
            var nothing = new VkClearColorValue(0f, 0f, 0f, 0f);
            device.Api.vkCmdClearColorImage(cmd, sums, VkImageLayout.General, &nothing, 1, &whole);
            var none = new VkClearColorValue(0u, 0u, 0u, 0u);
            device.Api.vkCmdClearColorImage(cmd, regions, VkImageLayout.General, &none, 1, &whole);
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
            device.EndSingleTimeCommands(cmd);

            var (pipeline, layout, _) = _referenceStage;
            for (int first = 0; first < samples; first += perSubmission)
            {
                cmd = device.BeginSingleTimeCommands();
                device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
                device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
                var push = new ReferencePush
                {
                    InverseViewProjection = inverseViewProjection,
                    Width = w, Height = h,
                    Samples = (uint)Math.Min(perSubmission, samples - first),
                    Bounces = (uint)Math.Max(1, bounces),
                    First = (uint)first,
                    FromProbe = probe is null ? 0u : 1u,
                    Through = through ? 1u : 0u,
                    Specular = probe is null || gloss == ReferenceGloss.Every ? (uint)gloss : 0u,
                    Probe = new Vector4(probe ?? Vector3.Zero, 0),
                };
                device.Api.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, (uint)sizeof(ReferencePush), &push);
                device.Api.vkCmdDispatch(cmd, (w + 7) / 8, (h + 7) / 8, 1);
                device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                    VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferRead);
                device.EndSingleTimeCommands(cmd);
            }

            cmd = device.BeginSingleTimeCommands();
            var copy = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(w, h, 1),
            };
            device.Api.vkCmdCopyImageToBuffer(cmd, sums, VkImageLayout.General, readSums.Buffer, 1, &copy);
            device.Api.vkCmdCopyImageToBuffer(cmd, regions, VkImageLayout.General, readRegions.Buffer, 1, &copy);
            device.EndSingleTimeCommands(cmd);

            var summed = MemoryMarshal.Cast<byte, Vector4>(device.Map(readSums));
            var light = new float[w * h * 3];
            for (int i = 0; i < summed.Length && i * 3 < light.Length; i++)
            {
                var s = summed[i];
                var count = Math.Max(s.W, 1);
                (light[i * 3], light[i * 3 + 1], light[i * 3 + 2]) = (s.X / count, s.Y / count, s.Z / count);
            }
            var pairs = MemoryMarshal.Cast<byte, uint>(device.Map(readRegions));
            var (regionOf, metAt) = (new uint[w * h], new float[w * h]);
            for (int i = 0; i < regionOf.Length; i++) (regionOf[i], metAt[i]) = (pairs[i * 2], BitConverter.UInt32BitsToSingle(pairs[i * 2 + 1]));
            return new TracedReference((int)w, (int)h, light, regionOf, metAt);
        }
        finally
        {
            device.DestroyDescriptorPool(pool);
            readSums.Dispose();
            readRegions.Dispose();
            lightBuffer.Dispose();
            lookBuffer.Dispose();
            foreach (var (image, memory, view) in new[] { (sums, sumsMemory, sumsView), (regions, regionsMemory, regionsView) })
            {
                device.Api.vkDestroyImageView(view);
                device.Api.vkDestroyImage(image);
                device.Api.vkFreeMemory(memory);
                DeviceObjects.Gone(DeviceObjects.Kind.Image);
                DeviceObjects.Gone(DeviceObjects.Kind.Memory);
            }
        }
    }

    // The reference's set, the scene's top-level structure, its copies and its corners, the sums
    // and the regions, the lights and the environment, the corners' texture coordinates, the
    // copies' looks and their textures, the first slot's in every slot past those given.
    private void WriteReferenceSet(VkDescriptorSet set, GpuRayScene scene, VkImageView sums, VkImageView regions, VulkanBuffer lights,
        IImageView environment, ISampler sampler, VulkanBuffer looks, IReadOnlyList<(IImageView View, ISampler Sampler)> textures)
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
        device.Api.vkUpdateDescriptorSets(7, writes, 0, null);

        var uvs = scene.Uvs ?? surfaces;
        var more = stackalloc VkDescriptorBufferInfo[2];
        more[0] = new VkDescriptorBufferInfo { buffer = uvs.Buffer, offset = 0, range = uvs.Size };
        more[1] = new VkDescriptorBufferInfo { buffer = looks.Buffer, offset = 0, range = Vulkan.VK_WHOLE_SIZE };
        var slots = stackalloc VkDescriptorImageInfo[ReferenceTextures];
        for (int i = 0; i < ReferenceTextures; i++)
        {
            var (view, slotSampler) = textures[i < textures.Count ? i : 0];
            slots[i] = new VkDescriptorImageInfo
            {
                imageView = ((VulkanImageView)view).View,
                sampler = ((VulkanSampler)slotSampler).Sampler,
                imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
            };
        }
        var moreWrites = stackalloc VkWriteDescriptorSet[3];
        moreWrites[0] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 7, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &more[0] };
        moreWrites[1] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 8, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &more[1] };
        moreWrites[2] = new VkWriteDescriptorSet
        {
            dstSet = set, dstBinding = 9, descriptorCount = ReferenceTextures, descriptorType = VkDescriptorType.CombinedImageSampler, pImageInfo = slots,
        };
        device.Api.vkUpdateDescriptorSets(3, moreWrites, 0, null);
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
        device.Api.vkCreateImage(&info, null, out VkImage image).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Image);
        device.Api.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = device.FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        var memory = device.AllocateSaying(&allocation);
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        device.Api.vkBindImageMemory(image, memory, 0).CheckResult();
        var viewInfo = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.Image2D,
            format = format,
            components = VkComponentMapping.Rgba,
            subresourceRange = ColorLevels(0, 1),
        };
        device.Api.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
        return (image, memory, view);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_referenceStage.Pipeline.Handle == 0) return;
        DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
        device.Api.vkDestroyPipeline(_referenceStage.Pipeline);
        device.Api.vkDestroyPipelineLayout(_referenceStage.Layout);
        device.Api.vkDestroyDescriptorSetLayout(_referenceStage.SetLayout);
        _referenceStage = default;
    }
}
