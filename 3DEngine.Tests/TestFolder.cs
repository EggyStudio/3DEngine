using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Engine.Tests;

/// <summary>
/// A folder a test class writes its files into, made when the class is and deleted when it is
/// disposed.
/// </summary>
/// <remarks>
/// <para>
/// The test class holds it and disposes it, rather than a test deleting its folder in a
/// <c>finally</c>, because xUnit reports what a class's <c>Dispose</c> throws beside the test's own
/// failure, where an exception from a <c>finally</c> takes the place of the one the test threw and
/// hides whether its checks passed.
/// </para>
/// <para>
/// Windows refuses to delete a file that is open, and a delete is tried again for two seconds,
/// for a handle a moment from being closed. A file still held then is named with the processes
/// holding it, found through the Restart Manager, so a run that fails here says where to look. A
/// test's own process among them is a handle the engine kept, and another is a child that
/// inherited one. Linux and macOS delete an open file and never get that far, which
/// <c>FileHandleTests</c> covers by looking for the handle itself.
/// </para>
/// </remarks>
internal sealed class TestFolder : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>Makes a new folder under the system's temporary folder, its name starting with <paramref name="prefix"/>.</summary>
    public TestFolder(string prefix) => Path = Directory.CreateTempSubdirectory(prefix).FullName;

    private TestFolder(string path, bool _) => Path = Directory.CreateDirectory(path).FullName;

    /// <summary>
    /// Makes a folder at <paramref name="path"/>, as one beside the test program for a test of
    /// where the engine looks for a file.
    /// </summary>
    public static TestFolder At(string path) => new(path, true);

    /// <summary>The path of <paramref name="name"/> in the folder.</summary>
    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <inheritdoc />
    public void Dispose() => Delete(Path);

    /// <summary>
    /// Deletes a folder and everything in it, trying again for two seconds while a file is held,
    /// and then throwing with the holders' names.
    /// </summary>
    public static void Delete(string path)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (waited.Elapsed < Patience)
                {
                    Thread.Sleep(50);
                    continue;
                }
                throw new IOException($"{ex.Message} {Holders(path)}", ex);
            }
        }
    }

    // Which processes hold the files left in the folder, as a sentence.
    private static string Holders(string path)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "The files left could not be listed.";
        }

        if (files.Length == 0) return "No file is left in it.";
        if (!OperatingSystem.IsWindows()) return $"{files.Length} files are left in it.";

        var held = new List<string>();
        foreach (var file in files)
        {
            var holders = RestartManager.Holders(file);
            if (holders.Count > 0)
                held.Add($"{System.IO.Path.GetFileName(file)} is held by {string.Join(", ", holders)}");
        }
        return held.Count == 0
            ? $"{files.Length} files are left in it, and the Restart Manager names no process holding one."
            : string.Join(". ", held) + ".";
    }

    // The Restart Manager's list of the processes that have a file open, the list Windows itself
    // asks for when an installer finds a file in use.
    private static class RestartManager
    {
        private const int MoreData = 234;

        public static List<string> Holders(string file)
        {
            var names = new List<string>();
            var key = new StringBuilder(33);
            if (RmStartSession(out var session, 0, key) != 0) return ["(the Restart Manager would not start)"];
            try
            {
                if (RmRegisterResources(session, 1, [file], 0, null, 0, null) != 0) return names;

                uint reasons = 0;
                uint count = 0;
                var result = RmGetList(session, out var needed, ref count, null, ref reasons);
                if (result == MoreData && needed > 0)
                {
                    var processes = new ProcessInfo[needed];
                    count = needed;
                    result = RmGetList(session, out needed, ref count, processes, ref reasons);
                    if (result == 0)
                        for (int i = 0; i < count; i++)
                            names.Add($"{processes[i].AppName} ({processes[i].Process.ProcessId}"
                                + (processes[i].Process.ProcessId == Environment.ProcessId ? ", the test's own process)" : ")"));
                }
                return names;
            }
            finally
            {
                RmEndSession(session);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UniqueProcess
        {
            public int ProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessInfo
        {
            public UniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceShortName;
            public int ApplicationType;
            public uint AppStatus;
            public uint TerminalServicesSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint session, int flags, StringBuilder key);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint session);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint session, uint fileCount, string[] files,
            uint applicationCount, UniqueProcess[]? applications, uint serviceCount, string[]? services);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint session, out uint needed, ref uint count,
            [In, Out] ProcessInfo[]? processes, ref uint rebootReasons);
    }
}
