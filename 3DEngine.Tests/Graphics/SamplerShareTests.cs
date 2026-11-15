using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Graphics;

/// <summary>The device's samplers, one for each distinct set of settings, shared by all that ask for it.</summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class SamplerShareTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void Samplers_Of_The_Same_Settings_Share_One_Which_The_Last_Let_Go_Destroys()
    {
        using var device = new GraphicsDevice();
        device.Initialize(new OffscreenSurface(16, 16), "sampler test");
        var before = device.SamplerCount;
        var linear = new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear, SamplerAddressMode.Repeat, SamplerAddressMode.Repeat, SamplerAddressMode.Repeat);
        var nearest = linear with { MinFilter = SamplerFilter.Nearest, MagFilter = SamplerFilter.Nearest };

        var smooth = Enumerable.Range(0, 3).Select(_ => (GraphicsDevice.VulkanSampler)device.CreateSampler(linear)).ToList();
        // A mip filter given as the one it is taken as when not given makes the same sampler.
        smooth.Add((GraphicsDevice.VulkanSampler)device.CreateSampler(linear with { MipFilter = SamplerFilter.Linear }));
        var sharp = (GraphicsDevice.VulkanSampler)device.CreateSampler(nearest);
        device.SamplerCount.Should().Be(before + 2, "four samplers of one set of settings and one of another make two");
        smooth.Select(s => s.Sampler.Handle).Distinct().Should().ContainSingle("the four share one");
        sharp.Sampler.Handle.Should().NotBe(smooth[0].Sampler.Handle);

        foreach (var sampler in smooth.Skip(1)) sampler.Dispose();
        smooth[1].Dispose();
        device.SamplerCount.Should().Be(before + 2, "one of the four still holds it, and a sampler let go twice lets it go once");
        smooth[0].Dispose();
        device.SamplerCount.Should().Be(before + 1, "the last of the four let it go");
        sharp.Dispose();
        device.SamplerCount.Should().Be(before);
    }

    [NeedsVulkanFact]
    public void Textures_Past_The_Devices_Most_Samplers_Draw_Through_A_Few_Of_Them()
    {
        // MoltenVK on a Mac allows 96 samplers, which a scene of many textures passed at one a
        // texture. The device's most is lowered to 24 here, over what the app's own passes hold, and
        // 160 textures of four filters and wraps are loaded and drawn, so a sampler a texture would
        // pass it many times over, which the device logs as an error and the test fails on.
        var config = Config.Default.WithWindow("sampler test", 64, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        Frames(2, []);
        var held = device.SamplerCount;
        device.SamplerLimit = (uint)held + 8;
        var settings = new[] { (TextureFilter.Point, TextureWrap.Clamp), (TextureFilter.Bilinear, TextureWrap.Repeat), (TextureFilter.Trilinear, TextureWrap.Repeat), (TextureFilter.Bilinear, TextureWrap.MirrorRepeat) };
        var textures = new List<Texture2D>();
        for (int i = 0; i < 160; i++)
        {
            var image = GenImageColor(4, 4, new Color((byte)i, (byte)(255 - i), 128, 255));
            var texture = LoadTextureFromImage(image);
            UnloadImage(image);
            var (filter, wrap) = settings[i % settings.Length];
            SetTextureFilter(texture, filter);
            SetTextureWrap(texture, wrap);
            textures.Add(texture);
        }
        Frames(3, textures);
        device.SamplerCount.Should().BeLessThanOrEqualTo(held + settings.Length, "the 160 textures share a sampler for each of their four settings");
        device.SamplerCount.Should().BeLessThanOrEqualTo((int)device.SamplerLimit);
        foreach (var texture in textures) UnloadTexture(texture);
        // The frame that takes the unloads, and those a texture's parts are kept for after it.
        Frames(GpuTextures.RetireFrames + 2, []);
        device.SamplerCount.Should().BeLessThanOrEqualTo(held, "the textures let theirs go once unloaded");
    }

    // Frames each drawing the textures given side by side.
    private static void Frames(int count, List<Texture2D> textures)
    {
        for (int frame = 0; frame < count; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            for (int i = 0; i < textures.Count; i++)
                DrawTexture(textures[i], i % 16 * 4, i / 16 * 4, Color.White);
            EndDrawing();
        }
    }
}
