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
    public void A_Texture_Not_On_The_GPU_Yet_Reads_Back_From_Its_Queued_Pixels_As_Raylib_Reads_At_Once()
    {
        var store = new TextureStore();
        var pixels = Pixels(2, 2);
        pixels[0] = 10;
        var id = store.Add(pixels, 2, 2);
        store.UpdateRegion(id, [20, 21, 22, 23], 1, 1, 1, 1).Should().BeTrue();

        var pending = store.PendingPixels(id);

        pending.Should().NotBeNull();
        pending![0].Should().Be(10, "the whole upload is read");
        pending[12..16].Should().Equal([20, 21, 22, 23], "with the rectangle queued after it laid over");
        pixels[12].Should().Be(0, "and the program's own array is left as it was");

        store.Take();
        store.PendingPixels(id).Should().BeNull("once the queue is taken the pixels are on the GPU, read from there");
        store.PendingPixels(store.AddTarget(4, 4)).Should().BeNull("and a render target is drawn rather than uploaded");
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
    public void SetWrap_Queues_A_Sampler_Change_That_Later_Uploads_Keep()
    {
        var store = new TextureStore();
        var id = store.Add(Pixels(1, 1), 1, 1);
        store.Take();

        store.SetWrap(id, TextureWrap.Clamp).Should().BeTrue();
        store.SetWrap(999, TextureWrap.Clamp).Should().BeFalse();
        store.Take().Uploads.Should().ContainSingle().Which.Should().Match<TextureStore.Upload>(u => u.Rgba == null && u.Wrap == TextureWrap.Clamp);

        store.SetFilter(id, TextureFilter.Point);
        store.Take().Uploads.Should().ContainSingle().Which.Wrap.Should().Be(TextureWrap.Clamp, "a filter change keeps the wrap");
        GpuTextures.SamplerFor(TextureFilter.Point, TextureWrap.Clamp).AddressU.Should().Be(SamplerAddressMode.ClampToEdge);
        GpuTextures.SamplerFor(TextureFilter.Point).AddressV.Should().Be(SamplerAddressMode.Repeat);
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

    [Fact]
    public void Mipmaps_Asked_For_Before_Upload_Ride_On_The_Pending_Pixels()
    {
        var store = new TextureStore();
        var id = store.Add(new byte[16], 2, 2);

        store.GenerateMipmaps(id).Should().BeTrue();

        store.Take().Uploads.Should().ContainSingle().Which.Should().Match<TextureStore.Upload>(u => u.Mipmaps && u.Rgba != null);
        store.HasMipmaps(id).Should().BeTrue();
    }

    [Fact]
    public void Mipmaps_Asked_For_After_Upload_Queue_A_Rebuild_And_Stay_Through_Updates()
    {
        var store = new TextureStore();
        var id = store.Add(new byte[16], 2, 2);
        store.Take();

        store.GenerateMipmaps(id).Should().BeTrue();
        store.GenerateMipmaps(id).Should().BeTrue();
        store.Take().Uploads.Should().ContainSingle().Which.Should().Match<TextureStore.Upload>(u => u.Mipmaps && u.Rgba == null);

        store.Update(id, new byte[16]);
        store.SetFilter(id, TextureFilter.Point);
        store.Take().Uploads.Should().OnlyContain(u => u.Mipmaps);
    }

    [Fact]
    public void A_Render_Target_Or_Unknown_Texture_Has_No_Mipmaps()
    {
        var store = new TextureStore();
        var target = store.AddTarget(4, 4);

        store.GenerateMipmaps(target).Should().BeFalse();
        store.GenerateMipmaps(999).Should().BeFalse();
    }

    [Fact]
    public void The_Full_Mip_Chain_Reaches_One_Pixel()
    {
        ImageDesc.FullMipChain(1, 1).Should().Be(1);
        ImageDesc.FullMipChain(256, 256).Should().Be(9);
        ImageDesc.FullMipChain(300, 20).Should().Be(9);
    }

    [Fact]
    public void An_Anisotropic_Filter_Asks_The_Sampler_For_Its_Samples()
    {
        GpuTextures.SamplerFor(TextureFilter.Point).Should().Match<SamplerDesc>(d => d.MinFilter == SamplerFilter.Nearest && d.MaxAnisotropy == 1);
        GpuTextures.SamplerFor(TextureFilter.Bilinear).MaxAnisotropy.Should().Be(1);
        GpuTextures.SamplerFor(TextureFilter.Anisotropic8x).Should().Match<SamplerDesc>(d => d.MinFilter == SamplerFilter.Linear && d.MaxAnisotropy == 8);
    }

    [Fact]
    public void Bilinear_Reads_The_Nearest_Mip_Level_And_Trilinear_Blends_Two_As_In_Raylib()
    {
        GpuTextures.SamplerFor(TextureFilter.Point).MipFilter.Should().Be(SamplerFilter.Nearest);
        GpuTextures.SamplerFor(TextureFilter.Bilinear).Should().Match<SamplerDesc>(d => d.MinFilter == SamplerFilter.Linear && d.MipFilter == SamplerFilter.Nearest);
        GpuTextures.SamplerFor(TextureFilter.Trilinear).Should().Match<SamplerDesc>(d => d.MinFilter == SamplerFilter.Linear && d.MipFilter == SamplerFilter.Linear);
        GpuTextures.SamplerFor(TextureFilter.Anisotropic4x).MipFilter.Should().Be(SamplerFilter.Linear, "an anisotropic filter is trilinear with more samples");
    }
}
