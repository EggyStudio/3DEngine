using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Whether a file or a folder is the user's own and closed to others' writes, so a program runs
/// no program and writes into no folder another user set in its way.
/// </summary>
/// <remarks>
/// On Unix a file's owner is read through <c>statx</c> on Linux and <c>stat</c> on macOS, each
/// through a layout the platform holds fixed, and its mode through the base library. A file on
/// Windows carries no Unix owner, so every file there is taken as the user's.
/// </remarks>
internal static unsafe partial class OwnFiles
{
    /// <summary>The user id the program runs as on Unix, 0 for the superuser, or none on Windows.</summary>
    public static uint? UserId => OperatingSystem.IsWindows() ? null : EffectiveUser();

    /// <summary>Whether <paramref name="path"/>, followed through a link, is owned by the user the program runs as.</summary>
    public static bool IsOwn(string path) =>
        OperatingSystem.IsWindows() || OwnerOf(path) is { } owner && owner == EffectiveUser();

    /// <summary>Whether <paramref name="path"/> is the user's own and neither its group nor others may write it.</summary>
    public static bool IsOwnAndClosed(string path) =>
        OperatingSystem.IsWindows() || IsOwn(path) && (File.GetUnixFileMode(path) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0;

    // The user id that owns the file, or none where it cannot be read. statx's own layout holds that
    // id 20 bytes in on every Linux, and macOS's stat with 64-bit inodes 16 bytes in.
    private static uint? OwnerOf(string path)
    {
        var buffer = stackalloc byte[512];
        if (OperatingSystem.IsLinux())
            return Statx(AtCurrentFolder, path, 0, StatxUid, buffer) == 0 ? *(uint*)(buffer + 20) : null;
        if (OperatingSystem.IsMacOS())
            return (RuntimeInformation.ProcessArchitecture == Architecture.X64 ? StatInode64(path, buffer) : Stat(path, buffer)) == 0 ? *(uint*)(buffer + 16) : null;
        return null;
    }

    // A relative path read from the current folder, and the bit of statx's mask that fills in the user id.
    private const int AtCurrentFolder = -100;
    private const uint StatxUid = 0x8;

    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Statx(int folder, string path, int flags, uint mask, byte* buffer);

    [LibraryImport("libc", EntryPoint = "stat", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Stat(string path, byte* buffer);

    [LibraryImport("libc", EntryPoint = "stat$INODE64", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int StatInode64(string path, byte* buffer);

    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint EffectiveUser();
}
