using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>The span the uploads' maps hand out, which writes mapped memory and has no member that reads it.</summary>
[Trait("Category", "Unit")]
public sealed class WriteOnlySpanTests
{
    // An element no member of a span returns but one that hands its memory back.
    private readonly record struct Cell(long A, long B);

    // The public members of a span type that hand its memory back, one returning an element, a
    // reference or a pointer into it, an array, a span or an enumerator over it, a conversion, or
    // one taking a span, an array or a reference it could copy the memory out into.
    private static string[] Reading(Type span)
    {
        var element = span.GetGenericArguments()[0];
        static bool Spans(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Span<>);
        bool Hands(Type t) => t == element || t.IsByRef || t.IsPointer || t.IsArray || Spans(t)
            || t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>) || t.Name.EndsWith("Enumerator", StringComparison.Ordinal);
        bool Takes(ParameterInfo p) => p.ParameterType.IsByRef && !p.IsIn || p.ParameterType.IsPointer || p.ParameterType.IsArray || Spans(p.ParameterType);
        const BindingFlags members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return [.. span.GetMethods(members).Where(m => Hands(m.ReturnType) || m.Name is "op_Implicit" or "op_Explicit" || m.GetParameters().Any(Takes)).Select(m => m.Name),
            .. span.GetFields(members).Select(f => f.Name)];
    }

    [Fact]
    public void No_Member_Hands_Back_The_Memory_It_Writes()
    {
        Reading(typeof(Span<Cell>)).Should().Contain(new[] { "get_Item", "ToArray", "CopyTo", "GetEnumerator", "op_Implicit" },
            "the members of a span that reads are found");
        Reading(typeof(WriteOnlySpan<Cell>)).Should().BeEmpty("mapped memory is written and never read back");
        typeof(WriteOnlySpan<Cell>).GetProperty("Item")!.GetMethod.Should().BeNull("an element is set and not got");
    }

    [Fact]
    public void The_Maps_Uploads_Write_Through_Hand_Out_The_Span_That_Writes()
    {
        typeof(IGraphicsDevice).GetMethod(nameof(IGraphicsDevice.Map))!.ReturnType.Should().Be(typeof(WriteOnlySpan<byte>));
        typeof(DynamicBufferAllocator).GetMethod(nameof(DynamicBufferAllocator.Map))!.ReturnType.Should().Be(typeof(WriteOnlySpan<byte>));
        typeof(IGraphicsDevice).GetMethod(nameof(IGraphicsDevice.Read))!.ReturnType.Should().Be(typeof(ReadOnlySpan<byte>),
            "the buffers whose GPU results are read have a map of their own that reads");
    }

    [Fact]
    public void Each_Write_Lands_Where_It_Is_Told()
    {
        var bytes = new byte[32];
        var span = new WriteOnlySpan<byte>(bytes);
        span.Fill(9);
        span.Slice(4, 4).Clear();
        var words = span.Slice(8, 8).Cast<uint>();
        words[1] = 0x01020304;
        span.Slice(16).Write(new Vector2(1, 2));
        span.Slice(24).CopyFrom([5, 6]);

        span.Length.Should().Be(32);
        words.Length.Should().Be(2);
        bytes[..4].Should().OnlyContain(b => b == 9);
        bytes[4..8].Should().OnlyContain(b => b == 0);
        bytes[8..12].Should().OnlyContain(b => b == 9, "the first of the two uints was not written");
        MemoryMarshal.Read<uint>(bytes.AsSpan(12)).Should().Be(0x01020304);
        MemoryMarshal.Read<Vector2>(bytes.AsSpan(16)).Should().Be(new Vector2(1, 2));
        bytes[24..].Should().Equal(5, 6, 9, 9, 9, 9, 9, 9);
    }
}
