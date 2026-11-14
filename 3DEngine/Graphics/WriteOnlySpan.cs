using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Memory the GPU reads, mapped for the CPU to write, which can be set, sliced, cast, copied into,
/// cleared and filled, and not read.
/// </summary>
/// <remarks>
/// A driver may give such memory pages that are written in order and read back many times slower
/// than they are written, so each value is worked out before it is written and the memory is never
/// read. With no getter, a read of mapped memory does not compile, and the buffers whose GPU
/// results are read have a map of their own that reads (<see cref="IGraphicsDevice.Read"/>).
/// </remarks>
internal readonly ref struct WriteOnlySpan<T> where T : unmanaged
{
    private readonly Span<T> _span;

    /// <summary>Wraps the mapped memory <paramref name="span"/> covers.</summary>
    public WriteOnlySpan(Span<T> span) => _span = span;

    /// <summary>How many elements it holds.</summary>
    public int Length => _span.Length;

    /// <summary>Sets the element at <paramref name="index"/>.</summary>
    public T this[int index]
    {
        set => _span[index] = value;
    }

    /// <summary>The elements from <paramref name="start"/> on.</summary>
    public WriteOnlySpan<T> Slice(int start) => new(_span[start..]);

    /// <summary>The <paramref name="length"/> elements from <paramref name="start"/>.</summary>
    public WriteOnlySpan<T> Slice(int start, int length) => new(_span.Slice(start, length));

    /// <summary>The same memory as elements of another type.</summary>
    public WriteOnlySpan<TTo> Cast<TTo>() where TTo : unmanaged => new(MemoryMarshal.Cast<T, TTo>(_span));

    /// <summary>Writes <paramref name="value"/> at the start of the memory, as its bytes lie.</summary>
    public void Write<TValue>(in TValue value) where TValue : unmanaged => MemoryMarshal.Write(MemoryMarshal.AsBytes(_span), in value);

    /// <summary>Copies <paramref name="source"/> into the start of the memory.</summary>
    public void CopyFrom(ReadOnlySpan<T> source) => source.CopyTo(_span);

    /// <summary>Sets every element to its default.</summary>
    public void Clear() => _span.Clear();

    /// <summary>Sets every element to <paramref name="value"/>.</summary>
    public void Fill(T value) => _span.Fill(value);
}
