using FluentAssertions;
using Xunit;

namespace Engine.Tests.Renderer;

[Trait("Category", "Unit")]
public class RendererDiagnosticsTests
{
    [Fact]
    public void FramesRendered_Starts_At_Zero()
    {
        var diag = new RendererDiagnostics();

        diag.FramesRendered.Should().Be(0UL);
    }

    [Fact]
    public void RecordFrame_Increments_FramesRendered()
    {
        var diag = new RendererDiagnostics();
        var extent = new Extent2D(800, 600);

        diag.RecordFrame(GraphicsAdapterInfo.Unknown, extent, 0);
        diag.RecordFrame(GraphicsAdapterInfo.Unknown, extent, 0);

        diag.FramesRendered.Should().Be(2UL);
    }

    [Fact]
    public void RecordFrame_Updates_SurfaceExtent()
    {
        var diag = new RendererDiagnostics();
        var extent = new Extent2D(1920, 1080);

        diag.RecordFrame(GraphicsAdapterInfo.Unknown, extent, 0);

        diag.SurfaceExtent.Width.Should().Be(1920);
        diag.SurfaceExtent.Height.Should().Be(1080);
    }

    [Fact]
    public void RecordFrame_Updates_SurfaceRevision()
    {
        var diag = new RendererDiagnostics();

        diag.RecordFrame(GraphicsAdapterInfo.Unknown, new Extent2D(800, 600), 5);

        diag.SurfaceRevision.Should().Be(5UL);
    }

    [Fact]
    public void Initialize_Sets_AdapterInfo()
    {
        var diag = new RendererDiagnostics();
        var info = new GraphicsAdapterInfo("TestGPU", 0x1234, 0x5678, GraphicsDeviceType.DiscreteGpu);

        diag.Initialize(info);

        diag.AdapterInfo.Name.Should().Be("TestGPU");
        diag.AdapterInfo.DeviceType.Should().Be(GraphicsDeviceType.DiscreteGpu);
    }

    [Fact]
    public void AdapterInfo_Defaults_To_Unknown()
    {
        var diag = new RendererDiagnostics();

        diag.AdapterInfo.Should().Be(GraphicsAdapterInfo.Unknown);
    }
}
