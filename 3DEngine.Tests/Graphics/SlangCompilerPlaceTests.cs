using System.Runtime.Versioning;
using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>
/// Where the shader compiler is taken from beside the program, and the folder a compile writes
/// in, so neither is one another user set in the program's way.
/// </summary>
[Trait("Category", "Unit")]
[Collection("E3D")]
public sealed class SlangCompilerPlaceTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-slang-place-");

    public void Dispose() => _folder.Dispose();

    private static readonly string Executable = OperatingSystem.IsWindows() ? "slangc.exe" : "slangc";

    // A file at the folder's relative path, made with what it holds, its folders with it.
    private string Made(string relative, string text = "")
    {
        var path = Path.Combine(_folder.Path, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_Compiler_Is_Taken_From_The_Nearest_Checkout_Of_The_Engine_Alone()
    {
        // A compiler set in a folder above the program, which no solution of the engine's marks as
        // its checkout, as a shared folder another user writes in is not.
        var compiler = Made(Path.Combine("outer", "build", "tools", "slang", "bin", Executable));
        var program = Directory.CreateDirectory(Path.Combine(_folder.Path, "outer", "inner", "bin")).FullName;
        SlangCompiler.FromCheckout(program, Executable).Should().BeNull("no folder above the program is a checkout of the engine");

        Made(Path.Combine("outer", "inner", "VoxelSandbox.slnx"));
        SlangCompiler.FromCheckout(program, Executable).Should().BeNull("a game's solution marks no checkout of the engine");

        Made(Path.Combine("outer", SlangCompiler.SolutionFile));
        SlangCompiler.FromCheckout(program, Executable).Should().Be(compiler, "the checkout above the program fetched it");

        Made(Path.Combine("outer", "inner", SlangCompiler.SolutionFile));
        SlangCompiler.FromCheckout(program, Executable).Should().BeNull("the walk stops at the nearest checkout, which fetched none");
    }

    [Fact]
    public void A_Compile_Where_No_Runtime_Folder_Stands_Writes_In_The_Users_Own_Cache_And_Not_In_One_A_Test_Set()
    {
        // A test that sets the engine's cache to a folder of its own deletes it as it ends, while a
        // compile in a test running beside it may stand in it where no runtime folder takes the
        // compile, as on macOS. The compile's folder lies in the user's own cache whatever a test set.
        var set = Directory.CreateDirectory(Path.Combine(_folder.Path, "set")).FullName;
        UserCache.Folder = set;
        try
        {
            var work = SlangCompiler.WorkFolder(null, SlangCompiler.WorkCache);
            try
            {
                work.FullName.Should().NotStartWith(set, "the folder a test set is deleted as the test ends");
                if (!OperatingSystem.IsWindows())
                    work.Parent!.FullName.Should().Be(Path.Combine(UserCache.Own, "slang-work"));
            }
            finally
            {
                work.Delete(true);
            }
        }
        finally
        {
            UserCache.Folder = null!;
        }
    }

    [NeedsUnixUserFact]
    public void A_Compiler_Another_User_Owns_Is_Not_Taken()
    {
        // The checkout's compiler is a link to the system's shell, which the superuser owns.
        var own = Made("own");
        OwnFiles.IsOwn(own).Should().BeTrue("the test made it");
        OwnFiles.IsOwn("/bin/sh").Should().BeFalse("the superuser owns the system's shell");

        Made(SlangCompiler.SolutionFile);
        var bin = Directory.CreateDirectory(Path.Combine(_folder.Path, "build", "tools", "slang", "bin")).FullName;
        File.CreateSymbolicLink(Path.Combine(bin, Executable), "/bin/sh");
        SlangCompiler.FromCheckout(_folder.Path, Executable).Should().BeNull("the compiler is another user's");
    }

    [NeedsUnixUserFact]
    [UnsupportedOSPlatform("windows")]
    public void A_Compile_Writes_In_A_Folder_Of_Its_Own_Closed_To_Others()
    {
        // The user's runtime folder where it is the user's own and closed, and the engine's cache
        // folder made closed in its place where the runtime folder is open to others or missing.
        const UnixFileMode closed = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        var runtime = Directory.CreateDirectory(Path.Combine(_folder.Path, "runtime"), closed).FullName;
        var cache = Path.Combine(_folder.Path, "cache", "slang-work");

        var work = SlangCompiler.WorkFolder(runtime, cache);
        work.Parent!.FullName.Should().Be(runtime);
        File.GetUnixFileMode(work.FullName).Should().Be(closed, "no other user reads or writes the compile's files");

        File.SetUnixFileMode(runtime, closed | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        SlangCompiler.WorkFolder(runtime, cache).Parent!.FullName.Should().Be(cache, "a runtime folder others may write is passed over");
        SlangCompiler.WorkFolder(null, cache).Parent!.FullName.Should().Be(cache, "and so is none");
        File.GetUnixFileMode(cache).Should().Be(closed, "the cache's work folder is made closed");

        File.SetUnixFileMode(cache, closed | UnixFileMode.GroupWrite);
        var open = () => SlangCompiler.WorkFolder(null, cache);
        open.Should().Throw<InvalidOperationException>("no compile writes where others may").WithMessage("*others may write*");
    }
}
