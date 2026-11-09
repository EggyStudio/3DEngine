using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>The names of the SPIR-V modules the compiler gave, by their bytes (<see cref="ShaderNames"/>).</summary>
public sealed class ShaderNamesTests
{
    [Fact]
    public void A_Module_Is_Named_By_Its_Bytes_And_One_Not_Noted_Says_So()
    {
        byte[] noted = [3, 2, 35, 7, 1, 2, 3, 4], copied = [.. noted], other = [3, 2, 35, 7, 4, 3, 2, 1];
        ShaderNames.Note(noted, "named.slang's computeMain");
        ShaderNames.Of(copied).Should().Be("named.slang's computeMain", "a copy of the bytes is the same module");
        ShaderNames.Of(other).Should().Be("SPIR-V the compiler did not give");
    }
}
