using System.Numerics;
using System.Runtime.InteropServices;
using RadianceCascades;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

internal sealed unsafe partial class GlobalIlluminationStages
{
    // What gi_split.slang is pushed, the pass and its cascade, the store's shape, whether each way
    // starts again from the frame once its frames disagree with its light, and the frame's origin
    // against the store's.
    [StructLayout(LayoutKind.Sequential)]
    private struct SplitPush
    {
        public uint Pass, Cascade, Unused0, Unused1;
        public uint Slots, Cascades, Agree, Unused3;
        public float Spacing, Unused4, Unused5, Unused6;
        public Vector3 Origin;
        public float Unused7;
    }

    private const uint SplitPushBytes = 64;

    // The threads a row of a split pass's dispatch, 1024 groups of 64, as gi_split.slang numbers
    // an index.
    private const uint SplitRow = 65536;

    /// <summary>
    /// Whether the split cascades' build through the field was given and its pipeline made.
    /// </summary>
    internal bool SplitsCascades => GiStages[SplitStage].Pipeline.Handle != 0;

    /// <summary>
    /// Records a frame of the RadianceCascades module's split cascades over the window
    /// (<c>gi_split.slang</c>) after the world's probes are merged, in the order its passes run,
    /// the store's probes asked for by the points of <paramref name="depth"/> the eye sees, a ray
    /// traced from each point, the rays' light kept and merged from the coarsest cascade down, and
    /// gathered onto each pixel of <paramref name="surface"/>, left ready for the model pass to
    /// read, and each pixel's gather read by its causes into the surface's
    /// <see cref="GpuSurfaceGather.Causes"/> where it holds them. Each point takes the normal
    /// <paramref name="normals"/> holds beside the depth where it holds one
    /// (<see cref="WindowDepth.Normals"/>). Each way whose rays may cross a box of
    /// <paramref name="changes"/>, the store's <see cref="GpuSplitStore.Changes"/> bytes, lets the
    /// light it kept go.
    /// The rays meet the meshes through <paramref name="rays"/> where it is given and its build was
    /// made, and else the field.
    /// </summary>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordSplitGather(ICommandBuffer commandBuffer, GpuIllumination gi, GpuSurfaceGather surface, GpuSplitStore store, GpuSceneField field,
        IImageView depth, ISampler depthSampler, IImageView normals, ReadOnlySpan<byte> view, IImageView environment, ISampler environmentSampler,
        GpuRayScene? rays, Vector3 origin, ReadOnlySpan<byte> changes, bool agree = false)
    {
        var meshes = rays is not null && GiStages[RaySplitStage].Pipeline.Handle != 0;
        var stage = meshes ? RaySplitStage : SplitStage;
        var run = new ProbeRun(this, device, commandBuffer, [(stage, 1)]);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = view[..(Math.Min(view.Length, GpuScreenProbes.ViewBytes) & ~3)];
        fixed (byte* data = bytes)
            device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)surface.View).Buffer, 0, (ulong)bytes.Length, data);
        fixed (byte* data = changes)
            device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)store.Changes).Buffer, 0, (ulong)changes.Length, data);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead);
        var light = ((VulkanImage)surface.Light).Image;
        device.PipelineBarrier(cmd, [ImageBarrier(light, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead)]);

        var set = run.Set(stage);
        if (meshes) device.WriteRayScene(set, RayScreenFirst, rays!);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 1, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 2, VkDescriptorType.UniformBuffer, field.Info);
        run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 5, VkDescriptorType.UniformBuffer, gi.Lights);
        run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 7, VkDescriptorType.CombinedImageSampler, gi.CubesAtView, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 8, VkDescriptorType.UniformBuffer, surface.View);
        run.Image(set, 9, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)environment).View, environmentSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 10, VkDescriptorType.SampledImage, ((VulkanImageView)field.SecondView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 11, VkDescriptorType.SampledImage, ((VulkanImageView)field.FacesView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 12, VkDescriptorType.StorageBuffer, store.Uints);
        run.Buffer(set, 13, VkDescriptorType.StorageBuffer, store.Light);
        run.Buffer(set, 16, VkDescriptorType.StorageBuffer, store.Merged);
        run.Image(set, 17, VkDescriptorType.SampledImage, ((VulkanImageView)normals).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, (uint)(meshes ? RayScreenFirst + 3 : RayScreenFirst), VkDescriptorType.UniformBuffer, store.Changes);
        run.Image(set, 14, VkDescriptorType.StorageImage, ((VulkanImageView)surface.LightView).View, null, VkImageLayout.General);
        // The gathered light stands in for the causes where the surface holds none, the pass that
        // writes them not dispatched.
        run.Image(set, 15, VkDescriptorType.StorageImage, ((VulkanImageView)(surface.CausesView ?? surface.LightView)).View, null, VkImageLayout.General);
        var (pipeline, layout, _) = GiStages[stage];
        device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
        device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);

        // The passes a frame runs in the module's order, each over what it runs on
        // (gi_split.slang).
        var shape = store.Shape;
        var push = new SplitPush
        {
            Slots = (uint)shape.Slots, Cascades = (uint)shape.Cascades, Agree = agree ? 1u : 0u, Spacing = shape.Spacing, Origin = origin,
        };
        var points = (long)surface.Width * surface.Height;
        void Pass(uint pass, int cascade, long threads)
        {
            push.Pass = pass;
            push.Cascade = (uint)cascade;
            var groups = (uint)((threads + 63) / 64);
            var row = SplitRow / 64;
            var p = push;
            device.Api.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, SplitPushBytes, &p);
            device.Api.vkCmdDispatch(cmd, Math.Min(groups, row), (groups + row - 1) / row, 1);
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader,
                VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        }
        // Each pass timed apart where gi.passes asks, the cascades' asks and merges each as one.
        Pass(0, 0, 4L * shape.Slots);
        MarkGlobalIllumination(commandBuffer, "gi_split_clear");
        Pass(1, 0, shape.Entries);
        MarkGlobalIllumination(commandBuffer, "gi_split_keep");
        Pass(2, 0, points);
        MarkGlobalIllumination(commandBuffer, "gi_split_ask");
        for (int c = 0; c + 1 < shape.Cascades; c++)
            Pass(3, c, shape.PoolProbes(c));
        MarkGlobalIllumination(commandBuffer, "gi_split_ask_cascades");
        Pass(4, 0, points);
        MarkGlobalIllumination(commandBuffer, "gi_split_trace");
        Pass(5, 0, shape.OwnLights);
        MarkGlobalIllumination(commandBuffer, "gi_split_light");
        for (int c = shape.Cascades - 1; c >= 0; c--)
            Pass(6, c, shape.CascadeLights(c));
        MarkGlobalIllumination(commandBuffer, "gi_split_merge");
        Pass(7, 0, points);
        MarkGlobalIllumination(commandBuffer, "gi_split_gather");
        if (surface.Causes is not null)
        {
            Pass(8, 0, points);
            MarkGlobalIllumination(commandBuffer, "gi_split_cause");
        }

        device.PipelineBarrier(cmd, [ImageBarrier(light, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, readers, VkAccessFlags2.ShaderRead)]);
        return run;
    }
}
