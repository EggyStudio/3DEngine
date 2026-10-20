using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The rooms of <c>shaders_bounce_rooms</c> at each quality of global illumination against
/// references path-traced through the GPU's rays, each view's frame held within its measured
/// difference from its reference.
/// </summary>
/// <remarks>
/// A reference is the view's light traced with 4096 paths a pixel at 160 by 90, averaged to 80 by
/// 45, in linear light, checked in under <c>References/bounce</c>; setting
/// <c>E3D_WRITE_REFERENCES=1</c> traces them again. The frame's linear light is averaged the same,
/// and a view's difference is its pixels' differences summed over the reference's light, so one
/// region's excess does not hide another's shortfall. The test runs where the GPU traces rays, as
/// the references are traced through them and the bounds were measured on such a GPU, the
/// development machine's.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class BounceRoomsTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private const int Width = 160, Height = 90, Down = 2;

    // Each view at each quality, bound 15% over what it read on the development machine's GPU,
    // where the views read 0.12 to 0.14 of their light apart from their references in the Cornell
    // box, 0.09 in the thin room, 0.20 to 0.24 in the corridor, 0.27 to 0.30 in the window's, 0.02
    // among the red walls, 0.22 in the strip's, 0.11 to 0.12 on the grazing floor, 0.11 to 0.12 in
    // the carried lamp's and 0.05 to 0.06 around the emissive blocks, the screen's probes' rays
    // reaching twice as far as the world's probes are apart, which took the corridor from 0.26, the
    // strip's from 0.27 to 0.29 and the carried lamp's from 0.10 to 0.12, and since the small
    // emitters are lights of their own (GlowLights), where the strip's read 0.45 to 0.47. The
    // strip's and the blocks' read 0.25 to 0.27 and 0.05 with each pixel's glow light its own;
    // taken from texels of half the window's size (gi_screen_glow.slang), which at 160 by 90 are
    // two pixels wide beside a strip six hundredths of a block thick, they read further, where at
    // 1280 by 720 the strip's reads 0.163 against 0.162 and the blocks' 0.020 against 0.018. Since
    // the second cascade of Low and Medium, of 8 texels a side, sends four rays a texel as the
    // first does (RaysAt), the rooms whose views it moved read as High reads them, the window's
    // 0.356 at Low and 0.340 at Medium where 0.315 and 0.300, High's 0.324, its walls 21 and 16%
    // over at Low where High's are 22 and 19%; the strip's 0.266 and 0.255 where 0.254 and 0.243,
    // High's 0.250; and the grazing floor's 0.105 at Medium where 0.080, High's 0.099; and since
    // the last cascade's rays go on to the field's edge (GlobalIlluminationRenderer), Low's reads
    // 0.104, where 0.092 with one ray a texel and 0.074 with four and the last cascade's rays
    // ending at four times its spacing, its room 7% over its reference where 4% under, High's 8%
    // over. Read with no lean off the surface (Lean), the window's view reads 0.301, 0.287 and
    // 0.270 at Low, Medium and High where 0.356, 0.340 and 0.324, the strip's 0.216, 0.213 and
    // 0.209 where 0.266, 0.255 and 0.250, and the grazing floor's 0.075, 0.075 and 0.055 where
    // 0.104, 0.105 and 0.099. With a hit given none of a glow light whose box its ray passed
    // through within a cell before it (glowLightPassed), the strip's view reads 0.234, 0.245 and
    // 0.244 where 0.222, 0.216 and 0.212, its room 12 to 13% under its reference where level, since
    // the light the strip throws on the wall it is mounted on lies within a fifth of a block of the
    // strip, a band the probes' rays, through the same points of each texel at every probe of a
    // row, pass over, and the probes' rays through the strip to the wall had made up for it. With
    // each tile's rays placed at one of sixteen places in their texels, the thin walls' view reads
    // 0.085, 0.090 and 0.094 where 0.072, 0.071 and 0.072, its world probes 9 to 12% over the
    // reference by their own rays, the gain past the first bounce, and the grazing floor's at
    // High 0.086 where 0.058, its walls 8% over where 8% under, the merge's parallax, each read
    // under by the fixed ways before, which also had the strip's read 0.234, 0.245 and 0.244 where
    // 0.216, 0.240 and 0.218. With the probes above read toward what the rays would meet (the
    // merge's parallax), the thin walls' view reads 0.090, 0.094 and 0.098, their walls thinner
    // than a cell, and the window's, the grazing floor's and the strip's narrow. With the reach's
    // slack an eighth of the probes' spacing where half (probeSees), the probes beyond a wall lend
    // the screen's probes' hits beside it none of their light, and the thin walls' view reads
    // 0.078, 0.079 and 0.084, the lamp outside lighting their walls' foot and edges none. The
    // corridor's reads 0.234, 0.244 and 0.232 where 0.219, 0.232 and 0.215, its walls 78 to 79%
    // under their reference at 800 by 450 where 67 to 70%, the sunlit probes beyond them having
    // covered part of their own reading under, and the grazing floor's at High 0.082 where 0.078,
    // its wall facing -x 12% over where 9%, the dark probes behind it having covered part of the
    // gain past the first bounce. With the screen's probes' rays past their reach marched on
    // through the field where their world probes give nothing (gi_screen.slang), the corridor's
    // view reads 0.213, 0.222 and 0.215, its first cascade's probes holding nothing along it since
    // no probe of a coarser cascade stands inside it.
    [NeedsRayQueryTheory]
    [InlineData(1, GlobalIllumination.Low, 0.17)]
    [InlineData(1, GlobalIllumination.Medium, 0.16)]
    [InlineData(1, GlobalIllumination.High, 0.15)]
    [InlineData(2, GlobalIllumination.Low, 0.09)]
    [InlineData(2, GlobalIllumination.Medium, 0.10)]
    [InlineData(2, GlobalIllumination.High, 0.10)]
    [InlineData(3, GlobalIllumination.Low, 0.25)]
    [InlineData(3, GlobalIllumination.Medium, 0.26)]
    [InlineData(3, GlobalIllumination.High, 0.25)]
    [InlineData(4, GlobalIllumination.Low, 0.35)]
    [InlineData(4, GlobalIllumination.Medium, 0.34)]
    [InlineData(4, GlobalIllumination.High, 0.32)]
    [InlineData(5, GlobalIllumination.Low, 0.03)]
    [InlineData(5, GlobalIllumination.Medium, 0.03)]
    [InlineData(5, GlobalIllumination.High, 0.03)]
    [InlineData(6, GlobalIllumination.Low, 0.27)]
    [InlineData(6, GlobalIllumination.Medium, 0.28)]
    [InlineData(6, GlobalIllumination.High, 0.28)]
    [InlineData(7, GlobalIllumination.Low, 0.09)]
    [InlineData(7, GlobalIllumination.Medium, 0.09)]
    [InlineData(7, GlobalIllumination.High, 0.10)]
    [InlineData(8, GlobalIllumination.Low, 0.15)]
    [InlineData(8, GlobalIllumination.Medium, 0.13)]
    [InlineData(8, GlobalIllumination.High, 0.14)]
    [InlineData(9, GlobalIllumination.Low, 0.07)]
    [InlineData(9, GlobalIllumination.Medium, 0.07)]
    [InlineData(9, GlobalIllumination.High, 0.06)]
    public void Each_Room_Reads_Within_Its_Measured_Difference_From_Its_Reference(int view, GlobalIllumination quality, double bound)
    {
        var config = Config.Default.WithWindow("bounce rooms", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        using var rooms = new Rooms();
        var camera = Rooms.Views[view - 1];
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                rooms.Draw();
                EndMode3D();
                EndDrawing();
            }
        }

        var path = Path.Combine(Folder(), $"view-{view}.pfm");
        if (!File.Exists(path) || Environment.GetEnvironmentVariable("E3D_WRITE_REFERENCES") == "1")
        {
            // Traced at High, where the window's meshes are built for the GPU's rays.
            SetGlobalIllumination(GlobalIllumination.High);
            Frames(SceneFieldPlan.SettleFrames + 30);
            using var folder = new TestFolder("engine-bounce-");
            var traced = Path.Combine(folder.Path, $"view-{view}.png");
            BounceReference.Trace(GetApp().World, traced, 4096);
            var (width, height, light) = BounceReference.ReadPfm(traced + ".pfm");
            WritePfm(path, Averaged(light, width, height, 3));
        }

        SetGlobalIllumination(quality);
        Frames(SceneFieldPlan.SettleFrames + 90);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var decoded = renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!;
        var frame = Averaged(((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(decoded.ColorView.Image), Width, Height, 4);
        var (_, _, reference) = BounceReference.ReadPfm(path);
        double differs = 0, lit = 0;
        for (int i = 0; i < reference.Length; i++)
        {
            differs += Math.Abs(frame[i] - reference[i]);
            lit += reference[i];
        }
        var share = differs / lit;
        share.Should().BeLessThan(bound, $"view {view} at {quality} differs from its reference by {share:0.000} of its light");
    }

    [NeedsVulkanFact]
    public void The_Thin_Rooms_Corner_Takes_No_Light_From_The_Lamp_Outside()
    {
        // The thin room seen as shaders_bounce_rooms' capture sees it, where the right wall meets
        // the front wall at the outside lamp's height. A pixel there that no screen probe on a
        // surface like its own stands near read the world's probes, and one beyond the walls
        // thinner than their spacing lent it the lamp's light, the corner's brightest 3.90 times the
        // wall beside it at 800 by 450 on the development machine's GPU, where a path-traced
        // reference reads the tick's pixels 1.10 of it. Read from the probes it sees through the
        // field, it reads 1.09.
        const int Wide = 800, High = 450;
        var config = Config.Default.WithWindow("bounce rooms", Wide, High) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms();
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(Rooms.Views[1]);
            rooms.Draw();
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        double At(int x, int y) => 0.2126 * light[(y * Wide + x) * 4] + 0.7152 * light[(y * Wide + x) * 4 + 1] + 0.0722 * light[(y * Wide + x) * 4 + 2];
        double brightest = 0, beside = 0;
        for (int y = 150; y < 260; y++)
        {
            for (int x = 676; x < 692; x++) brightest = Math.Max(brightest, At(x, y));
            for (int x = 630; x < 660; x++) beside += At(x, y) / 30;
        }
        beside /= 110;
        (brightest / beside).Should().BeLessThan(1.4, "the corner reads the wall's light, not the lamp's beyond the walls");
    }

    [NeedsVulkanFact]
    public void The_Thin_Walls_Foot_Takes_No_Light_From_The_Lamp_Outside()
    {
        // The thin room's right wall along its foot, seen as the corner's test sees it, with the
        // lamp outside the wall and with that lamp off, which a path-traced reference reads alike.
        // The screen's probes' hits beside the wall read the world's probes by their reach, and
        // those beyond it read as seeing the hits through the slack the reach is given (probeSees),
        // which over half the probes' spacing lent them the lamp's light through the wall 0.1
        // thick, the foot 1.279 times its light with the lamp off on the development machine's GPU,
        // and over an eighth 1.050.
        const int Wide = 800, High = 450;
        var config = Config.Default.WithWindow("bounce rooms", Wide, High) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms();
        double Foot()
        {
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(Rooms.Views[1]);
                rooms.Draw();
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int y = 300; y < 322; y++)
                for (int x = 430; x < 520; x++)
                    sum += 0.2126 * light[(y * Wide + x) * 4] + 0.7152 * light[(y * Wide + x) * 4 + 1] + 0.0722 * light[(y * Wide + x) * 4 + 2];
            return sum / (22 * 90);
        }

        var lit = Foot();
        SetLightColor(rooms.Outside, Color.White, 0);
        var dark = Foot();

        (lit / dark).Should().BeLessThan(1.1, $"the wall's foot reads {lit:0.0000} with the lamp outside and {dark:0.0000} without it");
    }

    // The screen probe gi.screenprobe read in shaders_bounce_rooms at 800 by 450 on the small block's
    // front in the Cornell box. Its rays that met nothing in reach took the world's probes around
    // the point they left the surface from, which saw along each ray's way what the ray passed by,
    // and read 97% over the reference across the ways they stand for on the development machine's
    // GPU; read around where they ended, 9% over. The far wall of the carried lamp's room read 13%
    // under and then 5% under, and is held below by its whole light instead, since once its rays
    // passing the thin wall's end go past their reach they read 11% over the reference along their
    // ways and 116% over it across the ways they stand for, the sixteen ways' own error, which is
    // read apart (RENDERING.md). The probe on the Cornell box's back wall beside the red wall's
    // corner is left out too, its rays past their reach reading the second cascade, whose probes
    // 2.4 apart hold what they meet nearer than that as met before their interval began, so they
    // read 22% under, and the probe as a whole is held below. With each tile's rays at one of
    // sixteen places in their texels, a ray stands for its texel from one place of it, and the
    // block's front's rays past their reach read 34% under the reference across their texels, where
    // 9% over from the texels' middles.
    [NeedsRayQueryTheory]
    [InlineData(1, 468, 364, 0.4)]
    public void A_Screen_Probes_Rays_Past_Their_Reach_Read_The_Light_The_Reference_Holds_Over_Their_Ways(int view, int x, int y, double bound)
    {
        var probe = ReadScreenProbe(view - 1, x, y, out var report);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        var past = probe.Rays.Where(ray => ray.Met == 2).ToArray();
        past.Should().NotBeEmpty(report);
        var read = past.Sum(ray => Luminance(ray.Light) * ray.Share);
        var held = past.Sum(ray => Luminance(ray.OverCell) * ray.Share);
        (read / held - 1).Should().BeInRange((float)-bound, (float)bound, $"the rays past their reach read the light beyond them\n{report}");
    }

    // The first cascade's probe in the Cornell box's pocket between the floor and the small block, as
    // gi.probe reads it. Its texels' rays, one through each texel's middle, took the lamp's bright
    // spot on the panel's underside for a whole texel, a sixty-fourth of the sphere, its upward face
    // 93% over the path-traced reference's on the development machine's GPU and its faces from 13%
    // over; four rays a texel, one from the middle of each quarter of it, read 7 to 24% over, at a
    // cost to global illumination of 0.10 to 0.19 ms in the Cornell box at 800 by 450 on that GPU and
    // 13 ms of lavapipe's 87 there.
    [NeedsRayQueryFact]
    public void The_First_Cascades_Probe_In_The_Cornell_Boxs_Pocket_Reads_Each_Face_Near_The_Reference()
    {
        var config = Config.Default.WithWindow("bounce rooms", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms();
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(Rooms.Views[0]);
            rooms.Draw();
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(0.6f, 0.6f, 1.8f), 256, -1, 0, out var why);
        probe.Should().NotBeNull(why);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        string[] names = ["+x", "-x", "+y", "-y", "+z", "-z"];
        var shares = Enumerable.Range(0, 6).Select(f => Luminance(probe!.Faces[f]) / Luminance(probe.FacesReference[f]) - 1).ToArray();
        var read = string.Join(", ", Enumerable.Range(0, 6).Select(f => $"{names[f]} {shares[f]:+0.00;-0.00}"));
        shares.Should().OnlyContain(share => Math.Abs(share) < 0.3f, $"each face reads the light its texels hold, not a spot under the lamp for a whole texel, {read}");
    }

    // The first cascade's probe in the Cornell box's pocket, as the test above reads it, its upward
    // face's part from the cascade above, the ways that met nothing within its interval. Read along
    // the same way from where they stand, the probes above lent the ceiling nearer the lamp that
    // they meet, 171% over the path-traced reference on the development machine's GPU; read toward
    // the point of the pocket's ray nearest where their own met a surface, 61% over.
    [NeedsRayQueryFact]
    public void The_Pocket_Probes_Light_From_The_Cascade_Above_Is_Read_Toward_What_Its_Own_Rays_Would_Meet()
    {
        var config = Config.Default.WithWindow("bounce rooms", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms();
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(Rooms.Views[0]);
            rooms.Draw();
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(0.6f, 0.6f, 1.8f), 256, -1, 0, out var why);
        probe.Should().NotBeNull(why);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        var above = probe!.Each.Where(way => way.Met == 2).Select(way => (Weight: Math.Max(way.Way.Y, 0) * way.Share, way.Merged, way.Reference)).ToArray();
        var lent = above.Sum(way => Luminance(way.Merged) * way.Weight) / above.Sum(way => Luminance(way.Reference) * way.Weight);
        lent.Should().BeLessThan(2, $"the probes above are read toward what the pocket's own rays would meet, {lent:0.00} of the reference");
    }

    // The screen probe on the Cornell box's back wall beside the red wall's corner in
    // shaders_bounce_rooms at 800 by 450, which stands in the field's second cascade. Its rays went
    // 1.2 units, half as far as the first cascade's, and read the light beyond them from the second
    // cascade's probe over the tall block, which holds the block's lamp-lit top and the ceiling
    // under the lamp as met before its interval began, so the probe read 86% over the path-traced
    // reference across its hemisphere on the development machine's GPU. Going 2.4 units, they meet
    // the ceiling and the block themselves, and it reads 5% over.
    [NeedsRayQueryFact]
    public void The_Screen_Probe_In_The_Cornell_Boxs_Back_Corner_Reads_Its_Light_Near_The_Reference()
    {
        var probe = ReadScreenProbe(0, 276, 140, out var report);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        (Luminance(probe.Light) / Luminance(probe.Hemisphere) - 1).Should().BeInRange(-0.25f, 0.25f,
            $"the corner's rays reach past the second cascade's near surfaces before they read the light beyond\n{report}");
    }

    // The screen probe on the corridor's wall facing +z, 13 units from its open end, in
    // shaders_bounce_rooms at 800 by 450. The corridor holds no world probe of the second cascade
    // or above, so its first cascade's probes hold nothing along it, and the probe's rays past
    // their reach read nothing, the probe 1% of the reference's light along its ways on the
    // development machine's GPU. Marched on through the field to what they meet, it reads 13%.
    [NeedsRayQueryFact]
    public void A_Screen_Probe_On_The_Corridors_Wall_Takes_The_Light_Of_Its_Open_End()
    {
        var probe = ReadScreenProbe(2, 560, 225, out var report);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        (Luminance(probe.Light) / Luminance(probe.AlongWays)).Should().BeGreaterThan(0.06f,
            $"the rays past their reach go on to the light the open end sends along the corridor\n{report}");
    }

    // The screen probe on the far wall of the carried lamp's room in shaders_bounce_rooms at 800 by
    // 450, two of whose rays pass the thin green wall's end within two of the march's steps behind
    // the depth there. The screen's march took them for meeting the wall's face the eye sees, dark,
    // and the probe read 51% under the reference along its sixteen ways on the development
    // machine's GPU; taken only where the depth does not leap across the step they cross it in, they
    // go on past the wall, and it reads 2% over. With each tile's rays at one of sixteen places in
    // their texels, it reads 22% over, its rays past their reach 34% over the reference along their
    // ways, the merge's parallax, which the sixteen fixed ways' reading under had covered.
    [NeedsRayQueryFact]
    public void A_Screen_Probes_Rays_Passing_Behind_A_Thin_Walls_End_Read_What_Lies_Beyond_It()
    {
        var probe = ReadScreenProbe(7, 600, 200, out var report);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        (Luminance(probe.Light) / Luminance(probe.AlongWays) - 1).Should().BeInRange(-0.15f, 0.3f,
            $"the rays read what they meet past the wall's end, not the wall's face before it\n{report}");
    }

    // The screen probe on the Cornell box's back wall beside the red wall's corner, as the test above
    // reads it. Its rays meeting the red wall within a cell of the white back wall took their color
    // from the field's cells blended, those on the corner's diagonal holding the white wall's, and
    // what the wall sends on of the light straight from the lamp held 0.050 to 0.105 of green to
    // 0.439 to 0.471 of red on the development machine's GPU, where the wall's own color holds 0.012
    // to 0.58; taken from the cells whose face looks the hit's way, it holds 0.007 to 0.008 of green
    // to 0.429 to 0.448 of red, and the hits read 12 to 31% over the reference across the ways they
    // stand for where they read 24 to 139% over.
    [NeedsRayQueryFact]
    public void A_Red_Walls_Rays_Beside_The_White_Wall_It_Meets_Take_The_Red_Walls_Color()
    {
        var probe = ReadScreenProbe(0, 276, 140, out var report);
        var onRed = probe.Rays.Where(ray => ray.Met == 0 && ray.Way.X < -0.3f).ToArray();
        onRed.Should().HaveCountGreaterThanOrEqualTo(4, $"the probe's rays toward the red wall meet it on the screen\n{report}");
        onRed.Should().OnlyContain(ray => ray.Own.Y < 0.05f * ray.Own.X, $"the red wall's hits beside the white wall take the red wall's color\n{report}");
    }

    // The screen probe on the far wall of the carried lamp's room in shaders_bounce_rooms at 800 by
    // 450, the lamp carried past the green wall and the wall moved toward it, as L twice and M leave
    // them. Its ray toward the green wall's end meets the wall's dark face toward the probe, which
    // the eye does not see, and the screen's march took it for meeting the face the eye sees, lit by
    // the lamp, so the ray read 1375% over the reference along it on the development machine's GPU
    // and the face's green lay over the far wall and the floor past the wall's edge. A face met from
    // behind is passed as the depth is, and the field reads the ray 57% under.
    [NeedsRayQueryFact]
    public void A_Screen_Probes_Ray_Meeting_A_Walls_Unseen_Face_Does_Not_Take_The_Face_The_Eye_Sees()
    {
        var probe = ReadScreenProbe(7, 490, 160, out var report, new Vector3(282, 1.2f, -1), moved: true);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        var ray = probe.Rays.MaxBy(ray => ray.Way.X)!;
        (Luminance(ray.Light) / Luminance(ray.Reference)).Should().BeLessThan(2,
            $"the ray toward the green wall's end reads the face it meets, not the lit face the eye sees behind it\n{report}");
    }

    // The ninth room's right corner, where its floor meets the wall, at 800 by 450 at High, each
    // point of the corner the brightest of the pixels around it against the floor and the wall a
    // quarter of a block off it. The glow lights' pass took a texel of the frame before by its
    // distance from the eye alone, and at a corner the other surface's texels lie as far, so the
    // floor took the wall's share of its rough light times its own, a line of light along the
    // corner a texel apart, the points 37% over the floor and the wall beside them on average and
    // 47 of 101 more than 20% on the development machine's GPU; taken only from texels facing as
    // the point does, 4% under, and 4 of 101.
    [NeedsVulkanFact]
    public void A_Corner_Lit_By_Glow_Lights_Carries_No_Line_Of_Light_Along_It()
    {
        var config = Config.Default.WithWindow("bounce rooms", 800, 450) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms();
        var camera = Rooms.Views[8];
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            rooms.Draw();
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        float Luminance(int x, int y)
        {
            var i = (Math.Clamp(y, 0, 449) * 800 + Math.Clamp(x, 0, 799)) * 4;
            return 0.2126f * light[i] + 0.7152f * light[i + 1] + 0.0722f * light[i + 2];
        }
        float Around(Vector3 at, bool brightest)
        {
            var p = GetWorldToScreenEx(at, camera, 800, 450);
            float most = 0, sum = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var l = Luminance((int)p.X + dx, (int)p.Y + dy);
                    (most, sum) = (Math.Max(most, l), sum + l);
                }
            return brightest ? most : sum / 9;
        }
        var over = new List<float>();
        for (float z = -3.5f; z <= 1.5f; z += 0.05f)
            over.Add(Around(new Vector3(323.99f, 0.01f, z), true) / Math.Max(Around(new Vector3(323.75f, 0, z), false), Around(new Vector3(324, 0.25f, z), false)) - 1);
        var said = $"the corner's points {over.Average():+0%;-0%} over the floor and the wall beside them, {over.Count(o => o > 0.2)} of {over.Count} more than 20%";
        over.Average().Should().BeLessThan(0.1f, said);
        over.Count(o => o > 0.2f).Should().BeLessThan(15, said);
    }

    // The screen probe on the strip's room's back wall under the ceiling, over the strip, whose rays
    // that meet the ceiling right above the strip bring back the strip's light there. The march that
    // tells how much of a glow light a hit sees ran beside the wall the strip is mounted on, against
    // a cone as wide as the strip's mean half side, 0.35, and took the wall for something between,
    // and the six such hits read the strip's light 35% under its closed form on the development
    // machine's GPU; against a cone as wide as the strip across the way toward the wall, 0.03, they
    // read it 4% under.
    [NeedsRayQueryFact]
    public void The_Ceiling_Over_A_Strip_On_Its_Wall_Takes_The_Strips_Light_In_Closed_Form()
    {
        var probe = ReadScreenProbe(5, 400, 110, out var report);
        static float Luminance(Vector3 light) => 0.2126f * light.X + 0.7152f * light.Y + 0.0722f * light.Z;
        var over = probe.Rays.Where(ray => ray.Met < 2).Select(ray => (ray.Own, At: probe.Start + ray.Way * ray.Distance))
            .Where(hit => hit.At.Y > 2.8f && MathF.Abs(hit.At.X - 200) <= 1 && hit.At.Z < -1.6f).ToArray();
        over.Should().NotBeEmpty($"the probe's rays meet the ceiling over the strip\n{report}");
        var read = over.Sum(hit => Luminance(hit.Own));
        var exact = over.Sum(hit => Luminance(StripLight(hit.At with { Y = 3 }, -Vector3.UnitY)));
        (read / exact - 1).Should().BeInRange(-0.1f, 0.1f, $"the ceiling over the strip takes its light as its closed form gives it\n{report}");
    }

    // The strip's light at p on the room's white facing n, Lambert's closed form over each face of its
    // box p lies before, in linear light.
    private static Vector3 StripLight(Vector3 p, Vector3 n)
    {
        static float Linear(byte c)
        {
            var v = c / 255f;
            return v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
        }
        var middle = new Vector3(200, 1.5f, -2.45f);
        Vector3[] axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
        float[] halves = [1, 0.03f, 0.03f];
        var seen = 0f;
        for (int k = 0; k < 3; k++)
            for (int side = -1; side <= 1; side += 2)
            {
                var face = middle + axes[k] * (side * halves[k]);
                if (Vector3.Dot(p - face, axes[k]) * side <= 0) continue;
                var (u, v) = (axes[(k + 1) % 3] * halves[(k + 1) % 3], axes[(k + 2) % 3] * halves[(k + 2) % 3]);
                seen += FormFactor(p, n, [face + u + v, face - u + v, face - u - v, face + u - v]);
            }
        var (white, emitted) = (new Color(220, 220, 215), new Color(255, 230, 190));
        return new Vector3(Linear(white.R) * Linear(emitted.R), Linear(white.G) * Linear(emitted.G), Linear(white.B) * Linear(emitted.B)) * 30 * seen;
    }

    // The share of the light a uniformly bright polygon sends a point at p facing n, the polygon
    // clipped to the side n faces.
    private static float FormFactor(Vector3 p, Vector3 n, Vector3[] corners)
    {
        var kept = new List<Vector3>();
        for (int i = 0; i < corners.Length; i++)
        {
            var (a, b) = (corners[i] - p, corners[(i + 1) % corners.Length] - p);
            var (da, db) = (Vector3.Dot(a, n), Vector3.Dot(b, n));
            if (da >= 0) kept.Add(a);
            if (da >= 0 != db >= 0) kept.Add(a + (b - a) * (da / (da - db)));
        }
        if (kept.Count < 3) return 0;
        var sum = Vector3.Zero;
        for (int i = 0; i < kept.Count; i++)
        {
            var (a, b) = (Vector3.Normalize(kept[i]), Vector3.Normalize(kept[(i + 1) % kept.Count]));
            var g = Vector3.Cross(a, b);
            if (g.Length() > 1e-9f) sum += Vector3.Normalize(g) * MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1, 1));
        }
        return MathF.Abs(Vector3.Dot(sum, n)) / (2 * MathF.PI);
    }

    // The window's screen probe under the pixel x, y of a view of shaders_bounce_rooms at 800 by 450
    // at High, its rays written over the last frames once the screen's probes have settled, as
    // gi.screenprobe reads it, with the lines it writes of it.
    private static BounceReference.ScreenProbeLight ReadScreenProbe(int view, int x, int y, out string report, Vector3? lamp = null, bool moved = false)
    {
        var config = Config.Default.WithWindow("bounce rooms", 800, 450) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms(lamp, moved);
        var world = GetApp().World;
        GpuScreenProbes? screen = null;
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 90; frame++)
        {
            if (frame == SceneFieldPlan.SettleFrames + 80)
            {
                screen = world.Resource<Engine.Renderer>().RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!;
                world.Resource<GlobalIlluminationSettings>().LoggedScreenProbe = y / screen.Tile * screen.Across + x / screen.Tile;
            }
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(Rooms.Views[view]);
            rooms.Draw();
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureScreenProbe(world, screen!, 256, -1, out var why);
        probe.Should().NotBeNull(why);
        report = BounceReference.ScreenProbeReport(probe!, screen!.Tile, x, y, 256);
        return probe!;
    }

    // The references' folder beside this file.
    private static string Folder([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "References", "bounce");

    // Light of width by height, channels a pixel, rows from the top, averaged over Down by Down
    // pixels, three channels a pixel, a pixel past the picture's light taken as nothing.
    private static float[] Averaged(float[] light, int width, int height, int channels)
    {
        var (w, h) = (width / Down, height / Down);
        var averaged = new float[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int c = 0; c < 3; c++)
                {
                    float sum = 0;
                    for (int dy = 0; dy < Down; dy++)
                        for (int dx = 0; dx < Down; dx++)
                        {
                            var value = light[((y * Down + dy) * width + x * Down + dx) * channels + c];
                            sum += float.IsFinite(value) ? value : 0;
                        }
                    averaged[(y * w + x) * 3 + c] = sum / (Down * Down);
                }
        return averaged;
    }

    // A PFM file of the averaged light, its rows from the bottom as the format has them and as
    // BounceReference.ReadPfm reads them.
    private static void WritePfm(string path, float[] light)
    {
        var (w, h) = (Width / Down, Height / Down);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        file.Write(Encoding.ASCII.GetBytes($"PF\n{w} {h}\n-1.0\n"));
        for (int y = h - 1; y >= 0; y--)
            file.Write(MemoryMarshal.AsBytes(light.AsSpan(y * w * 3, w * 3)));
    }

    // The rooms, their lights and their views as shaders_bounce_rooms has them, the carried lamp
    // where it starts or at another of its places, and the green wall beside it where it starts or
    // moved.
    private sealed class Rooms : IDisposable
    {
        private readonly Model _slab, _glow, _strip, _lamp;
        private readonly Vector3 _carried;
        private readonly bool _moved;

        // The bright lamp close outside the thin room's right wall.
        public LightHandle Outside { get; }

        public static readonly Camera3D[] Views =
        [
            new(new Vector3(0, 2.5f, 8), new Vector3(0, 2.4f, 0), Vector3.UnitY, 45),
            new(new Vector3(38.3f, 1.4f, 1.6f), new Vector3(41.8f, 1.1f, -0.4f), Vector3.UnitY, 60),
            new(new Vector3(87.5f, 1.4f, 0), new Vector3(72, 1, 0), Vector3.UnitY, 60),
            new(new Vector3(122.6f, 1.6f, 2.6f), new Vector3(118, 0.8f, -1), Vector3.UnitY, 65),
            new(new Vector3(172, 4, 9), new Vector3(162, 0.6f, -5), Vector3.UnitY, 50),
            new(new Vector3(200, 1.4f, 2.3f), new Vector3(200, 1.3f, -2.5f), Vector3.UnitY, 65),
            new(new Vector3(234.5f, 0.35f, 5.5f), new Vector3(245, 0.15f, -5), Vector3.UnitY, 60),
            new(new Vector3(282.6f, 1.7f, 2.6f), new Vector3(278.5f, 1, -1.5f), Vector3.UnitY, 65),
            new(new Vector3(320, 2.6f, 3.2f), new Vector3(320, 0, -0.3f), Vector3.UnitY, 60),
        ];

        public Rooms(Vector3? carried = null, bool moved = false)
        {
            _carried = carried ?? new Vector3(278, 2, -1.5f);
            _moved = moved;
            CreateDirectionalLight(Vector3.Normalize(new Vector3(0.8f, -1, 0.25f)), new Color(255, 245, 230), 1.5f, castsShadows: true);
            CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);
            CreatePointLight(new Vector3(40, 2.2f, 0), Color.White, 0.6f, range: 8);
            Outside = CreatePointLight(new Vector3(43, 1.5f, 0), Color.White, 25, range: 10, castsShadows: true);
            CreatePointLight(new Vector3(240, 2.6f, 0), new Color(255, 236, 210), 14, range: 16);
            CreatePointLight(_carried, new Color(255, 230, 200), 8, range: 10, castsShadows: true);
            _slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _glow.Materials[0].Emissive = new Color(255, 240, 220);
            _glow.Materials[0].EmissiveIntensity = 6;
            _strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _strip.Materials[0].Emissive = new Color(255, 230, 190);
            _strip.Materials[0].EmissiveIntensity = 30;
            _lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            _lamp.Materials[0].Emissive = new Color(255, 200, 120);
            _lamp.Materials[0].EmissiveIntensity = 4;
        }

        public void Draw()
        {
            var white = new Color(220, 220, 215);
            var red = new Color(200, 30, 30);
            var green = new Color(30, 170, 40);
            Box(_slab, new Vector3(0, -0.15f, 0), new Vector3(5.6f, 0.3f, 5.6f), white);
            Box(_slab, new Vector3(0, 5.15f, 0), new Vector3(5.6f, 0.3f, 5.6f), white);
            Box(_slab, new Vector3(0, 2.5f, -2.65f), new Vector3(5.6f, 5.6f, 0.3f), white);
            Box(_slab, new Vector3(-2.65f, 2.5f, 0), new Vector3(0.3f, 5.6f, 5.6f), red);
            Box(_slab, new Vector3(2.65f, 2.5f, 0), new Vector3(0.3f, 5.6f, 5.6f), green);
            Box(_slab, new Vector3(-0.9f, 1.5f, -0.8f), new Vector3(1.4f, 3, 1.4f), white, 20);
            Box(_slab, new Vector3(1, 0.7f, 0.7f), new Vector3(1.4f, 1.4f, 1.4f), white, -18);
            Box(_glow, new Vector3(0, 4.97f, 0), new Vector3(1.6f, 0.06f, 1.6f), Color.White);

            Room(new Vector3(40, 0, 0), new Vector3(4, 3, 4), 0.1f, white);
            Box(_slab, new Vector3(40.8f, 0.5f, -0.6f), new Vector3(1, 1, 1), white);

            Room(new Vector3(80, 0, 0), new Vector3(16, 2.5f, 2), 0.3f, white, openToward: true);

            Box(_slab, new Vector3(120, -0.15f, 0), new Vector3(6.6f, 0.3f, 6.6f), white);
            Box(_slab, new Vector3(120, 3.15f, 0), new Vector3(6.6f, 0.3f, 6.6f), white);
            Box(_slab, new Vector3(120, 1.5f, -3.15f), new Vector3(6.6f, 3, 0.3f), white);
            Box(_slab, new Vector3(120, 1.5f, 3.15f), new Vector3(6.6f, 3, 0.3f), white);
            Box(_slab, new Vector3(123.15f, 1.5f, 0), new Vector3(0.3f, 3, 6), green);
            Box(_slab, new Vector3(116.85f, 0.25f, 0), new Vector3(0.3f, 0.5f, 6), white);
            Box(_slab, new Vector3(116.85f, 2.8f, 0), new Vector3(0.3f, 0.4f, 6), white);
            Box(_slab, new Vector3(116.85f, 1.55f, -2.5f), new Vector3(0.3f, 2.1f, 1), white);
            Box(_slab, new Vector3(116.85f, 1.55f, 2.5f), new Vector3(0.3f, 2.1f, 1), white);

            Box(_slab, new Vector3(165, -0.15f, -6), new Vector3(30, 0.3f, 30), white);
            float[] apart = [1, 3, 6];
            for (int i = 0; i < 3; i++)
            {
                var z = -i * 5f;
                Box(_slab, new Vector3(160, 0.75f, z), new Vector3(1, 1.5f, 1), white);
                Box(_slab, new Vector3(160.5f + apart[i] + 0.15f, 1.5f, z), new Vector3(0.3f, 3, 2.5f), red);
            }

            Room(new Vector3(200, 0, 0), new Vector3(5, 3, 5), 0.3f, white);
            Box(_strip, new Vector3(200, 1.5f, -2.45f), new Vector3(2, 0.06f, 0.06f), Color.White);

            Room(new Vector3(240, 0, 0), new Vector3(12, 3, 12), 0.3f, white);

            Room(new Vector3(280, 0, 0), new Vector3(6, 3, 6), 0.3f, white);
            Box(_slab, new Vector3(277.15f, 1.5f, 0), new Vector3(0.3f, 3, 6), red);
            Box(_slab, new Vector3(_moved ? 281.5f : 280, 1.25f, -1), new Vector3(0.2f, 2.5f, 2.5f), green);
            DrawSphere(_carried, 0.08f, new Color(255, 230, 200));

            var dim = new Color(60, 60, 58);
            Box(_slab, new Vector3(320, -0.15f, 0), new Vector3(8.6f, 0.3f, 8.6f), white);
            Box(_slab, new Vector3(320, 3.15f, 0), new Vector3(8.6f, 0.3f, 8.6f), dim);
            Box(_slab, new Vector3(320, 1.5f, -4.15f), new Vector3(8.6f, 3, 0.3f), dim);
            Box(_slab, new Vector3(320, 1.5f, 4.15f), new Vector3(8.6f, 3, 0.3f), dim);
            Box(_slab, new Vector3(315.85f, 1.5f, 0), new Vector3(0.3f, 3, 8), dim);
            Box(_slab, new Vector3(324.15f, 1.5f, 0), new Vector3(0.3f, 3, 8), dim);
            Box(_lamp, new Vector3(320, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), new Color(230, 190, 110));
            Box(_slab, new Vector3(321.2f, 0.45f, 0), new Vector3(0.15f, 0.9f, 1.2f), dim);
            for (int i = 0; i < 4; i++)
                Box(_lamp, new Vector3(317.7f + (i & 1) * 0.6f, 0.3f, -2.1f + (i >> 1) * 0.6f), new Vector3(0.6f, 0.6f, 0.6f), new Color(230, 190, 110));
        }

        private void Room(Vector3 floor, Vector3 size, float thick, Color color, bool openToward = false)
        {
            var (w, h, d) = (size.X, size.Y, size.Z);
            var t = thick;
            Box(_slab, floor + new Vector3(0, -t / 2, 0), new Vector3(w + 2 * t, t, d + 2 * t), color);
            Box(_slab, floor + new Vector3(0, h + t / 2, 0), new Vector3(w + 2 * t, t, d + 2 * t), color);
            Box(_slab, floor + new Vector3(0, h / 2, -d / 2 - t / 2), new Vector3(w + 2 * t, h, t), color);
            Box(_slab, floor + new Vector3(0, h / 2, d / 2 + t / 2), new Vector3(w + 2 * t, h, t), color);
            if (!openToward) Box(_slab, floor + new Vector3(-w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), color);
            Box(_slab, floor + new Vector3(w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), color);
        }

        private static void Box(Model slab, Vector3 middle, Vector3 size, Color color, float turn = 0) =>
            DrawModelEx(slab, middle, Vector3.UnitY, turn, size, color);

        public void Dispose()
        {
            UnloadModel(_slab);
            UnloadModel(_glow);
            UnloadModel(_strip);
            UnloadModel(_lamp);
        }
    }
}
