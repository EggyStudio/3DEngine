using FluentAssertions;
using Xunit;

namespace Engine.Tests.Graphics;

[Trait("Category", "Unit")]
public class NullGraphicsDeviceTests
{
    private readonly NullGraphicsDevice _device = new();

    // -- Initialization --

    [Fact]
    public void IsInitialized_False_Before_Initialize()
    {
        _device.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Initialize_Sets_IsInitialized()
    {
        _device.Initialize(new StubSurfaceSource());

        _device.IsInitialized.Should().BeTrue();
    }

    // -- Swapchain --

    [Fact]
    public void Swapchain_Reports_1x1_Extent()
    {
        _device.Swapchain.Extent.Width.Should().Be(1);
        _device.Swapchain.Extent.Height.Should().Be(1);
    }

    [Fact]
    public void Swapchain_ImageCount_Is_One()
    {
        _device.Swapchain.ImageCount.Should().Be(1u);
    }

    [Fact]
    public void Swapchain_AcquireNextImage_Returns_Success()
    {
        var result = _device.Swapchain.AcquireNextImage(out var index);

        result.Should().Be(AcquireResult.Success);
        index.Should().Be(0u);
    }

    // -- Adapter info --

    [Fact]
    public void AdapterInfo_Returns_Unknown()
    {
        _device.AdapterInfo.Should().Be(GraphicsAdapterInfo.Unknown);
    }

    // -- FramesInFlight --

    [Fact]
    public void FramesInFlight_Is_One()
    {
        _device.FramesInFlight.Should().Be(1);
    }

    // -- BeginFrame / EndFrame cycle --

    [Fact]
    public void BeginFrame_EndFrame_Cycle_Succeeds()
    {
        var frame = _device.BeginFrame();

        frame.Should().NotBeNull();
        frame.FrameIndex.Should().Be(1u);
        frame.CommandBuffer.Should().NotBeNull();
        frame.Extent.Width.Should().Be(1);
        frame.Extent.Height.Should().Be(1);
        frame.FramesInFlight.Should().Be(1);

        // EndFrame should not throw
        var act = () => _device.EndFrame(frame);
        act.Should().NotThrow();
    }

    [Fact]
    public void BeginFrame_Increments_FrameIndex()
    {
        var f1 = _device.BeginFrame();
        var f2 = _device.BeginFrame();

        f2.FrameIndex.Should().BeGreaterThan(f1.FrameIndex);
    }

    // -- OnResize / Dispose do not throw --

    [Fact]
    public void OnResize_Does_Not_Throw()
    {
        var act = () => _device.OnResize();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        var act = () => _device.Dispose();
        act.Should().NotThrow();
    }

    // -- Unmap does not throw --

    [Fact]
    public void Unmap_Does_Not_Throw()
    {
        var act = () => _device.Unmap(null!);
        act.Should().NotThrow();
    }

    // -- All resource creation methods throw NotSupportedException --

    [Fact]
    public void CreateBuffer_Throws_NotSupportedException()
    {
        var act = () => _device.CreateBuffer(new BufferDesc(16, BufferUsage.Vertex, CpuAccessMode.Write));
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Map_Throws_NotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => _device.Map(null!));
    }

    [Fact]
    public void CreateImage_Throws_NotSupportedException()
    {
        var act = () => _device.CreateImage(default);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateImageView_Throws_NotSupportedException()
    {
        var act = () => _device.CreateImageView(null!);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateSampler_Throws_NotSupportedException()
    {
        var act = () => _device.CreateSampler(default);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateShader_Throws_NotSupportedException()
    {
        var act = () => _device.CreateShader(default);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateGraphicsPipeline_Throws_NotSupportedException()
    {
        var act = () => _device.CreateGraphicsPipeline(default);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateDescriptorSet_Throws_NotSupportedException()
    {
        var act = () => _device.CreateDescriptorSet();
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UpdateDescriptorSet_Throws_NotSupportedException()
    {
        var act = () => _device.UpdateDescriptorSet(null!, null, null);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void BindGraphicsPipeline_Is_NoOp()
    {
        var act = () => _device.BindGraphicsPipeline(null!, null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void BindDescriptorSet_Is_NoOp()
    {
        var act = () => _device.BindDescriptorSet(null!, null!, null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void Draw_Is_NoOp()
    {
        var act = () => _device.Draw(null!, 3);
        act.Should().NotThrow();
    }

    [Fact]
    public void DrawIndexed_Is_NoOp()
    {
        var act = () => _device.DrawIndexed(null!, 3);
        act.Should().NotThrow();
    }

    [Fact]
    public void BindVertexBuffers_Is_NoOp()
    {
        var act = () => _device.BindVertexBuffers(null!, 0, [], []);
        act.Should().NotThrow();
    }

    [Fact]
    public void BindIndexBuffer_Is_NoOp()
    {
        var act = () => _device.BindIndexBuffer(null!, null!, 0, IndexType.UInt16);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetViewport_Is_NoOp()
    {
        var act = () => _device.SetViewport(null!, 0, 0, 100, 100, 0, 1);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetScissor_Is_NoOp()
    {
        var act = () => _device.SetScissor(null!, 0, 0, 100, 100);
        act.Should().NotThrow();
    }

    [Fact]
    public void PushConstants_Is_NoOp()
    {
        var act = () => _device.PushConstants(null!, null!, ShaderStageFlags.All, 0, ReadOnlySpan<byte>.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void UploadTexture2D_Throws_NotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _device.UploadTexture2D(null!, ReadOnlySpan<byte>.Empty, 1, 1, 4));
    }

    // -- Stub --

    private sealed class StubSurfaceSource : ISurfaceSource
    {
        public IReadOnlyList<string> GetRequiredInstanceExtensions() => Array.Empty<string>();
        public nint CreateSurfaceHandle(nint instanceHandle) => 0;
        public (uint Width, uint Height) GetDrawableSize() => (800, 600);
    }
}
