using System.Runtime.InteropServices;

namespace Engine.Tests.Core;

/// <summary>
/// The process's handles by kind on 64-bit Windows, an event, a thread, a file or a section, read
/// from the system's table of every handle, so a leak test that finds handles kept on a machine no
/// one here has says what they are and not only how many. Elsewhere it reads nothing.
/// </summary>
/// <remarks>
/// The table is a snapshot of every process's handles, a few MB on a runner, each entry naming its
/// process, the handle and the index of its kind, and the kind's name is asked of one handle of
/// each, its own process's, which never waits as asking a pipe's name can. A handle closed between
/// the snapshot and the question leaves its kind named by its index.
/// </remarks>
internal static class HandleCensus
{
    private const int SystemExtendedHandleInformation = 64;
    private const int ObjectTypeInformation = 2;
    private const int InfoLengthMismatch = unchecked((int)0xC0000004);

    // The table begins with the count of its entries and a reserved word. An entry on a 64-bit
    // system is the object, the process, the handle, its access, a back trace's index, the kind's
    // index, its attributes and a reserved word.
    private const int EntriesAt = 16, EntrySize = 40, ProcessAt = 8, HandleAt = 16, KindAt = 30;

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returned);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryObject(IntPtr handle, int infoClass, IntPtr info, int length, out int returned);

    /// <summary>The process's handles by the name of their kind, none where the table cannot be read.</summary>
    public static Dictionary<string, int> Now()
    {
        var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) return kinds;
        var length = 1 << 22;
        var table = IntPtr.Zero;
        try
        {
            // The table grows between asking its size and reading it, so it is asked for again,
            // larger, a few times at most.
            for (int tries = 0; ; tries++)
            {
                table = Marshal.AllocHGlobal(length);
                var status = NtQuerySystemInformation(SystemExtendedHandleInformation, table, length, out var returned);
                if (status == 0) break;
                Marshal.FreeHGlobal(table);
                table = IntPtr.Zero;
                if (status != InfoLengthMismatch || tries == 6) return kinds;
                length = Math.Max(length * 2, returned + (1 << 16));
            }
            var count = Marshal.ReadInt64(table);
            if (count < 0 || EntriesAt + count * EntrySize > length) return kinds;
            var self = (long)Environment.ProcessId;
            var byKind = new Dictionary<int, (int Count, IntPtr Sample)>();
            for (long i = 0; i < count; i++)
            {
                var entry = IntPtr.Add(table, checked((int)(EntriesAt + i * EntrySize)));
                if (Marshal.ReadInt64(entry, ProcessAt) != self) continue;
                var kind = (ushort)Marshal.ReadInt16(entry, KindAt);
                var handle = Marshal.ReadIntPtr(entry, HandleAt);
                byKind[kind] = byKind.TryGetValue(kind, out var seen) ? (seen.Count + 1, seen.Sample) : (1, handle);
            }
            foreach (var (kind, (n, sample)) in byKind)
            {
                var name = NameOf(sample) ?? $"kind {kind}";
                kinds[name] = kinds.GetValueOrDefault(name) + n;
            }
        }
        finally
        {
            if (table != IntPtr.Zero) Marshal.FreeHGlobal(table);
        }
        return kinds;
    }

    /// <summary>The kinds whose counts differ between two readings, most changed first, as "+2 Event, -1 File".</summary>
    public static string Change(Dictionary<string, int> before, Dictionary<string, int> after) =>
        string.Join(", ", before.Keys.Union(after.Keys)
            .Select(kind => (Kind: kind, By: after.GetValueOrDefault(kind) - before.GetValueOrDefault(kind)))
            .Where(c => c.By != 0)
            .OrderByDescending(c => Math.Abs(c.By)).ThenBy(c => c.Kind, StringComparer.Ordinal)
            .Select(c => $"{c.By:+0;-0} {c.Kind}"));

    // The name of a handle's kind, which the type information begins with as a counted string:
    // its length in bytes, its room, and on a 64-bit system, after four bytes of padding, where
    // its characters are.
    private static string? NameOf(IntPtr handle)
    {
        const int room = 4096;
        var info = Marshal.AllocHGlobal(room);
        try
        {
            if (NtQueryObject(handle, ObjectTypeInformation, info, room, out _) != 0) return null;
            var bytes = (ushort)Marshal.ReadInt16(info);
            var characters = Marshal.ReadIntPtr(info, 8);
            return bytes == 0 || characters == IntPtr.Zero ? null : Marshal.PtrToStringUni(characters, bytes / 2);
        }
        finally
        {
            Marshal.FreeHGlobal(info);
        }
    }
}
