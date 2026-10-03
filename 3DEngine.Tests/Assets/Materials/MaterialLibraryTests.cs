using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Materials;

[Trait("Category", "Unit")]
public class MaterialLibraryTests
{
    [Fact]
    public void Create_Assigns_Unique_Ids_And_Returns_Live_Handle()
    {
        var lib = new MaterialLibrary();
        var a = lib.Create(new MaterialDescription { Name = "A" });
        var b = lib.Create(new MaterialDescription { Name = "B" });

        a.Id.Should().NotBe(b.Id);
        a.IsValid.Should().BeTrue();
        b.IsValid.Should().BeTrue();
        lib.Count.Should().Be(2);
    }

    [Fact]
    public void Default_Handle_Is_Not_Valid()
    {
        var h = default(MaterialHandle);

        h.IsValid.Should().BeFalse();
        h.Library.Should().BeNull();
    }

    [Fact]
    public void GetDescription_Returns_Independent_Clone()
    {
        var lib = new MaterialLibrary();
        var h = lib.Create(new MaterialDescription { Name = "M", MetallicFactor = 0.5f });

        var snap = h.GetDescription();
        snap.MetallicFactor = 0.99f;

        // mutating the snapshot must not affect the library entry
        h.GetDescription().MetallicFactor.Should().Be(0.5f);
    }

    [Fact]
    public void Mutate_Writes_Back_In_Place()
    {
        var lib = new MaterialLibrary();
        var h = lib.Create(new MaterialDescription { Name = "Gold" });

        h.SetBaseColor(new Vector4(1f, 0.86f, 0.57f, 1f));
        h.SetMetallic(1f);
        h.SetRoughness(0.2f);
        h.SetEmissive(new Vector3(0.05f, 0.04f, 0f));

        var d = h.GetDescription();
        d.BaseColorFactor.Should().Be(new Vector4(1f, 0.86f, 0.57f, 1f));
        d.MetallicFactor.Should().Be(1f);
        d.RoughnessFactor.Should().Be(0.2f);
        d.EmissiveFactor.Should().Be(new Vector3(0.05f, 0.04f, 0f));
    }

    [Fact]
    public void Update_Reindexes_Name_And_SourcePath()
    {
        var lib = new MaterialLibrary();
        var h = lib.Create(new MaterialDescription { Name = "Old", SourcePath = "/old/path" });

        h.SetDescription(new MaterialDescription { Name = "New", SourcePath = "/new/path" });

        lib.TryFindByName("Old", out _).Should().BeFalse();
        lib.TryFindByName("New", out var byName).Should().BeTrue();
        byName.Should().Be(h);

        lib.TryFindBySourcePath("/old/path", out _).Should().BeFalse();
        lib.TryFindBySourcePath("/new/path", out var bySrc).Should().BeTrue();
        bySrc.Should().Be(h);
    }

    [Fact]
    public void CreateOrGet_Deduplicates_By_SourcePath_When_Enabled()
    {
        var lib = new MaterialLibrary(new MaterialSettings { DeduplicateBySourcePath = true });
        var first  = lib.CreateOrGet(new MaterialDescription { Name = "M", SourcePath = "/Looks/M" });
        var second = lib.CreateOrGet(new MaterialDescription { Name = "M2", SourcePath = "/Looks/M" });

        second.Should().Be(first);
        lib.Count.Should().Be(1);
    }

    [Fact]
    public void CreateOrGet_Does_Not_Deduplicate_When_Setting_Off()
    {
        var lib = new MaterialLibrary(new MaterialSettings { DeduplicateBySourcePath = false });
        var first  = lib.CreateOrGet(new MaterialDescription { Name = "M", SourcePath = "/Looks/M" });
        var second = lib.CreateOrGet(new MaterialDescription { Name = "M2", SourcePath = "/Looks/M" });

        second.Should().NotBe(first);
        lib.Count.Should().Be(2);
    }

    [Fact]
    public void TryFindByName_And_TryFindBySourcePath_Round_Trip()
    {
        var lib = new MaterialLibrary();
        var h = lib.Create(new MaterialDescription { Name = "Steel", SourcePath = "/Looks/Steel" });

        lib.TryFindByName("Steel", out var byName).Should().BeTrue();
        byName.Should().Be(h);

        lib.TryFindBySourcePath("/Looks/Steel", out var bySrc).Should().BeTrue();
        bySrc.Should().Be(h);

        lib.TryFindByName("missing", out _).Should().BeFalse();
        lib.TryFindBySourcePath("/missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Destroy_Removes_Material_And_Invalidates_Handle()
    {
        var lib = new MaterialLibrary();
        var h = lib.Create(new MaterialDescription { Name = "Tmp", SourcePath = "/Looks/Tmp" });

        h.Destroy();

        h.IsValid.Should().BeFalse();
        lib.Count.Should().Be(0);
        lib.TryFindByName("Tmp", out _).Should().BeFalse();
        lib.TryFindBySourcePath("/Looks/Tmp", out _).Should().BeFalse();
    }

    [Fact]
    public void Get_From_Foreign_Library_Throws()
    {
        var libA = new MaterialLibrary();
        var libB = new MaterialLibrary();
        var h = libA.Create(new MaterialDescription { Name = "X" });

        var act = () => libB.GetDescription(h);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Settings_Defaults_Are_Applied_On_Create_When_Field_Is_Default()
    {
        var lib = new MaterialLibrary(new MaterialSettings
        {
            DefaultDoubleSided = true,
            DefaultAlphaMode = MaterialAlphaMode.Mask,
        });
        var h = lib.Create(new MaterialDescription { Name = "M" });

        var d = h.GetDescription();
        d.DoubleSided.Should().BeTrue();
        d.AlphaMode.Should().Be(MaterialAlphaMode.Mask);
    }

    [Fact]
    public void Handles_Snapshot_Enumerates_Every_Live_Material()
    {
        var lib = new MaterialLibrary();
        var a = lib.Create(new MaterialDescription { Name = "A" });
        var b = lib.Create(new MaterialDescription { Name = "B" });
        var c = lib.Create(new MaterialDescription { Name = "C" });

        lib.Handles.Should().BeEquivalentTo(new[] { a, b, c });
    }

    [Fact]
    public void Clear_Wipes_All_State()
    {
        var lib = new MaterialLibrary();
        lib.Create(new MaterialDescription { Name = "A", SourcePath = "/A" });
        lib.Create(new MaterialDescription { Name = "B", SourcePath = "/B" });

        lib.Clear();

        lib.Count.Should().Be(0);
        lib.TryFindByName("A", out _).Should().BeFalse();
        lib.TryFindBySourcePath("/B", out _).Should().BeFalse();
    }
}