namespace Engine;

internal sealed partial class DynamicBufferAllocator
{
    /// <summary>Per-frame bump arena maintaining one backing buffer per <see cref="BufferUsage"/>.</summary>
    private sealed partial class FrameArena
    {
        // One backing buffer per usage type encountered
        private readonly Dictionary<BufferUsage, ArenaBuffer> _buffers = new();

        // Buffers outgrown during this slot's frame. Draws recorded earlier in that frame still
        // read them, so they live until the slot comes round again, when its fence has signaled.
        private readonly List<IBuffer> _outgrown = [];

        /// <summary>Resets all write cursors to zero, and frees what this slot outgrew last time.</summary>
        public void Reset()
        {
            foreach (var ab in _buffers.Values)
                ab.Cursor = 0;
            foreach (var buffer in _outgrown) buffer.Dispose();
            _outgrown.Clear();
        }

        /// <summary>Allocates a sub-region of the given size and usage, growing the backing buffer if needed.</summary>
        /// <param name="gfx">Graphics device used to create/resize buffers.</param>
        /// <param name="size">Size in bytes of the allocation.</param>
        /// <param name="usage">Buffer usage flags determining which backing arena to allocate from.</param>
        /// <returns>A <see cref="DynamicAllocation"/> describing the buffer, offset, and size of the allocation.</returns>
        /// <param name="alignment">What the allocation's offset is a multiple of, a power of two.</param>
        public DynamicAllocation Allocate(IGraphicsDevice gfx, ulong size, BufferUsage usage, ulong alignment = 1)
        {
            if (!_buffers.TryGetValue(usage, out var ab))
            {
                ab = new ArenaBuffer();
                _buffers[usage] = ab;
            }

            var start = (ab.Cursor + alignment - 1) & ~(alignment - 1);
            if (ab.Buffer is null || ab.Capacity < start + size)
            {
                // A larger buffer starts empty. The outgrown one keeps what this frame wrote into
                // it, for the draws already recorded against it, until the slot's next frame.
                if (ab.Buffer is not null) _outgrown.Add(ab.Buffer);

                ulong newCap = Math.Max(MinBufferSize, ab.Capacity);
                while (newCap < size)
                    newCap = NextPowerOfTwo(newCap * 2);

                ab.Buffer = gfx.CreateBuffer(new BufferDesc(newCap, usage, CpuAccessMode.Write));
                ab.Capacity = newCap;
                ab.Cursor = 0;
                start = 0;
            }

            ab.Cursor = start + size;
            return new DynamicAllocation(ab.Buffer!, start, size);
        }

        /// <summary>Disposes all backing GPU buffers and resets all arena state.</summary>
        public void DisposeAll()
        {
            foreach (var buffer in _outgrown) buffer.Dispose();
            _outgrown.Clear();
            foreach (var ab in _buffers.Values)
            {
                ab.Buffer?.Dispose();
                ab.Buffer = null;
                ab.Capacity = 0;
                ab.Cursor = 0;
            }
            _buffers.Clear();
        }
    }

    /// <summary>Holds a single GPU buffer, its capacity, and the current write cursor.</summary>
    private sealed class ArenaBuffer
    {
        /// <summary>The GPU buffer, or <c>null</c> if not yet allocated.</summary>
        public IBuffer? Buffer;
        /// <summary>Total byte capacity of <see cref="Buffer"/>.</summary>
        public ulong Capacity;
        /// <summary>Current byte offset of the next allocation.</summary>
        public ulong Cursor;
    }

    /// <summary>Rounds <paramref name="v"/> up to the next power of two (minimum <see cref="MinBufferSize"/>).</summary>
    private static ulong NextPowerOfTwo(ulong v)
    {
        if (v == 0) return MinBufferSize;
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        v |= v >> 32;
        return v + 1;
    }
}
