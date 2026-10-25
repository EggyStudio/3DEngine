using System.Text;
using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>
/// The names the built-in shaders' SPIR-V carries, which MoltenVK writes into the Metal source it
/// translates each stage to on macOS, held clear of the words Metal keeps for itself.
/// </summary>
[Trait("Category", "Unit")]
public class MetalNamesTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-metal-names-test-");

    public void Dispose() => _folder.Dispose();

    // The words of the Metal Shading Language that no C++ or GLSL keeps, its functions' and address
    // spaces' qualifiers, which the translation leaves as they are where a variable is named one,
    // so Metal's compiler refuses the source. A local named vertex in field_counted.slang stopped
    // every program on macOS at the renderer's start, the translation's line
    // `uint vertex = ...` read as a function's qualifier, where Vulkan on Linux and Windows takes
    // the SPIR-V as it is.
    private static readonly HashSet<string> Reserved =
    [
        "vertex", "fragment", "kernel", "visible", "stitchable", "intersection",
        "device", "constant", "thread", "threadgroup", "threadgroup_imageblock", "ray_data", "object_data",
    ];

    [NeedsSlangFact]
    public void No_Name_A_Built_In_Shader_Carries_Is_A_Word_Metal_Keeps()
    {
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        var loader = new SlangLoader(_folder.Path, shaders);
        var found = new List<string>();
        foreach (var file in Directory.GetFiles(shaders, "*.slang").Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            if (!SlangLoader.EntryPoints(source).Any()) continue;
            var program = loader.Compile(source, Path.GetFileName(file));
            foreach (var (stage, spirv) in program.Stages)
                foreach (var name in Names(spirv).Where(Reserved.Contains).Distinct())
                    found.Add($"{Path.GetFileName(file)}'s {stage} stage names something {name}");
        }
        found.Should().BeEmpty();
    }

    [Fact]
    public void The_Names_Are_Read_From_The_Spirv_Debug_Instructions()
    {
        // A module of one OpName and one OpMemberName, in the words SPIR-V lays them out in.
        var words = new List<uint> { 0x07230203, 0x00010000, 0, 10, 0 };
        void Instruction(uint opcode, uint[] operands, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text + "\0");
            var padded = new byte[(bytes.Length + 3) / 4 * 4];
            bytes.CopyTo(padded, 0);
            var literal = Enumerable.Range(0, padded.Length / 4).Select(i => BitConverter.ToUInt32(padded, i * 4)).ToArray();
            words.Add((uint)(1 + operands.Length + literal.Length) << 16 | opcode);
            words.AddRange(operands);
            words.AddRange(literal);
        }
        Instruction(5, [3], "vertex");
        Instruction(6, [4, 0], "count");
        var spirv = words.SelectMany(BitConverter.GetBytes).ToArray();
        Names(spirv).Should().Equal("vertex", "count");
    }

    // The strings of the module's OpName and OpMemberName instructions, in order.
    private static IEnumerable<string> Names(byte[] spirv)
    {
        const uint OpName = 5, OpMemberName = 6;
        var words = new uint[spirv.Length / 4];
        Buffer.BlockCopy(spirv, 0, words, 0, words.Length * 4);
        for (int at = 5; at < words.Length;)
        {
            var count = (int)(words[at] >> 16);
            var opcode = words[at] & 0xffff;
            if (count == 0) yield break;
            var first = opcode == OpName ? at + 2 : opcode == OpMemberName ? at + 3 : -1;
            if (first > 0 && first < at + count)
            {
                var bytes = new byte[(at + count - first) * 4];
                Buffer.BlockCopy(words, first * 4, bytes, 0, bytes.Length);
                var end = Array.IndexOf(bytes, (byte)0);
                yield return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
            }
            at += count;
        }
    }
}
