using FluentAssertions;

namespace Engine.Tests.Rendering.Immediate;

[Trait("Category", "Unit")]
public class TextureStoreTests
{
    private static byte[] Pixels(int width, int height) => new byte[width * height * 4];

    [Fact]
    public void Ids_Start_At_One_So_A_Default_Texture_Is_Not_Loaded()
    {
        var store = new TextureStore();

        store.Add(Pixels(2, 2), 2, 2).Should().Be(1);
        store.Add(Pixels(2, 2), 2, 2).Should().Be(2);
        store.Contains(0).Should().BeFalse();
    }

    [Fact]
    public void Pixels_That_Do_Not_Match_The_Size_Are_Refused()
    {
        var act = () => new TextureStore().Add(Pixels(2, 2), 3, 2);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Take_Hands_Over_Uploads_And_Removals_Once()
    {
        var store = new TextureStore();
        var a = store.Add(Pixels(1, 1), 1, 1);
        var b = store.Add(Pixels(1, 1), 1, 1);
        store.Remove(a);

        var (uploads, removals) = store.Take();

        uploads.Select(u => u.Id).Should().Equal(b);
        removals.Should().Equal(a);
        store.Take().Uploads.Should().BeEmpty();
    }

    [Fact]
    public void A_Filter_Change_Carries_No_Pixels()
    {
        var store = new TextureStore();
        var id = store.Add(Pixels(1, 1), 1, 1);
        store.Take();

        store.SetFilter(id, TextureFilter.Point).Should().BeTrue();

        var upload = store.Take().Uploads.Should().ContainSingle().Subject;
        upload.Rgba.Should().BeNull();
        upload.Filter.Should().Be(TextureFilter.Point);
    }

    [Fact]
    public void Update_Needs_A_Loaded_Texture_Of_The_Same_Size()
    {
        var store = new TextureStore();
        var id = store.Add(Pixels(2, 2), 2, 2);

        store.Update(id, Pixels(2, 2)).Should().BeTrue();
        store.Update(id, Pixels(3, 3)).Should().BeFalse();
        store.Update(99, Pixels(2, 2)).Should().BeFalse();
    }

    [Fact]
    public void Removed_Textures_Leave_The_Count()
    {
        var store = new TextureStore();
        var id = store.Add(Pixels(1, 1), 1, 1);

        store.Remove(id).Should().BeTrue();
        store.Remove(id).Should().BeFalse();
        store.Count.Should().Be(0);
    }

    [Fact]
    public void A_Render_Target_Is_A_Texture_Id_Queued_Without_Pixels()
    {
        var store = new TextureStore();
        var id = store.AddTarget(320, 240);

        var upload = store.Take().Uploads.Should().ContainSingle().Subject;
        upload.Id.Should().Be(id);
        upload.Target.Should().BeTrue();
        upload.Rgba.Should().BeNull();
        store.Contains(id).Should().BeTrue();
    }
}
