using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// The air's sky drawn on the GPU into an environment's equirectangular image, ahead of the filter
/// that reads it (<see cref="RecordEnvironmentFilter"/>), from two tables the air's numbers make,
/// as the Sky module's SkyLight draws it on the CPU.
/// </summary>
/// <remarks>
/// <para>
/// The transmittance by height and zenith (<c>atmosphere_transmittance.slang</c>) and the light
/// scattered more than once by height and the sun's zenith (<c>atmosphere_scatter.slang</c>) are
/// made again only where the air differs from the one they hold, which a day's hours never change,
/// and each texel of the image is a march along its way through the air from the ground
/// (<c>atmosphere_sky.slang</c>), its mips blitted from its first level for the filter to read.
/// </para>
/// <para>
/// The tables are kept in the general layout, written as storage images and read through a
/// sampler, so a frame that makes them again needs no move of their layout.
/// </para>
/// </remarks>
internal sealed unsafe partial class CubeFilter
{
    private const int AirTransmittanceStage = 5, AirScatterStage = 6, AirSkyStage = 7;

    // The two tables, made the first time the air's sky is drawn and kept, and the air they hold.
    private (IImage Image, IImageView View)? _airTransmittance, _airScatter;
    private Sky.Atmosphere? _airTabled;

    /// <summary>Whether the air's passes have been given, so an environment can be made from the air.</summary>
    public bool CanDrawAir => _filterStages[AirSkyStage].Pipeline.Handle != 0;

    /// <summary>
    /// Makes the air's pipelines from the compute stages of <c>atmosphere_transmittance.slang</c>,
    /// <c>atmosphere_scatter.slang</c> and <c>atmosphere_sky.slang</c>, once.
    /// </summary>
    public void InitializeAtmosphere(ReadOnlySpan<byte> transmittance, ReadOnlySpan<byte> scatter, ReadOnlySpan<byte> sky)
    {
        if (!device.IsInitialized || CanDrawAir) return;
        _filterStages[AirTransmittanceStage] = device.MakeComputeStage(transmittance, [VkDescriptorType.StorageImage], AirSky.PushSize);
        _filterStages[AirScatterStage] = device.MakeComputeStage(scatter, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], AirSky.PushSize);
        _filterStages[AirSkyStage] = device.MakeComputeStage(sky,
            [VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], AirSky.PushSize);
    }

    // The sets RecordAirSky takes of a run, the tables' where the air differs from theirs.
    private (int Stage, int Sets)[] AirSets(AirSky air) =>
        air.Air == _airTabled ? [(AirSkyStage, 1)] : [(AirTransmittanceStage, 1), (AirScatterStage, 1), (AirSkyStage, 1)];

    // Records the air's sky into every level of image, its first written by the pass and the rest
    // blitted down from it, left ready to sample.
    private void RecordAirSky(FilterRun run, VkCommandBuffer cmd, IImage image, AirSky air)
    {
        if (!CanDrawAir) throw new InvalidOperationException("The air's shaders have not been given.");
        var sampler = _filterSampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        var transmittance = _airTransmittance ??= Table(AirSky.TransmittanceAngles, AirSky.TransmittanceHeights, "Air's transmittance");
        var scatter = _airScatter ??= Table(AirSky.ScatterAngles, AirSky.ScatterHeights, "Air's light scattered more than once");
        static VkImageView Of(IImageView view) => ((VulkanImageView)view).View;

        if (air.Air != _airTabled)
        {
            // After any frame's reading of the tables, the transmittance and then the scattering
            // read from it.
            var first = _airTabled is null;
            device.PipelineBarrier(cmd, [
                ImageLayoutBarrier(transmittance.Image, first ? VkImageLayout.Undefined : VkImageLayout.General, VkImageLayout.General,
                    VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite),
                ImageLayoutBarrier(scatter.Image, first ? VkImageLayout.Undefined : VkImageLayout.General, VkImageLayout.General,
                    VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite)]);
            var made = run.Set(AirTransmittanceStage);
            run.Image(made, 0, VkDescriptorType.StorageImage, Of(transmittance.View), null, VkImageLayout.General);
            run.DispatchArea(AirTransmittanceStage, made, air.PushFor(0, 0), AirSky.TransmittanceAngles, AirSky.TransmittanceHeights);
            AirBetween(cmd);
            var scattered = run.Set(AirScatterStage);
            run.Image(scattered, 0, VkDescriptorType.CombinedImageSampler, Of(transmittance.View), sampler, VkImageLayout.General);
            run.Image(scattered, 1, VkDescriptorType.StorageImage, Of(scatter.View), null, VkImageLayout.General);
            run.DispatchArea(AirScatterStage, scattered, air.PushFor(0, 0), AirSky.ScatterAngles, AirSky.ScatterHeights);
            AirBetween(cmd);
            _airTabled = air.Air;
        }

        var (width, height) = (image.Description.Extent.Width, image.Description.Extent.Height);
        var level = device.CreateFirstLevelView(image);
        run.Holds(level);
        device.PipelineBarrier(cmd, ImageLayoutBarrier(image, VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite));
        var set = run.Set(AirSkyStage);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, Of(transmittance.View), sampler, VkImageLayout.General);
        run.Image(set, 1, VkDescriptorType.CombinedImageSampler, Of(scatter.View), sampler, VkImageLayout.General);
        run.Image(set, 2, VkDescriptorType.StorageImage, Of(level), null, VkImageLayout.General);
        run.DispatchArea(AirSkyStage, set, air.PushFor(width, height), width, height);
        device.PipelineBarrier(cmd, ImageLayoutBarrier(image, VkImageLayout.General, VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite));
        device.RecordMipChain(cmd, (VulkanImage)image);
    }

    // A table of half floats a pass writes and samples.
    private (IImage Image, IImageView View) Table(uint width, uint height, string name)
    {
        var image = device.CreateImage(new ImageDesc(new Extent2D(width, height), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(image, name);
        return (image, device.CreateImageView(image));
    }

    // What one dispatch of the air wrote is seen by the next that reads it.
    private void AirBetween(VkCommandBuffer cmd) => device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
        VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);

    // Lets the tables go with the device.
    private void DisposeAtmosphere()
    {
        foreach (var table in new[] { _airTransmittance, _airScatter })
            if (table is { } made)
            {
                made.View.Dispose();
                made.Image.Dispose();
            }
        (_airTransmittance, _airScatter, _airTabled) = (null, null, null);
    }
}
