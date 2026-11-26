using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>
/// The field's reads of one cascade as the engine's compiler builds them, held to keep no copy of
/// the table of cascades in a thread's own memory, which a read of one cascade has no need of, and
/// which MoltenVK's translation of the field's view to Metal read wrong on macOS where Vulkan on
/// Linux and Windows took it as it was.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FieldReadCopiesTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-field-read-copies-");

    public void Dispose() => _folder.Dispose();

    [NeedsSlangFact]
    public void A_Read_Of_One_Cascade_Copies_No_Table_Of_Cascades_Into_A_Threads_Memory()
    {
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        var spirv = SlangCompiler.Compile("""
            import scenefield;

            [[vk::binding(0, 0)]] Sampler3D sceneField;
            [[vk::binding(1, 0)]] Texture3D fieldSecond;
            [[vk::binding(2, 0)]] ConstantBuffer<SceneFieldInfo> fieldInfo;
            [[vk::binding(3, 0)]] RWStructuredBuffer<float> read;

            [shader("compute")]
            [numthreads(8, 1, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                int c = int(id.x) % max(int(fieldInfo.counts.x), 1);
                float3 p = float3(id) * 0.25;
                read[id.x] = fieldDistanceIn(sceneField, fieldInfo, c, p)
                    + fieldDistanceSheetIn(SheetVolume(sceneField, fieldSecond), fieldInfo, c, p);
            }
            """, "field_read_copies.slang", "computeMain", ShaderStage.Compute, _folder.Path, shaders);
        ThreadArraysOf(spirv, "FieldCascade").Should().Be(0);
    }

    // The copies the built-in shaders make of the table, every one a march's through the field,
    // which takes the whole table to find the cascade a point lies in.
    private const int MarchCopies = 96;

    [NeedsSlangFact]
    public void The_Built_In_Shaders_Copy_The_Table_Of_Cascades_No_More_Often_Than_Their_Marches_Do()
    {
        var shaders = Path.Combine(AppContext.BaseDirectory, "source", "shaders");
        var loader = new SlangLoader(_folder.Path, shaders);
        var found = new List<string>();
        int copies = 0;
        foreach (var file in Directory.GetFiles(shaders, "*.slang").Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            if (!SlangLoader.EntryPoints(source).Any()) continue;
            var program = loader.Compile(source, Path.GetFileName(file));
            foreach (var (stage, spirv) in program.Stages)
            {
                var count = ThreadArraysOf(spirv, "FieldCascade");
                copies += count;
                if (count > 0) found.Add($"{Path.GetFileName(file)}'s {stage} stage {count}");
            }
        }
        copies.Should().BeLessThanOrEqualTo(MarchCopies, $"the copies are {string.Join(", ", found)}");
    }

    [Fact]
    public void The_Thread_Arrays_Of_A_Type_Are_Counted_From_The_Spirv()
    {
        // A module naming a struct, an array of it, a pointer to that array in a thread's memory
        // and one in a uniform's, and a variable of each.
        var words = new List<uint> { 0x07230203, 0x00010000, 0, 20, 0 };
        void Instruction(uint opcode, params uint[] operands)
        {
            words.Add((uint)(1 + operands.Length) << 16 | opcode);
            words.AddRange(operands);
        }
        words.Add(4u << 16 | 5);
        words.AddRange([1, BitConverter.ToUInt32("Cell"u8), 0]);
        Instruction(30, 1, 2);
        Instruction(28, 3, 1, 4);
        Instruction(32, 5, Function, 3);
        Instruction(32, 6, 2, 3);
        Instruction(59, 5, 7, Function);
        Instruction(59, 6, 8, 2);
        Instruction(59, 5, 9, Function);
        var spirv = words.SelectMany(BitConverter.GetBytes).ToArray();
        ThreadArraysOf(spirv, "Cell").Should().Be(2);
        ThreadArraysOf(spirv, "Other").Should().Be(0);
    }

    private const uint Function = 7;

    // How many variables in a thread's own memory hold an array of the struct named.
    private static int ThreadArraysOf(byte[] spirv, string type)
    {
        const uint OpName = 5, OpTypeArray = 28, OpTypePointer = 32, OpVariable = 59;
        var words = new uint[spirv.Length / 4];
        Buffer.BlockCopy(spirv, 0, words, 0, words.Length * 4);
        var named = new HashSet<uint>();
        var arrays = new HashSet<uint>();
        var pointers = new HashSet<uint>();
        int count = 0;
        for (int at = 5; at < words.Length;)
        {
            var length = (int)(words[at] >> 16);
            var opcode = words[at] & 0xffff;
            if (length == 0) break;
            if (opcode == OpName && NameAt(words, at + 2, at + length) == type)
                named.Add(words[at + 1]);
            else if (opcode == OpTypeArray && named.Contains(words[at + 2]))
                arrays.Add(words[at + 1]);
            else if (opcode == OpTypePointer && words[at + 2] == Function && arrays.Contains(words[at + 3]))
                pointers.Add(words[at + 1]);
            else if (opcode == OpVariable && words[at + 3] == Function && pointers.Contains(words[at + 1]))
                count++;
            at += length;
        }
        return count;
    }

    // The string the uints of a literal hold from first up to end, to its first zero byte.
    private static string NameAt(uint[] words, int first, int end)
    {
        var bytes = new byte[(end - first) * 4];
        Buffer.BlockCopy(words, first * 4, bytes, 0, bytes.Length);
        var stop = Array.IndexOf(bytes, (byte)0);
        return System.Text.Encoding.UTF8.GetString(bytes, 0, stop < 0 ? bytes.Length : stop);
    }
}
