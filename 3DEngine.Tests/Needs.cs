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

/// <summary>A theory skipped as <see cref="NeedsVulkanFactAttribute"/> is, where no Vulkan device starts.</summary>
public sealed class NeedsVulkanTheoryAttribute : TheoryAttribute
{
    public NeedsVulkanTheoryAttribute()
    {
        if (new NeedsVulkanFactAttribute().Skip is { } reason) Skip = reason;
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

/// <summary>
/// A theory skipped where the files the process has open cannot be told. Linux lists them under
/// <c>/proc/self/fd</c> and Windows refuses a file open elsewhere to one opened alone, and macOS
/// does neither.
/// </summary>
public sealed class NeedsOpenFilesTheoryAttribute : TheoryAttribute
{
    public NeedsOpenFilesTheoryAttribute()
    {
        if (NeedsOpenFilesFactAttribute.Missing() is { } reason) Skip = reason;
    }
}

/// <summary>Skipped as <see cref="NeedsOpenFilesTheoryAttribute"/> is, and where <c>slangc</c> is not found when the constructor is told it is needed.</summary>
public sealed class NeedsOpenFilesFactAttribute : FactAttribute
{
    public NeedsOpenFilesFactAttribute(bool slang = false)
    {
        if (Missing() is { } reason) Skip = reason;
        else if (slang && !SlangCompiler.Available) Skip = "slangc was not found. Run build/fetch-slang.sh or set ENGINE_SLANGC.";
    }

    internal static string? Missing() =>
        OperatingSystem.IsWindows() || Directory.Exists("/proc/self/fd")
            ? null
            : "This system neither lists a process's open files under /proc/self/fd nor refuses an open file to one opened alone.";
}

/// <summary>
/// Skipped where the checkout does not hold the commit N 7.2 reads the messages from, as a copy of
/// the files alone or a checkout of the last commit only, which the workflow makes.
/// </summary>
public sealed class NeedsHistoryFactAttribute : FactAttribute
{
    public NeedsHistoryFactAttribute()
    {
        try
        {
            NormTests.Git("rev-parse", "--verify", "--quiet", NormTests.MessagesFrom + "^{commit}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Skip = $"This checkout does not hold commit {NormTests.MessagesFrom}, from which N 7.2 reads the messages, or git is not on PATH.";
        }
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

    // The name Python runs by, python3 where it is, as on Linux and macOS, and python on Windows.
    public static readonly Lazy<string?> Python = new(() =>
    {
        foreach (var name in new[] { "python3", "python" })
        {
            try
            {
                using var python = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(name, "--version") { RedirectStandardOutput = true, RedirectStandardError = true });
                if (python is null) continue;
                python.WaitForExit(10_000);
                if (python.HasExited && python.ExitCode == 0) return name;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Not on PATH.
            }
        }
        return null;
    });

    public static readonly Lazy<bool> Audio = new(() =>
    {
        using var backend = new SdlAudioBackend();
        backend.Initialize();
        return backend.IsInitialized;
    });
}

/// <summary>Skipped where build/package holds no engine package, unless <c>E3D_REQUIRE_PACKAGE</c> is set.</summary>
public sealed class NeedsPackageFactAttribute : FactAttribute
{
    public NeedsPackageFactAttribute()
    {
        // The pack workflow sets it after build/pack.sh, so a package that was not made fails there.
        if (Package.PackageContentsTests.Newest() is null && Environment.GetEnvironmentVariable("E3D_REQUIRE_PACKAGE") != "1")
            Skip = "build/package holds no 3DEngine package. build/pack.sh makes one.";
    }
}

/// <summary>Skipped where neither <c>python3</c> nor <c>python</c> runs, which the workflow's runners all have.</summary>
public sealed class NeedsPythonFactAttribute : FactAttribute
{
    public NeedsPythonFactAttribute()
    {
        if (Probes.Python.Value is null) Skip = "Neither python3 nor python runs here.";
    }
}

/// <summary>Skipped where neither <c>python3</c> nor <c>python</c> runs.</summary>
public sealed class NeedsPythonTheoryAttribute : TheoryAttribute
{
    public NeedsPythonTheoryAttribute()
    {
        if (Probes.Python.Value is null) Skip = "Neither python3 nor python runs here.";
    }
}
