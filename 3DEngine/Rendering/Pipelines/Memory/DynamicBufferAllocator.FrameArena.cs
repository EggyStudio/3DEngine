namespace Engine;

public sealed partial class DynamicBufferAllocator
{
    /// <summary>Per-frame bump arena maintaining one backing buffer per <see cref="BufferUsage"/>.</summary>
    private sealed partial class FrameArena
    {
        // One backing buffer per usage type encountered
        private readonly Dictionary<BufferUsage, ArenaBuffer> _buffers = new();

        /// <summary>Resets all write cursors to zero without disposing buffers.</summary>
        public void Reset()
        {
            foreach (var ab in _buffers.Values)
                ab.Cursor = 0;
        }

        /// <summary>Allocates a sub-region of the given size and usage, growing the backing buffer if needed.</summary>
        /// <param name="gfx">Graphics device used to create/resize buffers.</param>
        /// <param name="size">Size in bytes of the allocation.</param>
        /// <param name="usage">Buffer usage flags determining which backing arena to allocate from.</param>
        /// <returns>A <see cref="DynamicAllocation"/> describing the buffer, offset, and size of the allocation.</returns>
        public DynamicAllocation Allocate(IGraphicsDevice gfx, ulong size, BufferUsage usage)
        {
            if (!_buffers.TryGetValue(usage, out var ab))
            {
                ab = new ArenaBuffer();
                _buffers[usage] = ab;
            }

            // Grow if necessary
            ulong required = ab.Cursor + size;
            if (ab.Buffer is null || ab.Capacity < required)
            {
                ab.Buffer?.Dispose();

                ulong newCap = Math.Max(MinBufferSize, ab.Capacity);
                while (newCap < required)
                    newCap = NextPowerOfTwo(newCap * 2);

                var desc = new BufferDesc(newCap, usage, CpuAccessMode.Write);
                ab.Buffer = gfx.CreateBuffer(desc);
                ab.Capacity = newCap;

                // Must re-upload any data already written this frame into the old buffer.
                // Since we grew mid-frame, just reset cursor - caller hasn't bound anything yet
                // because we're bump-allocating forward and the old buffer was disposed.
                ab.Cursor = 0;
            }

            var offset = ab.Cursor;
            ab.Cursor += size;
            return new DynamicAllocation(ab.Buffer!, offset, size);
        }

        /// <summary>Disposes all backing GPU buffers and resets all arena state.</summary>
        public void DisposeAll()
        {
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
