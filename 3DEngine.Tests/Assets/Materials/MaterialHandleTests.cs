using FluentAssertions;
using Xunit;

namespace Engine.Tests.Materials;

[Trait("Category", "Unit")]
public class MaterialHandleTests
{
    [Fact]
    public void Equality_Compares_Library_Reference_And_Id()
    {
        var libA = new MaterialLibrary();
        var libB = new MaterialLibrary();
        var a1 = libA.Create(new MaterialDescription { Name = "M" });
        var a2 = new MaterialHandle(libA, a1.Id);
        var b1 = libB.Create(new MaterialDescription { Name = "M" });

        (a1 == a2).Should().BeTrue();
        a1.GetHashCode().Should().Be(a2.GetHashCode());
        (a1 == b1).Should().BeFalse();
    }

    [Fact]
    public void Name_Property_Resolves_Through_Library()
    {
        var lib = new MaterialLibrary();
        var h = lib.Create(new MaterialDescription { Name = "Copper" });

        h.Name.Should().Be("Copper");
    }
}

[Trait("Category", "Unit")]
public class MaterialPluginTests
{
    [Fact]
    public void Plugin_Inserts_MaterialLibrary_Resource()
    {
        using var app = new App();
        app.AddPlugin(new MaterialPlugin());

        app.World.ContainsResource<MaterialLibrary>().Should().BeTrue();
        app.World.Resource<MaterialLibrary>().Count.Should().Be(0);
    }

    [Fact]
    public void Plugin_Is_Idempotent_When_Added_Twice()
    {
        using var app = new App();
        app.AddPlugin(new MaterialPlugin());
        var lib = app.World.Resource<MaterialLibrary>();

        app.AddPlugin(new MaterialPlugin());

        app.World.Resource<MaterialLibrary>().Should().BeSameAs(lib);
    }

    [Fact]
    public void Plugin_Honours_Existing_MaterialSettings_Resource()
    {
        using var app = new App();
        app.World.InsertResource(new MaterialSettings { DefaultDoubleSided = true });

        app.AddPlugin(new MaterialPlugin());

        var lib = app.World.Resource<MaterialLibrary>();
        lib.Settings.DefaultDoubleSided.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class MaterialSettingsTests
{
    [Fact]
    public void Defaults_Are_Sensible()
    {
        var s = new MaterialSettings();

        s.DefaultDoubleSided.Should().BeFalse();
        s.DefaultAlphaMode.Should().Be(MaterialAlphaMode.Opaque);
        s.DefaultWrapS.Should().Be(TextureWrapMode.Repeat);
        s.DefaultWrapT.Should().Be(TextureWrapMode.Repeat);
        s.DeduplicateBySourcePath.Should().BeTrue();
    }
}