using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// The air's sky drawn on the GPU into an environment's equirectangular image, ahead of the filter
/// that reads it (<see cref="RecordEnvironmentFilter"/>), from the Sky module's tables of the air's
/// sky, as the module's SkyLight draws it on the CPU.
/// </summary>
/// <remarks>
/// The tables (<see cref="AirTables"/>) are made once for each air and put in a buffer the CPU
/// writes, made again only where the air differs from the one it holds, which a day's hours never
/// change, and each texel of the image is a march along its way through the air from the ground
/// (<c>atmosphere_sky.slang</c>), its mips blitted from its first level for the filter to read.
/// </remarks>
internal sealed unsafe partial class CubeFilter
{
    private const int AirSkyStage = 5;

    // The air's tables, uploaded the first time the air's sky is drawn and kept, and the air
    // they hold.
    private IBuffer? _airTables;
    private Sky.Atmosphere? _airTabled;

    /// <summary>Whether the air's pass has been given, so an environment can be made from
    /// the air.</summary>
    public bool CanDrawAir => _filterStages[AirSkyStage].Pipeline.Handle != 0;

    /// <summary>Makes the air's pipeline from the compute stage of
    /// <c>atmosphere_sky.slang</c>, once.</summary>
    public void InitializeAtmosphere(ReadOnlySpan<byte> sky)
    {
        if (!device.IsInitialized || CanDrawAir) return;
        _filterStages[AirSkyStage] = device.MakeComputeStage(sky, [VkDescriptorType.StorageBuffer, VkDescriptorType.StorageImage], AirSky.PushSize);
    }

    // The sets RecordAirSky takes of a run.
    private static (int Stage, int Sets)[] AirSets(AirSky air) => [(AirSkyStage, 1)];

    // Records the air's sky into every level of image, its first written by the pass and the rest
    // blitted down from it, left ready to sample.
    private void RecordAirSky(FilterRun run, VkCommandBuffer cmd, IImage image, AirSky air)
    {
        if (!CanDrawAir) throw new InvalidOperationException("The air's shader has not been given.");
        if (_airTables is null || air.Air != _airTabled)
        {
            // The tables of an air before stay until the frames that read them are done.
            var floats = AirTables.Of(air.Air).Floats;
            var tables = device.CreateBuffer(new BufferDesc((ulong)floats.Length * sizeof(float), BufferUsage.Storage, CpuAccessMode.Write));
            device.Name(tables, "Air's tables");
            device.FillBuffer(tables, 0, System.Runtime.InteropServices.MemoryMarshal.AsBytes(floats.AsSpan()));
            if (_airTables is { } before) run.Holds(before);
            (_airTables, _airTabled) = (tables, air.Air);
        }

        var (width, height) = (image.Description.Extent.Width, image.Description.Extent.Height);
        var level = device.CreateFirstLevelView(image);
        run.Holds(level);
        device.PipelineBarrier(cmd, ImageLayoutBarrier(image, VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite));
        var set = run.Set(AirSkyStage);
        run.Buffer(set, 0, VkDescriptorType.StorageBuffer, _airTables);
        run.Image(set, 1, VkDescriptorType.StorageImage, ((VulkanImageView)level).View, null, VkImageLayout.General);
        run.DispatchArea(AirSkyStage, set, air.PushFor(width, height), width, height);
        device.PipelineBarrier(cmd, ImageLayoutBarrier(image, VkImageLayout.General, VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite));
        device.RecordMipChain(cmd, (VulkanImage)image);
    }

    // Lets the tables go with the device.
    private void DisposeAtmosphere()
    {
        _airTables?.Dispose();
        (_airTables, _airTabled) = (null, null);
    }
}
