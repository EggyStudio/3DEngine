using System.Reflection;
using FluentAssertions;

namespace Engine.Tests.Rendering.Pipelines;

[Trait("Category", "Unit")]
public class DynamicBufferAllocatorTests
{
    public sealed class FakeBuffer(BufferDesc desc) : IBuffer
    {
        public BufferDesc Description { get; } = desc;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    // A device that makes buffers and does nothing else, which is all the allocator asks of one.
    public class BufferOnlyDevice : DispatchProxy
    {
        public readonly List<FakeBuffer> Made = [];

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_FramesInFlight") return 3;
            if (method.Name == nameof(IGraphicsDevice.CreateBuffer))
            {
                var buffer = new FakeBuffer((BufferDesc)args![0]!);
                Made.Add(buffer);
                return buffer;
            }
            throw new NotSupportedException(method.Name);
        }
    }

    private static (DynamicBufferAllocator Allocator, BufferOnlyDevice Device) Make()
    {
        var device = DispatchProxy.Create<IGraphicsDevice, BufferOnlyDevice>();
        var allocator = new DynamicBufferAllocator(device);
        allocator.BeginFrame(0);
        return (allocator, (BufferOnlyDevice)(object)device);
    }

    [Fact]
    public void Uniform_Allocations_Start_On_256_Byte_Boundaries()
    {
        var (allocator, _) = Make();

        var offsets = Enumerable.Range(0, 5).Select(_ => allocator.Allocate(1296, BufferUsage.Uniform).Offset).ToArray();

        offsets.Should().OnlyContain(o => o % 256 == 0);
        offsets.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void An_Outgrown_Buffer_Lives_Until_Its_Slot_Comes_Round_Again()
    {
        var (allocator, device) = Make();
        allocator.Allocate(64, BufferUsage.Uniform);
        var first = device.Made[0];

        // More than the first buffer holds, so the arena grows in the middle of the frame.
        allocator.Allocate(first.Description.Size * 4, BufferUsage.Uniform);
        device.Made.Should().HaveCount(2);
        first.Disposed.Should().BeFalse("a draw recorded earlier this frame still reads it");

        allocator.BeginFrame(1);
        allocator.BeginFrame(2);
        first.Disposed.Should().BeFalse();
        allocator.BeginFrame(0);
        first.Disposed.Should().BeTrue();
    }
}
