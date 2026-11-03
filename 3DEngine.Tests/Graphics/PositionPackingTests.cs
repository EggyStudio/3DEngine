using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>The positions of a mesh uploaded from them alone packed as 16-bit integers of a power of two, where they lie on its grid.</summary>
[Trait("Category", "Unit")]
public sealed class PositionPackingTests
{
    private static ModelVertex[] At(params Vector3[] positions) => [.. positions.Select(p => new ModelVertex { Position = p })];

    [Fact]
    public void A_Section_On_A_Sixteenth_Grid_Packs_To_Integers_That_Give_Its_Positions_Back_Exactly()
    {
        // A section's corners and faces from 0 to 16 blocks in sixteenths, as a voxel game meshes
        // its sections, the furthest 16, whose 32,767 multiples the quantum of 2^-10 reaches.
        var positions = Enumerable.Range(0, 257).Select(i => new Vector3(i / 16f, (i * 7 % 257) / 16f, 16 - i / 16f)).ToArray();
        var vertices = At(positions);
        var quantum = PositionPacking.Quantum(vertices);
        quantum.Should().Be(MathF.Pow(2, -10), "the smallest power of two whose 32,767 multiples reach 16");
        var integers = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(PositionPacking.Pack(vertices, quantum, 3));
        for (int i = 0; i < positions.Length; i++)
        {
            var back = new Vector3(integers[i * 3], integers[i * 3 + 1], integers[i * 3 + 2]) * quantum;
            back.Should().Be(positions[i], $"position {i} comes back as it was, bit for bit");
        }
        PositionPacking.Pack(vertices, quantum, 4).Length.Should().Be(positions.Length * 8, "four integers a vertex where the device reads no three");
    }

    [Fact]
    public void A_Mesh_With_A_Position_Off_Its_Grid_Or_Not_A_Number_Keeps_Its_Floats()
    {
        PositionPacking.Quantum(At(new Vector3(0, 0, 0), new Vector3(16, 0, 0.1f))).Should().Be(0, "a tenth is no multiple of a power of two that holds 16 in 16 bits");
        PositionPacking.Quantum(At(new Vector3(float.NaN, 0, 0))).Should().Be(0, "a position that is not a number packs to nothing");
        PositionPacking.Quantum(At(Vector3.Zero, Vector3.Zero)).Should().Be(1, "every position at the origin is a multiple of anything");
        PositionPacking.Quantum(At(new Vector3(-3000, 0, 0), new Vector3(0, 0, 4096))).Should().Be(0.25f, "4,096 in 16,384 quarters, since in eighths it would take 32,768, -3,000 among them");
    }
}
