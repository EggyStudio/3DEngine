using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests;

/// <summary>
/// The checks of the rules in <c>.github/NORM.md</c>, a test a rule named for its number, as
/// <c>N_1_3</c> is for N 1.3, each failing with a message that begins with the rule's number.
/// </summary>
/// <remarks>
/// <para>
/// A rule that code existing on the day it was written does not keep has a list,
/// <c>build/norm/&lt;number&gt;.txt</c>, a place a line, with a few words of why after <c> # </c>
/// where they help. The test fails for a place it finds that is not listed, and for a listed line
/// whose place keeps the rule or is gone, so a list only gets shorter, and a place on it is mended
/// when a batch next touches it.
/// </para>
/// <para>
/// The files are the repository's own, as Git lists them, those not yet added among them, so what
/// a build leaves under <c>bin</c> and <c>obj</c> and a session's own scratch are never read.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed partial class NormTests
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();

    private const string Blob = "https://github.com/EggyStudio/3DEngine/blob/main/";

    /// <summary>The last commit before the one that added these tests, which N 7.2 reads from.</summary>
    internal const string MessagesFrom = "bf1a559c";

    [Fact]
    public void N_1_1()
    {
        // The annex's namespaces, as its sentence beginning "Its namespaces are" names them.
        var annex = Section(Norm(), "## Annex A");
        var sentence = annex[annex.IndexOf("Its namespaces are", StringComparison.Ordinal)..];
        sentence = sentence[..sentence.IndexOf("\n\n", StringComparison.Ordinal)];
        var allowed = Ticked().Matches(sentence).Select(m => m.Groups["name"].Value).ToHashSet();
        allowed.Should().Contain("Engine", "N 1.1 reads the namespaces from Annex A");

        var found = typeof(App).Assembly.GetExportedTypes()
            .Select(t => t.Namespace ?? "(none)").Where(n => !allowed.Contains(n)).Distinct();

        Hold("1.1", found, "a namespace of the library that Annex A does not list");
    }

    [Fact]
    public void N_1_2()
    {
        var found = new List<string>();
        foreach (var file in Files("3DEngine/", ".cs"))
            foreach (Match type in TopLevelPublicType().Matches(Read(file)))
            {
                var name = type.Groups["name"].Value;
                var fileName = Path.GetFileName(file);
                if (fileName != name + ".cs" && !fileName.StartsWith(name + ".", StringComparison.Ordinal))
                    found.Add($"{file} {name}");
            }

        Hold("1.2", found, "a public type in a file not named for it");
    }

    [Fact]
    public void N_1_3()
    {
        var found = new[] { "3DEngine/", "3DEngine.Tests/", "3DEngine.Examples/" }
            .SelectMany(project => Files(project, ".cs"))
            .Where(file => File.ReadLines(Path.Combine(Root, file)).Count() > 800);

        Hold("1.3", found, "a file of more than 800 lines");
    }

    [Fact]
    public void N_1_4()
    {
        // A test file whose first folder is no top folder of the library, those at the test
        // project's root among them, which the tests share or which test the whole.
        var areas = Files("3DEngine/", "").Where(f => f.Count(c => c == '/') >= 2)
            .Select(f => f.Split('/')[1]).ToHashSet(StringComparer.Ordinal);
        var found = Files("3DEngine.Tests/", ".cs")
            .Where(file => file.Split('/') is var parts && (parts.Length < 3 || !areas.Contains(parts[1])));

        Hold("1.4", found, "a test outside the folder of the area it tests");
    }

    [Fact]
    public void N_1_5()
    {
        var table = Section(File.ReadAllText(Path.Combine(Root, "AGENTS.md")), "## Where things are");
        var rows = Regex.Matches(table, @"^\| `(?<path>[^`]+)`", RegexOptions.Multiline)
            .Select(m => m.Groups["path"].Value.TrimEnd('/')).ToList();
        // Each top folder of the repository, the hidden ones apart, and each top folder of the
        // library in place of the library's own. A project lives in a top folder, so the folder's
        // row is the project's.
        var wanted = Files("", "").Where(f => f.Contains('/') && !f.StartsWith('.') && !f.StartsWith("3DEngine/", StringComparison.Ordinal))
            .Select(f => f[..f.IndexOf('/')])
            .Concat(Files("3DEngine/", "").Where(f => f.Count(c => c == '/') >= 2).Select(f => f[..f.IndexOf('/', "3DEngine/".Length)]))
            .Distinct();
        // A folder is named by a row of its own or by a row for a folder within it.
        var found = wanted.Where(path => !rows.Any(row => row == path || row.StartsWith(path + "/", StringComparison.Ordinal)));

        Hold("1.5", found, "a top folder of the repository or of the library with no row in AGENTS.md's table of areas");
    }

    [Fact]
    public void N_2_8()
    {
        var design = Section(File.ReadAllText(Path.Combine(Root, ".github", "DESIGN.md")), "## 8.");
        var named = Regex.Matches(design, @"^\| (?<name>[^|`\s]+) \|", RegexOptions.Multiline)
            .Select(m => m.Groups["name"].Value).ToHashSet();
        var found = Regex.Matches(Read("3DEngine/3DEngine.csproj"), @"<PackageReference Include=""(?<name>[^""]+)""")
            .Select(m => m.Groups["name"].Value).Where(name => !named.Contains(name));

        Hold("2.8", found, "a package the library references that D 8's table does not list");
    }

    [Fact]
    public void N_2_10()
    {
        var handed = HandedToNativeCode();
        handed.Select(Name).Should().Contain(["Engine.GraphicsDevice.DebugCallback", "Engine.AssimpFiles+File.ReadInto", "Engine.AssimpFiles.OpenFile"],
            "one is marked to be called from native code, one is handed over as a delegate and one is reached through the binding's own");

        var found = handed
            .Select(method => (method, call: UnguardedCall(method)))
            .Where(entry => entry.call is not null)
            .Select(entry => $"{Name(entry.method)} calls {entry.call} outside a catch of every exception");

        Hold("2.10", found, "a method native code calls");
    }

    [Fact]
    public void N_3_3()
    {
        // A clock read or a wait in a test, by file and kind, the kind standing for every one of
        // it in the file.
        var found = Files("3DEngine.Tests/", ".cs").Where(file => file != "3DEngine.Tests/NormTests.cs")
            .SelectMany(file => Clock().Matches(Read(file)).Select(m => $"{file} {m.Value}"))
            .Distinct();

        Hold("3.3", found, "a test that waits on or reads the machine's clock");
    }

    [Fact]
    public void N_3_4()
    {
        var found = Files("3DEngine.Tests/", ".cs")
            .Where(file => file is not "3DEngine.Tests/TestFolder.cs" and not "3DEngine.Tests/NormTests.cs")
            .SelectMany(file => TemporaryFolder().Matches(Read(file)).Select(m => $"{file} {m.Value}"))
            .Distinct();

        Hold("3.4", found, "a temporary folder made or removed without the one helper");
    }

    [Fact]
    public void N_4_1()
    {
        var found = Files("", "").Where(Prose).Where(file => Read(file).Any(c => c is '\u2014' or '\u2013'));

        Hold("4.1", found, "a file with a dash STYLE.md forbids");
    }

    [Fact]
    public void N_4_2()
    {
        var readme = Read("README.md");
        var found = Files("docs/", ".md").Where(page => !readme.Contains("/blob/main/" + page, StringComparison.Ordinal));
        // Its prose, every line but its tables' rows, the gallery's pictures among them.
        var prose = readme.Split('\n').SkipLast(1).Count(line => !line.TrimStart().StartsWith('|'));
        if (prose > 320) found = found.Append($"README.md {prose} lines of prose");

        Hold("4.2", found, "a page of docs/ the README does not link, or a README over 320 lines of prose");
    }

    [Fact]
    public void N_4_7()
    {
        // Each line of the documents a game's author reads that names who decided, by file and line.
        var found = Files("docs/", ".md").Prepend("CHEATSHEET.md").Prepend("README.md")
            .SelectMany(file => Read(file).Split('\n').Select((line, index) => (Place: $"{file}:{index + 1}", Line: line)))
            .Where(line => NamesWhoDecided().IsMatch(line.Line))
            .Select(line => line.Place);

        Hold("4.7", found, "a line of a document a game's author reads that names who decided");
    }

    [Fact]
    public void N_4_5()
    {
        var found = new List<string>();
        foreach (var capture in Files(".github/assets/examples/", ".webp"))
        {
            var (width, height) = WebPSize(File.ReadAllBytes(Path.Combine(Root, capture)));
            if ((width, height) != (800, 450)) found.Add($"{Path.GetFileNameWithoutExtension(capture)} {width}x{height}");
        }

        // Each picture in the README's gallery opens the file of the program that drew it, the file
        // an example's class is written in or a game's Program.cs.
        var runs = Regex.Matches(Read("3DEngine.Examples/Program.cs"), @"\[""(?<name>[a-z0-9_]+)""\]\s*=\s*(?<class>\w+)\.Run")
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["class"].Value);
        foreach (Match picture in Regex.Matches(Read("README.md"), @"(?:<a href=""(?<link>[^""]+)"">)?<img src=""[^""]*/assets/examples/(?<name>[a-z0-9_]+)\.webp"""))
        {
            var (link, name) = (picture.Groups["link"].Value, picture.Groups["name"].Value);
            var file = link.StartsWith(Blob, StringComparison.Ordinal) ? link[Blob.Length..] : "";
            var opens = file.Length > 0 && File.Exists(Path.Combine(Root, file)) && (runs.TryGetValue(name, out var type)
                ? file.StartsWith("3DEngine.Examples/", StringComparison.Ordinal) && Regex.IsMatch(Read(file), $@"\bclass\s+{type}\b")
                : Regex.IsMatch(file, $@"^games/(?i:{name})/Program\.cs$"));
            if (!opens) found.Add($"README.md {name}");
        }

        Hold("4.5", found, "a capture not at raylib's window of 800 by 450, or a README picture that does not open its program");
    }

    // The width and height a WebP file's header gives, from whichever of its three kinds it is.
    private static (int Width, int Height) WebPSize(byte[] file) => System.Text.Encoding.ASCII.GetString(file, 12, 4) switch
    {
        "VP8X" => (1 + (file[24] | file[25] << 8 | file[26] << 16), 1 + (file[27] | file[28] << 8 | file[29] << 16)),
        "VP8L" => (1 + ((file[21] | file[22] << 8) & 0x3FFF), 1 + ((file[22] >> 6 | file[23] << 2 | file[24] << 10) & 0x3FFF)),
        "VP8 " => ((file[26] | file[27] << 8) & 0x3FFF, (file[28] | file[29] << 8) & 0x3FFF),
        _ => (0, 0),
    };

    [NeedsHistoryFact]
    public void N_7_2()
    {
        var log = Git("log", "--format=%x1e%H%x1f%s%x1f%b%x1f", "--name-only", $"{MessagesFrom}..HEAD");

        var found = new List<string>();
        foreach (var entry in log.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('\u001f');
            if (parts.Length < 4) continue;
            var (hash, subject, body) = (parts[0][..8], parts[1], parts[2].Trim());
            // The owner's setting of the version, whatever its message.
            if (parts[3].Split('\n', StringSplitOptions.RemoveEmptyEntries) is ["build/version.txt"]) continue;
            var sentence = body.Length > 0 && !body.Contains('\n') && body.EndsWith('.') && !Regex.IsMatch(body[..^1], @"[.!?] [A-Z]");
            if (subject != "\u200e \u200e \u200e" || !sentence) found.Add(hash);
        }

        Hold("7.2", found, "a message without the form COMMITS.md gives, three marks and one sentence");
    }

    /// <summary>
    /// Every test here is named for a rule NORM.md has, every rule its table calls listed for this
    /// engine, or checked by these tests, has its test, and every list has its test.
    /// </summary>
    [Fact]
    public void NormAndItsTestsAgree()
    {
        var norm = Norm();
        var rules = Regex.Matches(norm, @"\*\*(N \d+\.\d+) ").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var tests = typeof(NormTests).GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0)
            .Select(method => method.Name)
            .Where(name => name != nameof(NormAndItsTestsAgree))
            .ToHashSet(StringComparer.Ordinal);

        var problems = new List<string>();
        problems.AddRange(tests.Where(test => !rules.Contains(test.Replace("N_", "N ").Replace('_', '.'))).Select(test => $"{test} is named for no rule in NORM.md"));

        // This engine's column, the first after the rule's number.
        foreach (Match row in Regex.Matches(norm, @"^\| (N \d+\.\d+) \|([^|\n]*)\|", RegexOptions.Multiline))
        {
            var (rule, cell) = (row.Groups[1].Value, row.Groups[2].Value.Trim());
            var ours = cell.StartsWith("listed", StringComparison.Ordinal)
                || (cell.StartsWith("checked", StringComparison.Ordinal) && cell.Contains("NormTests", StringComparison.Ordinal));
            if (ours && !tests.Contains(rule.Replace(' ', '_').Replace('.', '_'))) problems.Add($"{rule} is {cell} in NORM.md and NormTests has no test for it");
        }

        var lists = Path.Combine(Root, "build", "norm");
        if (Directory.Exists(lists))
            foreach (var list in Directory.EnumerateFiles(lists, "*.txt"))
                if (!tests.Contains("N_" + Path.GetFileNameWithoutExtension(list).Replace('.', '_')))
                    problems.Add($"build/norm/{Path.GetFileName(list)} is the list of no test");

        Assert.True(problems.Count == 0, "NORM.md and NormTests disagree: " + string.Join("; ", problems));
    }

    // -- Native code

    // The engine's methods native code calls. Those marked to be called from it, those made into a
    // delegate of a type marked to be handed to it, and the engine's overrides of a binding's
    // virtual methods that the binding's own such methods reach, as an Assimp file system's are.
    private static List<MethodBase> HandedToNativeCode()
    {
        var engine = typeof(App).Assembly;
        var handed = new HashSet<MethodBase>();
        foreach (var method in Methods(engine))
        {
            if (method.IsDefined(typeof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute))) handed.Add(method);
            foreach (var target in DelegatesHandedOver(method))
                if (target.Module.Assembly == engine) handed.Add(target);
        }

        var bindings = engine.GetTypes()
            .SelectMany(type => Bases(type))
            .Select(type => type.Assembly)
            .Where(assembly => assembly != engine && assembly != typeof(object).Assembly)
            .ToHashSet();
        var reached = bindings.SelectMany(VirtualsReachedFromNativeCode).ToHashSet();
        foreach (var method in Methods(engine))
            if (method is MethodInfo { IsVirtual: true } info && reached.Contains(info.GetBaseDefinition()))
                handed.Add(method);

        return [.. handed.OrderBy(Name, StringComparer.Ordinal)];
    }

    // The virtual methods a binding's methods that native code calls reach, through the
    // binding's own calls, which are followed and others' are not.
    private static IEnumerable<MethodBase> VirtualsReachedFromNativeCode(Assembly binding)
    {
        var pending = new Stack<MethodBase>(Methods(binding).SelectMany(DelegatesHandedOver).Where(m => m.Module.Assembly == binding));
        var seen = new HashSet<MethodBase>();
        while (pending.TryPop(out var method))
        {
            if (!seen.Add(method)) continue;
            foreach (var (_, code, operand) in Instructions(method))
                if ((code == OpCodes.Call || code == OpCodes.Callvirt) && Resolve(method, operand) is { } called && called.Module.Assembly == binding)
                {
                    if (called.IsVirtual) yield return called is MethodInfo info ? info.GetBaseDefinition() : called;
                    pending.Push(called);
                }
        }
    }

    // The methods a method makes into a delegate of a type marked to be handed to native code.
    private static IEnumerable<MethodBase> DelegatesHandedOver(MethodBase method)
    {
        MethodBase? pointed = null;
        foreach (var (_, code, operand) in Instructions(method))
        {
            if (code == OpCodes.Ldftn || code == OpCodes.Ldvirtftn)
                pointed = Resolve(method, operand);
            else if (code == OpCodes.Newobj && pointed is not null
                     && Resolve(method, operand)?.DeclaringType?.IsDefined(typeof(System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute)) == true)
                yield return pointed;
            else if (code != OpCodes.Dup && code != OpCodes.Ldarg_0 && code != OpCodes.Ldnull)
                pointed = null;
        }
    }

    // The first call, allocation or throw of a method that no catch of every exception covers,
    // or null where there is none. What a catch does to answer native code is read by review.
    private static string? UnguardedCall(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null) return null;
        var guarded = body.ExceptionHandlingClauses
            .Where(c => c.Flags == ExceptionHandlingClauseOptions.Clause && (c.CatchType == typeof(Exception) || c.CatchType == typeof(object)))
            .SelectMany(c => new[] { (c.TryOffset, c.TryOffset + c.TryLength), (c.HandlerOffset, c.HandlerOffset + c.HandlerLength) })
            .ToList();
        foreach (var (offset, code, operand) in Instructions(method))
        {
            if (!Throwing.Contains(code) || guarded.Any(range => offset >= range.Item1 && offset < range.Item2)) continue;
            return code == OpCodes.Throw ? "throw" : Resolve(method, operand) is { } called ? Name(called) : code.Name;
        }
        return null;
    }

    private static readonly HashSet<OpCode> Throwing =
        [OpCodes.Call, OpCodes.Callvirt, OpCodes.Calli, OpCodes.Newobj, OpCodes.Newarr, OpCodes.Throw, OpCodes.Castclass, OpCodes.Unbox, OpCodes.Unbox_Any];

    private static IEnumerable<MethodBase> Methods(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.OfType<Type>()];
        }
        return types.SelectMany(type => type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)));
    }

    private static IEnumerable<Type> Bases(Type type)
    {
        for (var at = type.BaseType; at is not null; at = at.BaseType) yield return at;
    }

    private static MethodBase? Resolve(MethodBase within, int token)
    {
        try
        {
            return within.Module.ResolveMethod(token,
                within.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null,
                within.IsGenericMethod ? within.GetGenericArguments() : null);
        }
        catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or TypeLoadException or FileNotFoundException)
        {
            return null;
        }
    }

    private static string Name(MethodBase method) => $"{method.DeclaringType?.FullName}.{method.Name}";

    // A method's instructions, each with where it starts and its operand when that is a token.
    private static IEnumerable<(int Offset, OpCode Code, int Operand)> Instructions(MethodBase method)
    {
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or BadImageFormatException)
        {
            il = null;
        }
        if (il is null) yield break;
        for (int at = 0; at < il.Length;)
        {
            var offset = at;
            var code = il[at] == 0xFE ? TwoByteCodes[il[at + 1]] : OneByteCodes[il[at]];
            at += code.Size;
            var operand = 0;
            switch (code.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    at += 1;
                    break;
                case OperandType.InlineVar:
                    at += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    at += 8;
                    break;
                case OperandType.InlineSwitch:
                    at += 4 + 4 * BitConverter.ToInt32(il, at);
                    break;
                default:
                    operand = BitConverter.ToInt32(il, at);
                    at += 4;
                    break;
            }
            yield return (offset, code, operand);
        }
    }

    private static readonly OpCode[] OneByteCodes = Codes(twoBytes: false);
    private static readonly OpCode[] TwoByteCodes = Codes(twoBytes: true);

    private static OpCode[] Codes(bool twoBytes)
    {
        var codes = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode code && (code.Size == 2) == twoBytes)
                codes[(ushort)code.Value & 0xFF] = code;
        return codes;
    }

    // -- The lists

    /// <summary>
    /// Fails for a place that breaks the rule and is not on its list, and for a listed place that
    /// keeps the rule now or is gone, each named, the message beginning with the rule's number.
    /// </summary>
    private static void Hold(string number, IEnumerable<string> breaking, string what)
    {
        var found = breaking.ToHashSet(StringComparer.Ordinal);
        var listed = ReadList(number);
        var unlisted = found.Where(place => !listed.Contains(place)).Order(StringComparer.Ordinal).ToList();
        var mended = listed.Where(place => !found.Contains(place)).Order(StringComparer.Ordinal).ToList();

        var message = $"N {number}: ";
        if (unlisted.Count > 0) message += $"breaking the rule, {what}, and not on build/norm/{number}.txt:\n  {string.Join("\n  ", unlisted)}\n";
        if (mended.Count > 0) message += $"on build/norm/{number}.txt and keeping the rule now, or gone, so off the list they come:\n  {string.Join("\n  ", mended)}\n";
        Assert.True(unlisted.Count == 0 && mended.Count == 0, message);
    }

    /// <summary>A rule's list, a place a line, the place before a tab where a reason follows.</summary>
    private static HashSet<string> ReadList(string number)
    {
        var path = Path.Combine(Root, "build", "norm", number + ".txt");
        if (!File.Exists(path)) return [];
        return File.ReadLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t')[0])
            .ToHashSet(StringComparer.Ordinal);
    }

    // -- The repository

    private static string Norm() => File.ReadAllText(Path.Combine(Root, ".github", "NORM.md"));

    private static string Read(string file) => File.ReadAllText(Path.Combine(Root, file));

    // A Markdown section from its heading to the next heading of its level or above.
    private static string Section(string text, string heading)
    {
        var start = text.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0) return "";
        var level = heading.TakeWhile(c => c == '#').Count();
        var next = Regex.Match(text[(start + heading.Length)..], $@"^#{{1,{level}}} ", RegexOptions.Multiline);
        return next.Success ? text.Substring(start, heading.Length + next.Index) : text[start..];
    }

    // The files of the repository under a folder, with an extension, by path from the root with
    // forward slashes, tracked or not yet added, and existing.
    private static IEnumerable<string> Files(string under, string extension) =>
        RepositoryFiles.Value.Where(f => f.StartsWith(under, StringComparison.Ordinal) && f.EndsWith(extension, StringComparison.Ordinal));

    private static readonly Lazy<string[]> RepositoryFiles = new(() =>
    {
        string listing;
        try
        {
            listing = Git("ls-files", "--cached", "--others", "--exclude-standard");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A copy without Git is walked, what a build makes left out.
            return Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
                .Where(f => !Regex.IsMatch(f, @"(^|/)(bin|obj|\.git|captures)/|^build/(package|tools|sessions|soak|artifacts|shader-cache)/"))
                .ToArray();
        }
        return listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(f => File.Exists(Path.Combine(Root, f))).ToArray();
    });

    // The files whose prose STYLE.md governs.
    private static bool Prose(string file) =>
        Path.GetExtension(file) is ".cs" or ".md" or ".slang" or ".sh" or ".ps1" or ".py" or ".yml" or ".csproj" or ".props" or ".targets";

    internal static string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} ended with {git.ExitCode}: {git.StandardError.ReadToEnd()}");
        return output;
    }

    // The words N 4.7 looks for, which build/pack.sh leaves out of the release notes as well.
    [GeneratedRegex(@"(?i:\bthe owner\b|\bthe reviewing session\b)|\bREVIEW\.md\b")]
    internal static partial Regex NamesWhoDecided();

    [GeneratedRegex(@"`(?<name>[^`]+)`")]
    private static partial Regex Ticked();

    // A public type declared at a file's top level, which a file-scoped namespace leaves unindented.
    [GeneratedRegex(@"^public\s+(?:(?:static|sealed|abstract|readonly|partial|unsafe|ref)\s+)*(?:record\s+struct|record\s+class|record|class|struct|interface|enum|delegate\s+\S+)\s+(?<name>\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelPublicType();

    [GeneratedRegex(@"\b(?:Stopwatch|Thread\.Sleep|Task\.Delay|DateTime\.Now|DateTime\.UtcNow|DateTimeOffset\.Now|DateTimeOffset\.UtcNow|Environment\.TickCount64|Environment\.TickCount|SpinWait)\b")]
    private static partial Regex Clock();

    [GeneratedRegex(@"\b(?:CreateTempSubdirectory|Path\.GetTempPath|GetTempFileName|Directory\.Delete)\b")]
    private static partial Regex TemporaryFolder();
}
