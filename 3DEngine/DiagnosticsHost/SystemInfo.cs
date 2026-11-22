using System.Runtime.InteropServices;
using System.Text;

namespace Engine;

/// <summary>
/// The machine a program runs on, its OS, its CPU's name and the memory it has, read once for the
/// process, which the debug overlay's right column says.
/// </summary>
/// <remarks>
/// The CPU's name is read where each OS keeps it, Linux's <c>/proc/cpuinfo</c>, the registry on
/// Windows and <c>sysctl</c> on macOS, and is "unknown" where it cannot be read. The memory is the
/// most the runtime may use, the machine's or a container's limit where one is set.
/// </remarks>
internal static partial class SystemInfo
{
    private static readonly Lazy<(string Os, string Cpu, long Memory)> Read = new(() =>
        (RuntimeInformation.OSDescription.Trim(), ReadCpu(), GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));

    /// <summary>The OS and its version, as the runtime describes it.</summary>
    public static string Os => Read.Value.Os;

    /// <summary>The CPU's name, or "unknown".</summary>
    public static string Cpu => Read.Value.Cpu;

    /// <summary>The bytes of memory the runtime may use.</summary>
    public static long Memory => Read.Value.Memory;

    private static string ReadCpu() =>
        CpuName() is { Length: > 0 } name ? string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)) : "unknown";

    private static string? CpuName()
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
                return File.ReadLines("/proc/cpuinfo").Where(line => line.StartsWith("model name", StringComparison.Ordinal))
                    .Select(line => line[(line.IndexOf(':') + 1)..].Trim()).FirstOrDefault();
            if (OperatingSystem.IsWindows())
                return Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string;
            if (OperatingSystem.IsMacOS()) return Sysctl("machdep.cpu.brand_string");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        return null;
    }

    // A string sysctl reads by name, asked once for its length and then for its text.
    private static unsafe string? Sysctl(string name)
    {
        nuint length = 0;
        if (SysctlByName(name, null, &length, null, 0) != 0 || length == 0) return null;
        var bytes = new byte[(int)length];
        fixed (byte* text = bytes)
            if (SysctlByName(name, text, &length, null, 0) != 0) return null;
        return Encoding.UTF8.GetString(bytes, 0, (int)length).TrimEnd('\0');
    }

    [LibraryImport("libc", EntryPoint = "sysctlbyname", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int SysctlByName(string name, byte* value, nuint* length, void* newValue, nuint newLength);
}
