using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Engine.Tests.Core;

/// <summary>
/// The process's private bytes, its heaps and its threads, read from Windows without allocating
/// anything of the collector's, since a leak test reads them between two steps of an app, so a leak
/// found on a machine no one here has says where it lies (REVIEW.md, Verdict 64).
/// </summary>
internal static class ProcessMemory
{
    // The private bytes the process has committed, its commit charge, in megabytes, asked of the
    // system directly, where Process's figure is read from a snapshot of every process on the machine.
    public static double PrivateBytes()
    {
        var counters = new MemoryCounters { Size = (uint)Marshal.SizeOf<MemoryCounters>() };
        return GetProcessMemoryInfo(new IntPtr(-1), ref counters, counters.Size) ? counters.PrivateUsage / 1e6 : 0;
    }

    // PROCESS_MEMORY_COUNTERS_EX, whose last field is the private bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryCounters
    {
        public uint Size, PageFaultCount;
        public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage,
            QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);

    // On Windows, what the process's heaps hold allocated of what they have committed, in MB, and
    // how many heaps there are, as a reading's text, and nothing elsewhere. Every native library's
    // malloc takes from them, the C runtime's, SDL's, the loader's and lavapipe's, and the collector
    // and the code LLVM compiles do not. So a climb they hold allocated is blocks no one freed, one
    // they hold committed and not allocated is freed memory a heap keeps, and one outside them is
    // memory taken from the system directly, such as compiled code or a thread's stack.
    public static string Heaps()
    {
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            var heaps = new IntPtr[256];
            var count = (int)GetProcessHeaps((uint)heaps.Length, heaps);
            double allocated = 0, committed = 0;
            for (int h = 0; h < Math.Min(count, heaps.Length); h++)
            {
                var summary = new HeapSummaryData { Size = (uint)Marshal.SizeOf<HeapSummaryData>() };
                if (!HeapSummary(heaps[h], 0, ref summary)) continue;
                allocated += summary.Allocated / 1e6;
                committed += summary.Committed / 1e6;
            }
            return $", heaps {allocated:0.0} of {committed:0.0} in {count}";
        }
        catch (EntryPointNotFoundException)
        {
            return ", heaps not read";
        }
    }

    // HEAP_SUMMARY.
    [StructLayout(LayoutKind.Sequential)]
    private struct HeapSummaryData
    {
        public uint Size;
        public nuint Allocated, Committed, Reserved, MaxReserve;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetProcessHeaps(uint count, [Out] IntPtr[] heaps);

    [DllImport("kernel32.dll")]
    private static extern bool HeapSummary(IntPtr heap, uint flags, ref HeapSummaryData summary);

    // The process's threads. On Windows they are counted from a snapshot of the system's threads,
    // which takes nothing of the collector's, since they are counted between two readings of an
    // app's memory, where Process reads every process on the machine into objects.
    public static int Threads()
    {
        if (!OperatingSystem.IsWindows())
        {
            using var process = Process.GetCurrentProcess();
            return process.Threads.Count;
        }
        var snapshot = CreateToolhelp32Snapshot(SnapshotThreads, 0);
        if (snapshot == new IntPtr(-1)) return -1;
        try
        {
            var entry = new ThreadEntry { Size = (uint)Marshal.SizeOf<ThreadEntry>() };
            var count = 0;
            for (var more = Thread32First(snapshot, ref entry); more; more = Thread32Next(snapshot, ref entry))
                if (entry.OwnerProcessId == (uint)Environment.ProcessId) count++;
            return count;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    // On Linux, the process's threads by their id with the name each was given, read from
    // /proc/self/task, so a step that starts or ends threads says whose they are, and empty
    // elsewhere, where Windows' Thread handles name them (HandleCensus).
    public static Dictionary<int, string> ThreadNames()
    {
        var names = new Dictionary<int, string>();
        if (!OperatingSystem.IsLinux()) return names;
        try
        {
            foreach (var task in Directory.GetDirectories("/proc/self/task"))
                if (int.TryParse(Path.GetFileName(task), out var id))
                    try { names[id] = File.ReadAllText(Path.Combine(task, "comm")).TrimEnd('\n'); }
                    catch (IOException) { }
        }
        catch (IOException)
        {
        }
        return names;
    }

    private const uint SnapshotThreads = 0x4;

    // THREADENTRY32.
    [StructLayout(LayoutKind.Sequential)]
    private struct ThreadEntry
    {
        public uint Size, Usage, ThreadId, OwnerProcessId;
        public int BasePriority, DeltaPriority;
        public uint Flags;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);

    [DllImport("kernel32.dll")]
    private static extern bool Thread32First(IntPtr snapshot, ref ThreadEntry entry);

    [DllImport("kernel32.dll")]
    private static extern bool Thread32Next(IntPtr snapshot, ref ThreadEntry entry);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>
/// The process's committed memory on 64-bit Windows by the kind of region holding it, private,
/// mapped or a module's image, and by whether it is code, which may be executed, a thread's stack,
/// a private allocation holding a guard page, or data, with the regions of each, swept with
/// VirtualQuery. Elsewhere it reads nothing.
/// </summary>
/// <remarks>
/// On Windows each drawing app kept some 1 MB that no heap and not the collector held (REVIEW.md,
/// Verdict 64), and the kind that grows names its owner. The code LLVM compiles for lavapipe's
/// shaders is private code, the runtime's compiled code is mapped code, since it maps each page
/// twice so none is writable and executable at once, a thread's stack is a stack, and what LLVM or
/// the runtime allocates for itself outside a heap is private data.
/// </remarks>
internal sealed class ProcessRegions
{
    private const uint Commit = 0x1000, Private = 0x20000, Mapped = 0x40000, Image = 0x1000000, Guard = 0x100, Executable = 0xF0;

    private readonly Dictionary<string, (double Megabytes, int Count)> _kinds = new(StringComparer.Ordinal);

    public static ProcessRegions? Sweep()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) return null;
        var swept = new ProcessRegions();
        foreach (var (region, name) in Committed())
        {
            var (megabytes, count) = swept._kinds.GetValueOrDefault(name);
            swept._kinds[name] = (megabytes + region.RegionSize / 1e6, count + 1);
        }
        return swept;
    }

    // On 64-bit Windows, the private data committed in each allocation, in kilobytes by the
    // allocation's base, an allocation being the range one VirtualAlloc reserved, and null
    // elsewhere. The collector's regions lie in one reservation made as the process began.
    public static Dictionary<IntPtr, double>? PrivateData()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) return null;
        var data = new Dictionary<IntPtr, double>();
        foreach (var (region, name) in Committed())
            if (name == "private data") data[region.AllocationBase] = data.GetValueOrDefault(region.AllocationBase) + region.RegionSize / 1e3;
        return data;
    }

    // Every committed region of the address space with the name of its kind.
    private static IEnumerable<(MemoryBasicInformation Region, string Name)> Committed()
    {
        var regions = new List<MemoryBasicInformation>();
        var size = (nuint)Marshal.SizeOf<MemoryBasicInformation>();
        ulong address = 0;
        while (VirtualQuery(new IntPtr((long)address), out var region, size) != 0)
        {
            regions.Add(region);
            var next = (ulong)region.BaseAddress + region.RegionSize;
            if (next <= address) break;
            address = next;
        }
        var stacks = regions.Where(r => r.State == Commit && (r.Protect & Guard) != 0).Select(r => r.AllocationBase).ToHashSet();
        foreach (var region in regions.Where(r => r.State == Commit))
        {
            var kind = region.Type switch { Private => "private", Mapped => "mapped", Image => "image", _ => "other" };
            var what = (region.Protect & Executable) != 0 ? "code"
                : region.Type == Private && stacks.Contains(region.AllocationBase) ? "stacks" : "data";
            yield return (region, $"{kind} {what}");
        }
    }

    // Each kind's megabytes and regions then and now, the kind that grew most first.
    public string GrownSince(ProcessRegions before) =>
        string.Join(", ", _kinds.Keys.Union(before._kinds.Keys)
            .Select(name => (Name: name, Then: before._kinds.GetValueOrDefault(name), Now: _kinds.GetValueOrDefault(name)))
            .OrderByDescending(k => k.Now.Megabytes - k.Then.Megabytes)
            .Select(k => $"{k.Name} {k.Then.Megabytes:0.0} MB in {k.Then.Count} to {k.Now.Megabytes:0.0} in {k.Now.Count} " +
                         $"({k.Now.Megabytes - k.Then.Megabytes:+0.0;-0.0;0.0})"));

    // MEMORY_BASIC_INFORMATION.
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State, Protect, Type;
    }

    [DllImport("kernel32.dll")]
    private static extern nuint VirtualQuery(IntPtr address, out MemoryBasicInformation region, nuint length);
}
