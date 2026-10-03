namespace Engine;

/// <summary>A sub-allocation from a <see cref="DynamicBufferAllocator"/>.</summary>
/// <param name="Buffer">The backing GPU buffer.</param>
/// <param name="Offset">Byte offset within <paramref name="Buffer"/> where this allocation starts.</param>
/// <param name="Size">Byte size of the allocation.</param>
public readonly record struct DynamicAllocation(IBuffer Buffer, ulong Offset, ulong Size);
