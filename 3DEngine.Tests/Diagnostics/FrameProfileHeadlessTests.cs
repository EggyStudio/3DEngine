using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// The frame profile of an app with a renderer and no device, as every headless run is, which ends
/// each frame without an error.
/// </summary>
/// <remarks>
/// In the collection of the other test that sets the log's callback, which is one for the process,
/// so the two never run together.
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class FrameProfileHeadlessTests : IDisposable
{
    public void Dispose()
    {
        SetTraceLogCallback(null);
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    public void A_Headless_Apps_Profile_Holds_Its_Frames_And_Logs_No_Error()
    {
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        SetTraceLogCallback((level, text) =>
        {
            if (level >= LogLevel.Error && text.Contains("FrameProfile", StringComparison.Ordinal)) errors.Enqueue(text);
        });
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

        for (int frame = 0; frame < 5; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }

        GetApp().World.Resource<FrameProfile>().Frames.Should().BeGreaterThanOrEqualTo(5, "each frame's measure ends it");
        errors.Should().BeEmpty("the profile asks for no device a headless app does not have");
    }
}
