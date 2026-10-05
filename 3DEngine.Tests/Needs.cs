namespace Engine.Tests;

// Facts that need something of the machine report as skipped, with the reason, where it is
// missing, rather than pass by returning early, so a run's summary says how much of the suite ran.
// xUnit 2 decides a skip when it discovers the test, so each need is probed once per run, then.

/// <summary>Skipped where <c>slangc</c> is not found.</summary>
public sealed class NeedsSlangFactAttribute : FactAttribute
{
    public NeedsSlangFactAttribute()
    {
        if (!SlangCompiler.Available) Skip = "slangc was not found. Run build/fetch-slang.sh or set ENGINE_SLANGC.";
    }
}

/// <summary>Skipped where <c>slangc</c> is not found or no Vulkan device starts offscreen.</summary>
public sealed class NeedsVulkanFactAttribute : FactAttribute
{
    public NeedsVulkanFactAttribute()
    {
        if (!SlangCompiler.Available) Skip = "slangc was not found. Run build/fetch-slang.sh or set ENGINE_SLANGC.";
        // CI sets E3D_REQUIRE_VULKAN where it installs a device, so a device that does not start
        // there fails the render tests rather than skipping them all to a green run.
        else if (!Probes.Vulkan.Value && Environment.GetEnvironmentVariable("E3D_REQUIRE_VULKAN") != "1")
            Skip = "No Vulkan device starts offscreen here. A software one, such as lavapipe, is enough.";
    }
}

/// <summary>Skipped where SDL finds no audio device to open.</summary>
public sealed class NeedsAudioDeviceFactAttribute : FactAttribute
{
    public NeedsAudioDeviceFactAttribute()
    {
        if (!Probes.Audio.Value) Skip = "No audio device opens here.";
    }
}

/// <summary>Skipped where SDL opens an audio device, for what is checked only without one.</summary>
public sealed class NeedsNoAudioDeviceFactAttribute : FactAttribute
{
    public NeedsNoAudioDeviceFactAttribute()
    {
        if (Probes.Audio.Value) Skip = "An audio device opens here, so the backend's behavior without one cannot be reached.";
    }
}

internal static class Probes
{
    public static readonly Lazy<bool> Vulkan = new(() =>
    {
        try
        {
            using var device = new GraphicsDevice();
            device.Initialize(new OffscreenSurface(1, 1), "probe");
            return device.IsInitialized;
        }
        catch (Exception)
        {
            return false;
        }
    });

    public static readonly Lazy<bool> Audio = new(() =>
    {
        using var backend = new SdlAudioBackend();
        backend.Initialize();
        return backend.IsInitialized;
    });
}
