using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// A value kept in a file between runs, read over its default through the scene codec of its type,
/// here a <see cref="Transform"/>, whose codec starts a field the file lacks from its identity.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersistentTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-persistent-");

    public void Dispose() => _folder.Dispose();

    private static readonly Transform Fallback = new(new Vector3(1, 2, 3), Quaternion.Identity, new Vector3(2, 2, 2));

    [Fact]
    public void A_File_Is_Read_Over_The_Default_So_A_Field_It_Lacks_Keeps_The_Default_s()
    {
        // As a file an earlier version wrote before the scale was kept.
        var file = _folder.File("camera.json");
        File.WriteAllText(file, """{ "Position": [5, 6, 7] }""");

        var kept = new Persistent<Transform>(file, Fallback);

        kept.Value.Position.Should().Be(new Vector3(5, 6, 7), "the file's own field");
        kept.Value.Scale.Should().Be(new Vector3(2, 2, 2), "the field the file lacks keeps the default's, not the codec's identity");
        kept.Problem.Should().BeNull();
        kept.Changed.Should().BeFalse();
    }

    [Fact]
    public void A_Value_Saved_On_Demand_Reads_Back_And_One_Saved_On_Change_Writes_At_Each_Set()
    {
        var file = _folder.File("settings/camera.json");
        var kept = new Persistent<Transform>(file, Fallback);
        kept.Value.Should().Be(Fallback, "with no file the default stands");

        kept.Update(t => t with { Position = new Vector3(9, 0, 0) });
        kept.Changed.Should().BeTrue();
        File.Exists(file).Should().BeFalse("nothing is written until asked");
        kept.Save();
        kept.Changed.Should().BeFalse();
        new Persistent<Transform>(file, Fallback).Value.Position.Should().Be(new Vector3(9, 0, 0));

        var eager = new Persistent<Transform>(file, Fallback, saveOnChange: true);
        eager.Set(eager.Value with { Scale = Vector3.One });
        new Persistent<Transform>(file, Fallback).Value.Scale.Should().Be(Vector3.One, "written at the set");

        eager.Set(eager.Value with { Scale = new Vector3(3, 3, 3) });
        eager.Revert();
        eager.Value.Scale.Should().Be(new Vector3(3, 3, 3), "saved on change, so the file holds it already");
        eager.Reset();
        new Persistent<Transform>(file, Fallback).Value.Should().Be(Fallback, "a reset saves the default");
    }

    [Fact]
    public void A_File_That_Cannot_Be_Read_Gives_The_Default_And_Is_Not_Written_Over()
    {
        var file = _folder.File("broken.json");
        File.WriteAllText(file, "{ broken");

        var kept = new Persistent<Transform>(file, Fallback);

        kept.Value.Should().Be(Fallback);
        kept.Problem.Should().Contain("broken.json").And.Contain("the default stands");
        File.ReadAllText(file).Should().Be("{ broken", "a file that only failed to read is left for the player to see");
    }

    [Fact]
    public void A_Type_No_Codec_Writes_Is_Refused()
    {
        var made = () => new Persistent<Vector3>(_folder.File("v.json"), Vector3.Zero);
        made.Should().Throw<InvalidOperationException>().WithMessage("*Mark it [SceneComponent]*");
    }
}
