using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Engine;

/// <summary>
/// Cross-platform utility for discovering, pre-loading, and resolving native shared
/// libraries (<c>.so</c> / <c>.dll</c> / <c>.dylib</c>).
/// <para>
/// Supports ordered multi-directory search, DllImport resolver registration,
/// automatic platform name conversion, and Linux compatibility-shim creation.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var loader = new NativeLibraryLoader()
///     .AddSearchPath(AppContext.BaseDirectory)
///     .AddRuntimeNativePath()
///     .MapName("MyLib", "libMyLib.so")
///     .OnLog(Console.WriteLine)
///     .OnWarn(Console.Error.WriteLine);
///
/// loader.PreloadAll("libDep.so", "libMyLib.so");
/// loader.RegisterDllImportResolver(typeof(MyNativeWrapper).Assembly);
/// </code>
/// </example>
public sealed class NativeLibraryLoader
{
    private readonly List<string> _searchPaths = [];
    private readonly Dictionary<string, string> _nameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, nint> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Assembly> _resolverAssemblies = [];
    private bool _globalFallbackRegistered;
    private Action<string>? _onLog;
    private Action<string>? _onWarn;

    // -- Platform detection --

    /// <summary>OS identifier: <c>"win"</c>, <c>"linux"</c>, or <c>"osx"</c>.</summary>
    public static string PlatformName { get; } =
        OperatingSystem.IsWindows() ? "win" :
        OperatingSystem.IsLinux() ? "linux" :
        OperatingSystem.IsMacOS() ? "osx" :
        "unknown";

    /// <summary>CPU architecture: <c>"x64"</c>, <c>"arm64"</c>, <c>"x86"</c>, or <c>"arm"</c>.</summary>
    public static string ArchitectureName { get; } =
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64   => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86   => "x86",
            Architecture.Arm   => "arm",
            _                  => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

    /// <summary>NuGet-style runtime identifier, e.g. <c>"linux-x64"</c>, <c>"win-x64"</c>, <c>"osx-arm64"</c>.</summary>
    public static string RuntimeIdentifier { get; } = $"{PlatformName}-{ArchitectureName}";

    /// <summary>Platform native library extension including the leading dot.</summary>
    public static string NativeExtension { get; } =
        OperatingSystem.IsWindows() ? ".dll" :
        OperatingSystem.IsMacOS() ? ".dylib" : ".so";

    /// <summary>Platform native library prefix (<c>"lib"</c> on Linux/macOS, empty on Windows).</summary>
    public static string NativePrefix { get; } =
        OperatingSystem.IsWindows() ? "" : "lib";

    // -- Configuration (fluent) --

    /// <summary>
    /// Adds a directory to the search list. Directories are probed in the order they
    /// are added. Duplicate or blank paths are silently ignored.
    /// </summary>
    public NativeLibraryLoader AddSearchPath(string directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath) && !_searchPaths.Contains(directoryPath))
            _searchPaths.Add(directoryPath);
        return this;
    }

    /// <summary>
    /// Adds the NuGet-convention <c>runtimes/{rid}/native/</c> directory, resolved
    /// relative to <paramref name="baseDirectory"/> (defaults to <see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    public NativeLibraryLoader AddRuntimeNativePath(string? baseDirectory = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        return AddSearchPath(Path.Combine(baseDirectory, "runtimes", RuntimeIdentifier, "native"));
    }

    /// <summary>
    /// Maps a logical DllImport name (e.g. <c>"AppCore"</c>) to a platform-specific
    /// file name (e.g. <c>"libAppCore.so"</c>). Used during <see cref="ResolveDllImport"/>.
    /// </summary>
    public NativeLibraryLoader MapName(string dllImportName, string platformFileName)
    {
        _nameMap[dllImportName] = platformFileName;
        return this;
    }

    /// <summary>
    /// Batch version of <see cref="MapName"/>. A <c>null</c> platform name causes
    /// automatic conversion via <see cref="ToPlatformFileName"/>.
    /// </summary>
    public NativeLibraryLoader MapNames(params (string dllImportName, string? platformFileName)[] mappings)
    {
        foreach (var (name, file) in mappings)
            _nameMap[name] = file ?? ToPlatformFileName(name);
        return this;
    }

    /// <summary>Sets a callback for informational (debug-level) messages.</summary>
    public NativeLibraryLoader OnLog(Action<string> logAction)
    {
        _onLog = logAction;
        return this;
    }

    /// <summary>Sets a callback for warning-level messages.</summary>
    public NativeLibraryLoader OnWarn(Action<string> warnAction)
    {
        _onWarn = warnAction;
        return this;
    }

    // -- Loading --

    /// <summary>
    /// Searches all configured directories for <paramref name="fileName"/> and loads
    /// the first match. Returns the native handle, or <see cref="nint.Zero"/> on failure.
    /// Subsequent calls for the same <paramref name="fileName"/> return the cached handle.
    /// </summary>
    public nint Preload(string fileName)
    {
        if (_loaded.TryGetValue(fileName, out nint cached))
            return cached;

        foreach (string dir in _searchPaths)
        {
            string fullPath = Path.Combine(dir, fileName);
            if (!File.Exists(fullPath)) continue;

            if (NativeLibrary.TryLoad(fullPath, out nint handle))
            {
                _loaded[fileName] = handle;
                _onLog?.Invoke($"Loaded native library: {fullPath} (0x{handle:X})");
                return handle;
            }

            _onWarn?.Invoke($"Found but failed to load: {fullPath}");
        }

        _onWarn?.Invoke($"Native library not found in any search path: {fileName}");
        return nint.Zero;
    }

    /// <summary>
    /// Same as <see cref="Preload(string)"/> but loads the first matching file with
    /// <c>RTLD_GLOBAL</c> on Linux/macOS so its exported symbols are visible to
    /// subsequently loaded libraries (required when later libs depend on this one,
    /// e.g. <c>libWebCore.so</c> depends on symbols in <c>libUltralightCore.so</c>).
    /// On Windows this is identical to <see cref="Preload(string)"/> since the
    /// PE loader does not have a per-handle visibility flag.
    /// </summary>
    public nint PreloadGlobal(string fileName)
    {
        if (_loaded.TryGetValue(fileName, out nint cached))
            return cached;

        foreach (string dir in _searchPaths)
        {
            string fullPath = Path.Combine(dir, fileName);
            if (!File.Exists(fullPath)) continue;

            nint handle = TryLoadGlobal(fullPath);
            if (handle != nint.Zero)
            {
                _loaded[fileName] = handle;
                _onLog?.Invoke($"Loaded native library (GLOBAL): {fullPath} (0x{handle:X})");
                return handle;
            }

            _onWarn?.Invoke($"Found but failed to load (GLOBAL): {fullPath}");
        }

        _onWarn?.Invoke($"Native library not found in any search path: {fileName}");
        return nint.Zero;
    }

    /// <summary>
    /// Pre-loads multiple libraries with <c>RTLD_GLOBAL</c> in the given (dependency-first) order.
    /// </summary>
    public int PreloadAllGlobal(params string[] fileNames)
    {
        int count = 0;
        foreach (string name in fileNames)
        {
            if (PreloadGlobal(name) != nint.Zero)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Loads a native library from an explicit absolute path.
    /// Returns the handle or <see cref="nint.Zero"/> on failure.
    /// </summary>
    public nint PreloadFrom(string absolutePath)
    {
        string key = Path.GetFileName(absolutePath);
        if (_loaded.TryGetValue(key, out nint cached))
            return cached;

        if (!File.Exists(absolutePath))
        {
            _onWarn?.Invoke($"File not found: {absolutePath}");
            return nint.Zero;
        }

        if (NativeLibrary.TryLoad(absolutePath, out nint handle))
        {
            _loaded[key] = handle;
            _onLog?.Invoke($"Loaded native library: {absolutePath} (0x{handle:X})");
            return handle;
        }

        _onWarn?.Invoke($"Failed to load: {absolutePath}");
        return nint.Zero;
    }

    /// <summary>
    /// Tries a list of absolute <paramref name="candidates"/> and loads the first one
    /// that exists and can be opened. Ideal for locating system libraries whose exact
    /// path varies across distributions (e.g. <c>libbz2.so.1.0</c>).
    /// </summary>
    public nint PreloadFirstAvailable(string logicalName, params string[] candidates)
    {
        if (_loaded.TryGetValue(logicalName, out nint cached))
            return cached;

        foreach (string path in candidates)
        {
            if (!File.Exists(path)) continue;
            if (NativeLibrary.TryLoad(path, out nint handle))
            {
                _loaded[logicalName] = handle;
                _onLog?.Invoke($"Loaded {logicalName}: {path} (0x{handle:X})");
                return handle;
            }
        }

        _onWarn?.Invoke($"None of the candidates for '{logicalName}' could be loaded");
        return nint.Zero;
    }

    /// <summary>
    /// Pre-loads multiple libraries in the order given. Returns the number loaded
    /// successfully. Use dependency-first ordering so transitive deps are satisfied.
    /// </summary>
    public int PreloadAll(params string[] fileNames)
    {
        int count = 0;
        foreach (string name in fileNames)
        {
            if (Preload(name) != nint.Zero)
                count++;
        }
        return count;
    }

    /// <summary>Returns <c>true</c> if <paramref name="fileName"/> has been loaded.</summary>
    public bool IsLoaded(string fileName) => _loaded.ContainsKey(fileName);

    /// <summary>
    /// Gets the handle of a previously loaded library,
    /// or <see cref="nint.Zero"/> if it was never loaded.
    /// </summary>
    public nint GetHandle(string fileName) => _loaded.GetValueOrDefault(fileName);

    // -- DllImport resolution --

    /// <summary>
    /// Registers this loader as the <c>[DllImport]</c> / <c>[LibraryImport]</c> resolver
    /// for <paramref name="assembly"/>. When P/Invoke fires, the loader's name mappings
    /// and search paths are used to locate the native library.
    /// </summary>
    public NativeLibraryLoader RegisterDllImportResolver(Assembly assembly)
    {
        if (_resolverAssemblies.Add(assembly))
            NativeLibrary.SetDllImportResolver(assembly, ResolveDllImport);
        return this;
    }

    /// <summary>
    /// Registers a global last-resort handler on the default <see cref="AssemblyLoadContext"/>.
    /// Catches P/Invoke failures from assemblies not covered by a per-assembly resolver.
    /// </summary>
    public NativeLibraryLoader RegisterGlobalFallback()
    {
        if (_globalFallbackRegistered) return this;
        _globalFallbackRegistered = true;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll +=
            (_, name) => ResolveDllImport(name, null!, null);
        return this;
    }

    private nint ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // 1. Map DllImport name → platform file name (explicit or auto-generated).
        string fileName = _nameMap.TryGetValue(libraryName, out string? mapped)
            ? mapped
            : ToPlatformFileName(libraryName);

        // 2. Return cached handle if already loaded.
        if (_loaded.TryGetValue(fileName, out nint handle))
            return handle;

        // 3. Probe search paths.
        handle = Preload(fileName);
        if (handle != nint.Zero)
            return handle;

        // 4. Fallback: try the raw DllImport name as-is in the search paths
        //    (handles cases like "libfoo.so.2" used directly in [DllImport]).
        if (fileName != libraryName)
        {
            foreach (string dir in _searchPaths)
            {
                string fullPath = Path.Combine(dir, libraryName);
                if (!File.Exists(fullPath)) continue;
                if (NativeLibrary.TryLoad(fullPath, out handle))
                {
                    _loaded[libraryName] = handle;
                    return handle;
                }
            }
        }

        return nint.Zero;
    }

    // -- Compatibility helpers --

    /// <summary>
    /// Creates a symbolic link at <paramref name="linkPath"/> pointing to the first
    /// existing file in <paramref name="candidates"/>. Linux / macOS only.
    /// <para>
    /// Use this to satisfy SONAME dependencies that differ across distributions,
    /// e.g. <c>libbz2.so.1.0</c> on a distro that only ships <c>libbz2.so.1.0.8</c>.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> if the link already exists or was successfully created.</returns>
    public bool EnsureSymlink(string linkPath, params string[] candidates)
    {
        if (OperatingSystem.IsWindows()) return false;

        // Already valid?
        if (File.Exists(linkPath)) return true;

        // Remove stale / broken symlink so CreateSymbolicLink doesn't throw EEXIST.
        try
        {
            var info = new FileInfo(linkPath);
            if (info.LinkTarget is not null)
                info.Delete();
        }
        catch { /* ignore */ }

        // Ensure parent directory exists.
        string? dir = Path.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch { return false; }
        }

        foreach (string candidate in candidates)
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                File.CreateSymbolicLink(linkPath, candidate);
                _onLog?.Invoke($"Created symlink: {linkPath} → {candidate}");
                return true;
            }
            catch (Exception ex)
            {
                _onWarn?.Invoke($"Failed to create symlink {linkPath} → {candidate}: {ex.Message}");
            }
        }

        _onWarn?.Invoke($"Could not create symlink - no candidates found for: {linkPath}");
        return false;
    }

    /// <summary>Ensures a directory exists, creating it if necessary.</summary>
    public static bool EnsureDirectory(string path)
    {
        if (Directory.Exists(path)) return true;
        try { Directory.CreateDirectory(path); return true; }
        catch { return false; }
    }

    // -- Static helpers --

    // RTLD_LAZY = 1, RTLD_GLOBAL = 0x100 on glibc / musl / macOS.
    private const int RTLD_LAZY = 1;
    private const int RTLD_GLOBAL = 0x100;

    [System.Runtime.InteropServices.DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern nint dlopen_glibc(string filename, int flags);

    [System.Runtime.InteropServices.DllImport("libdl.so", EntryPoint = "dlopen")]
    private static extern nint dlopen_musl(string filename, int flags);

    [System.Runtime.InteropServices.DllImport("libSystem.dylib", EntryPoint = "dlopen")]
    private static extern nint dlopen_macos(string filename, int flags);

    /// <summary>
    /// Loads <paramref name="absolutePath"/> with global symbol visibility
    /// (so subsequently loaded shared objects can resolve symbols against it).
    /// On Windows this falls back to <see cref="NativeLibrary.TryLoad(string, out nint)"/>.
    /// </summary>
    private static nint TryLoadGlobal(string absolutePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return NativeLibrary.TryLoad(absolutePath, out nint h) ? h : nint.Zero;
        }

        if (OperatingSystem.IsMacOS())
        {
            try { return dlopen_macos(absolutePath, RTLD_LAZY | RTLD_GLOBAL); }
            catch { return nint.Zero; }
        }

        // Linux: try glibc soname first, fall back to musl.
        try
        {
            nint h = dlopen_glibc(absolutePath, RTLD_LAZY | RTLD_GLOBAL);
            if (h != nint.Zero) return h;
        }
        catch { /* libdl.so.2 not present (musl) */ }

        try { return dlopen_musl(absolutePath, RTLD_LAZY | RTLD_GLOBAL); }
        catch { return nint.Zero; }
    }

    /// <summary>
    /// Converts a logical library name (e.g. <c>"AppCore"</c>) to its platform-specific
    /// file name (e.g. <c>"libAppCore.so"</c>, <c>"AppCore.dll"</c>, <c>"libAppCore.dylib"</c>).
    /// Names that already carry a native extension are returned unchanged.
    /// </summary>
    public static string ToPlatformFileName(string baseName)
    {
        // Already has a platform extension → return as-is.
        if (baseName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            baseName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
            baseName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
            return baseName;

        // On Linux/macOS a name starting with "lib" just needs the extension.
        if (!OperatingSystem.IsWindows() && baseName.StartsWith("lib", StringComparison.Ordinal))
            return baseName + NativeExtension;

        return NativePrefix + baseName + NativeExtension;
    }
}
