using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// A traced reference of the window's ambient occlusion (<c>ao_reference.slang</c>), from the face
/// the eye's ray through each texel of the window's depth at half its size meets, rays drawn by
/// the cosine about that face,
/// each closed off where it meets one of the window's meshes within the radius, through the
/// device's rays global illumination at High holds the meshes for, so the horizons and the
/// circular occluders are measured against the scene's own surfaces and not the depth.
/// </summary>
internal sealed unsafe class OcclusionReference(GraphicsDevice device) : IDisposable
{
    /// <summary>The device's reference, made the first time it is asked for.</summary>
    internal static OcclusionReference Of(GraphicsDevice device) => device.Part(static device => new OcclusionReference(device));

    // The rays a pixel takes in one dispatch, and the dispatches in one submission.
    private const int RaysEach = 64;
    private const int DispatchesEach = 4;

    private ComputeStage _stage;

    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 EyeAndRadius;
        public uint Seed, Rays, Padding0, Padding1;
    }

    /// <summary>
    /// The share of <paramref name="rays"/> rays a pixel that meet no mesh within
    /// <paramref name="radius"/>, from each point of <paramref name="depth"/>, of
    /// <paramref name="extent"/>, through <paramref name="inverseViewProjection"/>, the camera's
    /// inverse view-projection in the world, from <paramref name="eye"/>, rows from the top, NaN
    /// where the depth holds nothing.
    /// </summary>
    public float[] Trace(ReadOnlySpan<byte> spirv, GpuRayScene scene, IImageView depth, ISampler sampler, Extent2D extent,
        Matrix4x4 inverseViewProjection, Vector3 eye, float radius, int rays)
    {
        if (!device.CanQueryRays) throw new InvalidOperationException("The device traces no rays.");
        if (_stage.Pipeline.Handle == 0)
            _stage = device.MakeComputeStage(spirv, [VkDescriptorType.AccelerationStructureKHR, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], (uint)sizeof(Push));
        device.FlushUploads();
        device.Api.vkDeviceWaitIdle().CheckResult();

        var (w, h) = (extent.Width, extent.Height);
        var origin = scene.Origin;
        // Each made inside the try, so what was made before a throw is destroyed.
        var (sums, memory, view) = (default(VkImage), default(VkDeviceMemory), default(VkImageView));
        VulkanBuffer? read = null;
        var pool = default(VkDescriptorPool);
        try
        {
            SumsImage(w, h, ref sums, ref memory, ref view);
            read = (VulkanBuffer)device.CreateBuffer(new BufferDesc(w * h * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
            pool = device.MakeDescriptorPool([(_stage.Descriptors, 1)]);
            var setLayout = _stage.SetLayout;
            var allocation = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
            VkDescriptorSet set;
            device.Api.vkAllocateDescriptorSets(&allocation, &set).CheckResult();
            Write(set, scene, depth, sampler, view);

            var whole = ColorLevels(0, 1);
            var cmd = device.BeginSingleTimeCommands();
            device.PipelineBarrier(cmd, ImageBarrier(sums, whole, VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite));
            var nothing = new VkClearColorValue(0f, 0f, 0f, 0f);
            device.Api.vkCmdClearColorImage(cmd, sums, VkImageLayout.General, &nothing, 1, &whole);
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
            device.EndSingleTimeCommands(cmd);

            var dispatches = (Math.Max(1, rays) + RaysEach - 1) / RaysEach;
            for (int first = 0; first < dispatches; first += DispatchesEach)
            {
                cmd = device.BeginSingleTimeCommands();
                device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _stage.Pipeline);
                device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _stage.Layout, 0, 1, &set, 0, null);
                for (int d = first; d < Math.Min(dispatches, first + DispatchesEach); d++)
                {
                    var push = new Push
                    {
                        InverseViewProjection = origin.Inverse(inverseViewProjection),
                        EyeAndRadius = new Vector4(origin.Against(eye), radius),
                        Seed = (uint)d,
                        Rays = (uint)Math.Min(RaysEach, rays - d * RaysEach),
                    };
                    device.Api.vkCmdPushConstants(cmd, _stage.Layout, VkShaderStageFlags.Compute, 0, (uint)sizeof(Push), &push);
                    device.Api.vkCmdDispatch(cmd, (w + 7) / 8, (h + 7) / 8, 1);
                    device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                        VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferRead);
                }
                device.EndSingleTimeCommands(cmd);
            }

            cmd = device.BeginSingleTimeCommands();
            var copy = new VkBufferImageCopy { imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1), imageExtent = new VkExtent3D(w, h, 1) };
            device.Api.vkCmdCopyImageToBuffer(cmd, sums, VkImageLayout.General, read.Buffer, 1, &copy);
            device.EndSingleTimeCommands(cmd);

            var pairs = MemoryMarshal.Cast<byte, Vector2>(device.Read(read));
            var shares = new float[w * h];
            for (int i = 0; i < shares.Length; i++) shares[i] = pairs[i].Y > 0 ? pairs[i].X / pairs[i].Y : float.NaN;
            return shares;
        }
        finally
        {
            device.DestroyDescriptorPool(pool);
            read?.Dispose();
            if (view.Handle != 0) device.Api.vkDestroyImageView(view);
            if (sums.Handle != 0)
            {
                device.Api.vkDestroyImage(sums);
                DeviceObjects.Gone(DeviceObjects.Kind.Image);
            }
            if (memory.Handle != 0)
            {
                device.Api.vkFreeMemory(memory);
                DeviceObjects.Gone(DeviceObjects.Kind.Memory);
            }
        }
    }

    // The set, the scene's top-level structure, its copies and its corners, the depth and the sums.
    private void Write(VkDescriptorSet set, GpuRayScene scene, IImageView depth, ISampler sampler, VkImageView sums)
    {
        var top = scene.Top;
        var structure = new VkWriteDescriptorSetAccelerationStructureKHR { accelerationStructureCount = 1, pAccelerationStructures = &top };
        var surfaces = scene.Surfaces!;
        var corners = scene.Corners ?? surfaces;
        var buffers = stackalloc VkDescriptorBufferInfo[2];
        buffers[0] = new VkDescriptorBufferInfo { buffer = surfaces.Buffer, offset = (ulong)scene.Slot * scene.SurfaceSlotBytes, range = scene.SurfaceSlotBytes };
        buffers[1] = new VkDescriptorBufferInfo { buffer = corners.Buffer, offset = 0, range = corners.Size };
        var images = stackalloc VkDescriptorImageInfo[2];
        images[0] = new VkDescriptorImageInfo
        {
            imageView = ((VulkanImageView)depth).View, sampler = ((VulkanSampler)sampler).Sampler, imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        images[1] = new VkDescriptorImageInfo { imageView = sums, imageLayout = VkImageLayout.General };
        var writes = stackalloc VkWriteDescriptorSet[5];
        writes[0] = new VkWriteDescriptorSet { pNext = &structure, dstSet = set, dstBinding = 0, descriptorCount = 1, descriptorType = VkDescriptorType.AccelerationStructureKHR };
        writes[1] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 1, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &buffers[0] };
        writes[2] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 2, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &buffers[1] };
        writes[3] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 3, descriptorCount = 1, descriptorType = VkDescriptorType.CombinedImageSampler, pImageInfo = &images[0] };
        writes[4] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 4, descriptorCount = 1, descriptorType = VkDescriptorType.StorageImage, pImageInfo = &images[1] };
        device.Api.vkUpdateDescriptorSets(5, writes, 0, null);
    }

    // The sums, two floats a texel, written by the dispatches and read by a copy, each of the
    // image, its memory and its view given back as it is made.
    private void SumsImage(uint width, uint height, ref VkImage image, ref VkDeviceMemory memory, ref VkImageView view)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image2D,
            format = VkFormat.R32G32Sfloat,
            extent = new VkExtent3D(width, height, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined,
        };
        device.Api.vkCreateImage(&info, null, out image).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Image);
        device.Api.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = device.FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        memory = device.AllocateSaying(&allocation);
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        device.Api.vkBindImageMemory(image, memory, 0).CheckResult();
        var viewInfo = new VkImageViewCreateInfo
        {
            image = image, viewType = VkImageViewType.Image2D, format = VkFormat.R32G32Sfloat, components = VkComponentMapping.Rgba, subresourceRange = ColorLevels(0, 1),
        };
        device.Api.vkCreateImageView(&viewInfo, null, out view).CheckResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_stage.Pipeline.Handle == 0) return;
        DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
        device.Api.vkDestroyPipeline(_stage.Pipeline);
        device.Api.vkDestroyPipelineLayout(_stage.Layout);
        device.Api.vkDestroyDescriptorSetLayout(_stage.SetLayout);
        _stage = default;
    }
}
