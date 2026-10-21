using System.Numerics;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// A copy of part of a storage buffer, asked for now and read once the GPU has made it, which
/// <see cref="GraphicsDevice.ReadBack"/> answers.
/// </summary>
internal sealed class ShaderReadbackTicket
{
    internal ShaderReadbackTicket(IBuffer staging, int size, object batch) => (Staging, Size, Batch) = (staging, size, batch);

    /// <summary>The buffer the copy lands in, one of the pool's.</summary>
    internal IBuffer Staging { get; }

    /// <summary>How many bytes were asked for.</summary>
    public int Size { get; }

    // The batch of copies it was recorded in.
    internal object Batch { get; }

    // Whether the program has let it go, so its staging buffer goes back to the pool once the
    // batch is done with it.
    internal bool Released { get; set; }
}

internal sealed unsafe partial class GraphicsDevice
{
    // A batch of copies, recorded into one command buffer and submitted with a fence of its own.
    private sealed class ReadbackBatch(VkCommandBuffer commands)
    {
        public readonly VkCommandBuffer Commands = commands;
        public readonly List<ShaderReadbackTicket> Tickets = [];
        public VkFence Fence;
        public bool Done;
    }

    // The copies asked for since the last submission, which go to the queue before the next one
    // of any kind, the frame's, a dispatch's or a wait's, so a copy runs after every dispatch
    // submitted before it was asked and before any submitted after, and the frame of the ask
    // never finds it made. Its own lock and pool, since an ask may come while a dispatch on
    // another thread holds the compute lock and submits.
    private readonly Lock _readbackGate = new();
    private ReadbackBatch? _recordingReadbacks;
    private readonly List<ReadbackBatch> _readbacksInFlight = [];
    private VkCommandPool _readbackPool;

    // The staging buffers not in use, by their size, a power of two from MinStaging up. A game
    // reading every frame asks for the same sizes again and again, so it reuses a few buffers
    // rather than making one a frame.
    private const int MinStaging = 256;
    private const int StagingKeptASize = 4;
    private readonly Dictionary<int, Stack<IBuffer>> _stagingPool = [];
    private int _stagingLive;

    /// <summary>How many staging buffers the readbacks hold, in use or kept for the next.</summary>
    internal int ReadbackStagingCount
    {
        get { lock (_readbackGate) return _stagingLive; }
    }

    /// <summary>
    /// Asks for <paramref name="size"/> bytes of a storage buffer from <paramref name="offset"/> to
    /// be copied out, after the dispatches submitted so far, without waiting for them.
    /// </summary>
    /// <remarks>
    /// The copy is recorded now and submitted with the next submission, so it is never made in the
    /// frame it was asked in unless a wait or a dispatch comes first. <see cref="IsReadbackReady"/>
    /// says when it is made, and <see cref="ReadReadback"/> reads it.
    /// </remarks>
    public ShaderReadbackTicket ReadBack(IBuffer source, int offset, int size)
    {
        var vk = (VulkanBuffer)source;
        lock (_readbackGate)
        {
            RetireReadbacks(wait: false);
            if (_readbackPool.Handle == 0)
            {
                var poolInfo = new VkCommandPoolCreateInfo { flags = VkCommandPoolCreateFlags.Transient, queueFamilyIndex = _graphicsQueueFamily };
                _deviceApi.vkCreateCommandPool(&poolInfo, null, out _readbackPool).CheckResult();
            }

            if (_recordingReadbacks is not { } batch)
            {
                var commandInfo = new VkCommandBufferAllocateInfo { commandPool = _readbackPool, level = VkCommandBufferLevel.Primary, commandBufferCount = 1 };
                VkCommandBuffer cmd;
                _deviceApi.vkAllocateCommandBuffers(&commandInfo, &cmd).CheckResult();
                var begin = new VkCommandBufferBeginInfo { flags = VkCommandBufferUsageFlags.OneTimeSubmit };
                _deviceApi.vkBeginCommandBuffer(cmd, &begin).CheckResult();
                // The copies read what earlier work on the queue wrote, a dispatch's above all.
                MemoryBarrier(cmd, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.MemoryWrite,
                    VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead);
                _recordingReadbacks = batch = new ReadbackBatch(cmd);
            }

            var staging = RentStaging(size);
            var region = new VkBufferCopy { srcOffset = (ulong)offset, dstOffset = 0, size = (ulong)size };
            _deviceApi.vkCmdCopyBuffer(batch.Commands, vk.Buffer, ((VulkanBuffer)staging).Buffer, 1, &region);
            var ticket = new ShaderReadbackTicket(staging, size, batch);
            batch.Tickets.Add(ticket);
            return ticket;
        }
    }

    /// <summary>Whether a readback's copy has been made, without waiting for it.</summary>
    public bool IsReadbackReady(ShaderReadbackTicket ticket)
    {
        var batch = (ReadbackBatch)ticket.Batch;
        lock (_readbackGate)
        {
            if (!batch.Done && batch.Fence.Handle != 0 && _deviceApi.vkGetFenceStatus(batch.Fence) == VkResult.Success)
                RetireReadbacks(wait: false);
            return batch.Done;
        }
    }

    /// <summary>
    /// Copies a readback's bytes into <paramref name="destination"/>, submitting and waiting for its
    /// copy first where it has not been made, as one read in the frame it was asked in has not.
    /// </summary>
    public void ReadReadback(ShaderReadbackTicket ticket, Span<byte> destination)
    {
        var batch = (ReadbackBatch)ticket.Batch;
        if (!IsReadbackReady(ticket))
        {
            // Submitted with the uploads ahead of it, as every submission sends them, and waited
            // for unless the flush found it done.
            FlushUploads();
            lock (_readbackGate)
                if (!batch.Done) RetireReadbacks(wait: true, until: batch);
        }
        Map(ticket.Staging)[..Math.Min(ticket.Size, destination.Length)].CopyTo(destination);
    }

    /// <summary>
    /// Lets a readback go, its staging buffer back to the pool at once where its copy is made, or
    /// once it is where it is still on the queue.
    /// </summary>
    public void ReleaseReadback(ShaderReadbackTicket ticket)
    {
        lock (_readbackGate)
        {
            if (ticket.Released) return;
            ticket.Released = true;
            if (((ReadbackBatch)ticket.Batch).Done) ReturnStaging(ticket.Staging);
        }
    }

    // Submits the copies recorded since the last submission, without waiting for them, and frees
    // what the batches already done held. FlushUploads calls it, so it runs before every
    // submission and every wait.
    private void FlushReadbacks()
    {
        lock (_readbackGate)
        {
            RetireReadbacks(wait: false);
            if (_recordingReadbacks is not { } batch) return;
            _recordingReadbacks = null;
            // The CPU reads the staging buffers once the fence is waited on or seen signaled.
            MemoryBarrier(batch.Commands, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.Host, VkAccessFlags2.HostRead);
            _deviceApi.vkEndCommandBuffer(batch.Commands).CheckResult();
            var fenceInfo = new VkFenceCreateInfo();
            _deviceApi.vkCreateFence(&fenceInfo, null, out batch.Fence).CheckResult();
            var commands = batch.Commands;
            var submit = new VkSubmitInfo { commandBufferCount = 1, pCommandBuffers = &commands };
            _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submit, batch.Fence).CheckResult();
            _readbacksInFlight.Add(batch);
        }
    }

    // Frees what finished batches held, the staging buffers of readbacks already let go back to
    // the pool, waiting for the batches up to until first where asked, or for all of them.
    private void RetireReadbacks(bool wait, ReadbackBatch? until = null)
    {
        for (var i = 0; i < _readbacksInFlight.Count; i++)
        {
            var batch = _readbacksInFlight[i];
            var fence = batch.Fence;
            if (wait) _deviceApi.vkWaitForFences(1, &fence, true, ulong.MaxValue).CheckResult();
            else if (_deviceApi.vkGetFenceStatus(fence) != VkResult.Success) continue;

            _deviceApi.vkDestroyFence(fence);
            var commands = batch.Commands;
            _deviceApi.vkFreeCommandBuffers(_readbackPool, 1, &commands);
            batch.Done = true;
            foreach (var ticket in batch.Tickets)
                if (ticket.Released) ReturnStaging(ticket.Staging);
            batch.Tickets.Clear();
            _readbacksInFlight.RemoveAt(i--);
            if (wait && ReferenceEquals(batch, until)) return;
        }
    }

    private IBuffer RentStaging(int size)
    {
        var bucket = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(MinStaging, size));
        if (_stagingPool.TryGetValue(bucket, out var kept) && kept.TryPop(out var buffer)) return buffer;
        _stagingLive++;
        return CreateBuffer(new BufferDesc((ulong)bucket, BufferUsage.TransferDst, CpuAccessMode.Read));
    }

    private void ReturnStaging(IBuffer buffer)
    {
        var bucket = (int)buffer.Description.Size;
        if (!_stagingPool.TryGetValue(bucket, out var kept)) _stagingPool[bucket] = kept = new Stack<IBuffer>();
        if (kept.Count < StagingKeptASize)
        {
            kept.Push(buffer);
            return;
        }
        buffer.Dispose();
        _stagingLive--;
    }

    // Frees every batch and staging buffer, with the queue idle, before the device goes.
    private void DestroyReadbacks()
    {
        lock (_readbackGate)
        {
            if (_recordingReadbacks is { } recording)
            {
                var commands = recording.Commands;
                _deviceApi.vkEndCommandBuffer(commands).CheckResult();
                _deviceApi.vkFreeCommandBuffers(_readbackPool, 1, &commands);
                foreach (var ticket in recording.Tickets) ticket.Staging.Dispose();
                _recordingReadbacks = null;
            }
            foreach (var batch in _readbacksInFlight) batch.Tickets.ForEach(ticket => ticket.Released = true);
            RetireReadbacks(wait: true);
            foreach (var kept in _stagingPool.Values)
                while (kept.TryPop(out var buffer)) buffer.Dispose();
            _stagingPool.Clear();
            _stagingLive = 0;
            if (_readbackPool.Handle != 0) _deviceApi.vkDestroyCommandPool(_readbackPool);
            _readbackPool = default;
        }
    }
}
