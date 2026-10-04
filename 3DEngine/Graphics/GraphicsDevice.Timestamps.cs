using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    // Up to this many marks a frame, each a timestamp the GPU writes when the commands before it
    // have finished, in a query pool with a range for each frame in flight.
    private const int TimestampsPerFrame = 64;
    private VkQueryPool _timestampPool;
    private bool _timestampsChecked;
    private bool _timestampsSupported;
    private double _nanosecondsPerTick;
    private ulong _timestampMask;
    private readonly List<string>[] _timestampLabels = Enumerable.Range(0, MaxFramesInFlight).Select(_ => new List<string>()).ToArray();
    private readonly bool[] _timestampsWritten = new bool[MaxFramesInFlight];

    /// <summary>Whether the device's graphics queue writes timestamps, which <see cref="Timestamp"/> needs.</summary>
    public bool TimestampsSupported
    {
        get
        {
            if (_timestampsChecked) return _timestampsSupported;
            _timestampsChecked = true;
            _instanceApi.vkGetPhysicalDeviceProperties(_physicalDevice, out var properties);
            uint count = 0;
            _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &count, null);
            var families = stackalloc VkQueueFamilyProperties[(int)count];
            _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &count, families);
            _timestampsSupported = properties.limits.timestampComputeAndGraphics && _graphicsQueueFamily < count
                                   && families[_graphicsQueueFamily].timestampValidBits > 0;
            _nanosecondsPerTick = properties.limits.timestampPeriod;
            // A queue may count in fewer than 64 bits, and a difference across its wrap is taken in those bits.
            var bits = _timestampsSupported ? families[_graphicsQueueFamily].timestampValidBits : 0;
            _timestampMask = bits >= 64 ? ulong.MaxValue : (1UL << (int)bits) - 1;
            if (_timestampsSupported)
            {
                var info = new VkQueryPoolCreateInfo
                {
                    queryType = VkQueryType.Timestamp,
                    queryCount = (uint)(TimestampsPerFrame * MaxFramesInFlight),
                };
                _deviceApi.vkCreateQueryPool(&info, null, out _timestampPool).CheckResult();
            }
            return _timestampsSupported;
        }
    }

    /// <summary>
    /// Starts a frame's marks in its in-flight slot, outside any render pass, and returns the GPU
    /// time between the marks the slot held the last time it was used, each labeled by the mark
    /// that ended it. The slot's fence was waited on when the frame began, so those are ready.
    /// </summary>
    public IReadOnlyList<(string Label, double Milliseconds)> BeginTimestamps(ICommandBuffer commandBuffer, int slot)
    {
        if (!TimestampsSupported || commandBuffer is not VulkanCommandBuffer vkCmd) return [];
        var labels = _timestampLabels[slot];
        var results = new List<(string, double)>();
        if (_timestampsWritten[slot] && labels.Count > 1)
        {
            var ticks = stackalloc ulong[labels.Count];
            var status = _deviceApi.vkGetQueryPoolResults(_timestampPool, (uint)(slot * TimestampsPerFrame), (uint)labels.Count,
                (nuint)(labels.Count * sizeof(ulong)), ticks, sizeof(ulong), VkQueryResultFlags.Bit64);
            if (status == VkResult.Success)
                for (int i = 1; i < labels.Count; i++)
                    results.Add((labels[i], ((ticks[i] - ticks[i - 1]) & _timestampMask) * _nanosecondsPerTick / 1e6));
        }

        labels.Clear();
        _deviceApi.vkCmdResetQueryPool(vkCmd.Handle, _timestampPool, (uint)(slot * TimestampsPerFrame), TimestampsPerFrame);
        _timestampsWritten[slot] = true;
        return results;
    }

    /// <summary>Marks the point in a frame's commands at which the GPU has finished everything recorded before it.</summary>
    public void Timestamp(ICommandBuffer commandBuffer, int slot, string label)
    {
        if (!TimestampsSupported || commandBuffer is not VulkanCommandBuffer vkCmd) return;
        var labels = _timestampLabels[slot];
        if (labels.Count >= TimestampsPerFrame) return;
        _deviceApi.vkCmdWriteTimestamp(vkCmd.Handle, VkPipelineStageFlags.BottomOfPipe, _timestampPool, (uint)(slot * TimestampsPerFrame + labels.Count));
        labels.Add(label);
    }

    private void DestroyTimestamps()
    {
        if (_timestampPool.Handle != 0) _deviceApi.vkDestroyQueryPool(_timestampPool);
        _timestampPool = default;
    }
}
