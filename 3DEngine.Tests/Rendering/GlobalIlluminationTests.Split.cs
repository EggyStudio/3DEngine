using System.Numerics;
using FluentAssertions;
using RadianceCascades;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The world's probes split as the RadianceCascades module lays them out (gi.layout split).
public sealed partial class GlobalIlluminationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Split_The_Store_s_Places_Move_By_Whole_Repeats_Of_Every_Key_As_The_Frame_s_Origin_Moves()
    {
        // The store's origin stands at the frame's rounded to the keys' repeat, so the frame's
        // origin against it moves with the frame's own and jumps by a whole repeat where it is
        // rounded the other way, and a point's cell keeps its key's nine low bits along each axis
        // at every spacing across that jump.
        var shape = GlobalIlluminationRenderer.SplitShape;
        var repeat = GpuSplitStore.Repeat(shape);
        repeat.Should().Be(512 * 0.25 * 32, "the coarsest spacing is the finest's twice over for each cascade above");
        GpuSplitStore.OriginAgainst(shape, new RenderOrigin(16, -32, 0)).Should().Be(new Vector3(16, -32, 0));
        var (before, after) = (new RenderOrigin(repeat / 2 - 16, 0, 0), new RenderOrigin(repeat / 2 + 16, 0, 0));
        var (near, far) = (GpuSplitStore.OriginAgainst(shape, before), GpuSplitStore.OriginAgainst(shape, after));
        (far.X - near.X).Should().Be((float)(32 - repeat), "the store's origin moves a whole repeat as the frame's crosses half of one");
        var world = repeat / 2 + 3.3;
        for (int cascade = 0; cascade < shape.Cascades; cascade++)
        {
            var spacing = shape.Spacing * Math.Pow(2, cascade);
            int Low(RenderOrigin origin, Vector3 against) => (int)Math.Floor((world - origin.X + against.X) / spacing + 0.5) & 511;
            Low(after, far).Should().Be(Low(before, near), $"a point keeps its probe's key at cascade {cascade}");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Split_The_Store_s_Every_Buffer_Binds_Whole_Within_The_Least_Storage_Range_A_Device_Holds()
    {
        // Lavapipe holds the least range Vulkan lets a device bind a storage buffer over, which the
        // store's light taken whole at the engine's shape is larger than, so its probes' own light
        // and the merged stand apart, each half the light the module lays out.
        var shape = GlobalIlluminationRenderer.SplitShape;
        var bytes = GpuSplitStore.Bytes(shape);
        bytes.Should().AllSatisfy(b => b.Should().BeLessThanOrEqualTo(GpuSplitStore.LeastStorageRange));
        bytes.Aggregate(0UL, (a, b) => a + b).Should().Be((ulong)shape.Bytes, "the buffers hold the store the module lays out");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Split_A_Floor_Under_An_Even_Sky_Gathers_The_Sky_Through_The_Field_And_The_Device_s_Rays_And_The_Sky_Gathers_None()
    {
        // A floor under a sky of 1 and nothing else takes the whole sky over its upward half, so
        // each pixel of it gathers 1 once the split probes about it have settled, at Low through
        // the field's march and at High through the device's rays where it traces them. A pixel of
        // the sky holds no surface and gathers none, the model pass reading its faces there.
        foreach (var quality in new[] { GlobalIllumination.Low, GlobalIllumination.High })
        {
            Open();
            SetAmbientLight(Color.White, 1);
            SetGlobalIllumination(quality);
            var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
            settings.Layout = ProbeLayout.Split;
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            void Floor() => DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(40, 1, 40), Color.Gray);
            Capture(Floor);
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var device = (GraphicsDevice)renderer.Context.Graphics!;
            var lit = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!;
            lit.Split.Should().NotBeNull($"the split layout keeps its store at {quality}");
            var surface = lit.Surface!;
            var light = GlobalIlluminationStages.Of(device).ReadSurfaceGather(surface);
            float At(uint x, uint y) => light[(y * surface.Width + x) * 4];
            var (width, height) = (surface.Width, surface.Height);
            var floor = Enumerable.Range(0, (int)width).Select(x => At((uint)x, height - 4)).ToArray();
            var sky = Enumerable.Range(0, (int)width).Select(x => At((uint)x, 2)).ToArray();
            output.WriteLine($"{quality} rays {lit.Rays is not null} floor {floor.Min():F4} to {floor.Max():F4} sky {sky.Min():F4} to {sky.Max():F4}");
            sky.Should().AllSatisfy(v => v.Should().Be(-1, "a pixel of the sky holds no surface"));
            // Within the gathered light's half floats' steps about 1, a two-thousandth below it,
            // and where the field's march carries the light, at Low or where the device traces no
            // rays, as on its CPU, losing a share of the sky the device's rays keep in the row
            // nearest the eye, within 0.015 over the 0.0127 this test measured.
            var bound = quality == GlobalIllumination.Low || lit.Rays is null ? 0.015f : 0.001f;
            floor.Should().AllSatisfy(v => v.Should().BeApproximately(1, bound, $"a pixel of the floor gathers the sky at {quality}"));
            settings.Layout = ProbeLayout.Dense;
            Capture(Floor);
            lit.Split.Should().BeNull("the dense layout lets the store go");
            UnloadModel(slab);
            CloseWindow();
            UseApp(null);
        }
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Split_A_Closed_Room_Of_Glowing_Black_Walls_Gathers_What_The_Grid_s_Probes_Gather_Through_The_Field_And_The_Device_s_Rays()
    {
        // Every way from a point of the room's walls meets a wall that glows and reflects nothing,
        // and the split's rays shade what they meet as the grid's probes shade their rays' hits, so
        // the window's pixels gather on the whole what the module's gather takes from the grid's
        // probes (gi.surface), at Low through the field's march and at High through the device's
        // rays where it traces them, each pixel a surface. The bound is 10% of the grid's light,
        // over the 6.8% this test measured at Low and the 6.4% at High.
        foreach (var quality in new[] { GlobalIllumination.Low, GlobalIllumination.High })
        {
            Open();
            SetSceneField(3, 0.25f, 2);
            SetSceneFieldCenter(new Vector3(0, 1.5f, 0));
            SetAmbientLight(Color.Black, 0);
            SetGlobalIllumination(quality);
            var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            slab.Materials[0] = new ModelMaterial(Color.Black);
            slab.Materials[0].Emissive = Color.White;
            slab.Materials[0].EmissiveIntensity = 1;
            void Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
                DrawModelEx(slab, new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2, Vector3.UnitY, 0, new Vector3(x1 - x0, y1 - y0, z1 - z0), Color.White);
            void Room()
            {
                Box(-2.3f, 2.3f, -0.3f, 0, -2.3f, 2.3f);
                Box(-2.3f, 2.3f, 3, 3.3f, -2.3f, 2.3f);
                Box(-2.3f, -2, 0, 3, -2.3f, 2.3f);
                Box(2, 2.3f, 0, 3, -2.3f, 2.3f);
                Box(-2, 2, 0, 3, -2.3f, -2);
                Box(-2, 2, 0, 3, 2, 2.3f);
            }
            var camera = new Camera3D(new Vector3(0, 1.5f, 1.6f), new Vector3(0, 1.4f, -2), Vector3.UnitY, 70);
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var device = (GraphicsDevice)renderer.Context.Graphics!;
            var lit = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!;
            float[] Gathered()
            {
                Capture(Room, camera);
                var light = GlobalIlluminationStages.Of(device).ReadSurfaceGather(lit.Surface!);
                return Enumerable.Range(0, light.Length / 4).Select(i => light[i * 4]).ToArray();
            }
            settings.Layout = ProbeLayout.Split;
            var split = Gathered();
            settings.Layout = ProbeLayout.Dense;
            settings.SurfaceGather = true;
            var grid = Gathered();
            output.WriteLine($"{quality} rays {lit.Rays is not null} split {split.Min():F4} to {split.Max():F4} mean {split.Average():F4}, grid mean {grid.Average():F4}");
            split.Should().AllSatisfy(v => v.Should().BeGreaterThan(0, $"each pixel of the room holds a surface that gathers the glow at {quality}"));
            split.Average().Should().BeApproximately(grid.Average(), 0.1f * grid.Average(), $"the split's rays shade the glow as the grid's probes do at {quality}");
            UnloadModel(slab);
            CloseWindow();
            UseApp(null);
        }
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Split_A_Floor_s_Gather_Read_By_Its_Causes_Where_Asked_Takes_Its_Standing_Probes_By_The_Share_They_Let_Through()
    {
        // With the causes asked for, each pixel of a floor under an even sky reads the probes about
        // its point standing, its weights by the share they let through toward it no more than by
        // their trilinear weights alone and its youngest probe taken some frames before, while its
        // light is gathered as without them. A pixel of the sky holds no surface and reads -1
        // throughout, and with the flag cleared the gather holds no image of the causes.
        Open();
        SetAmbientLight(Color.White, 1);
        SetGlobalIllumination(GlobalIllumination.Low);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        settings.Layout = ProbeLayout.Split;
        settings.SplitCauseRead = true;
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        void Floor() => DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(40, 1, 40), Color.Gray);
        Capture(Floor);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var stages = GlobalIlluminationStages.Of((GraphicsDevice)renderer.Context.Graphics!);
        var surface = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Surface!;
        surface.Causes.Should().NotBeNull("the gather holds its causes where they are asked for");
        var (causes, light) = (stages.ReadSplitCauses(surface), stages.ReadSurfaceGather(surface));
        var (width, height) = ((int)surface.Width, (int)surface.Height);
        Vector4 At(int x, int y) => new(causes[(y * width + x) * 4], causes[(y * width + x) * 4 + 1], causes[(y * width + x) * 4 + 2], causes[(y * width + x) * 4 + 3]);
        var floor = Enumerable.Range(0, width).Select(x => At(x, height - 4)).ToArray();
        var sky = Enumerable.Range(0, width).Select(x => At(x, 2)).ToArray();
        output.WriteLine($"floor x {floor.Min(c => c.X):F4} to {floor.Max(c => c.X):F4}, y {floor.Min(c => c.Y):F4} to {floor.Max(c => c.Y):F4}, "
            + $"z to {floor.Max(c => c.Z):F4}, w {floor.Min(c => c.W)} to {floor.Max(c => c.W)}, counts {SplitCauses.Count(causes, width * height)}");
        sky.Should().AllSatisfy(c => c.Should().Be(new Vector4(-1), "a pixel of the sky holds no surface"));
        floor.Should().AllSatisfy(c =>
        {
            c.Y.Should().BeGreaterThan(0, "the probes about a point of the floor stand and face it");
            c.X.Should().BeInRange(0, c.Y + 1e-3f, "a probe lends by the share it lets through no more than by its trilinear weight");
            c.Z.Should().BeInRange(0, 1, "the share lent from behind the plane is a share");
            c.W.Should().BeGreaterThan(0, "the probes were taken in the frames before");
        });
        // The light within the bound the floor's own test measured at Low.
        Enumerable.Range(0, width).Select(x => light[((height - 4) * width + x) * 4]).Should()
            .AllSatisfy(v => v.Should().BeApproximately(1, 0.015f, "the floor gathers the sky as without its causes read"));
        settings.SplitCauseRead = false;
        Capture(Floor);
        renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Surface!.Causes.Should().BeNull("the gather holds no causes once none are asked for");
        UnloadModel(slab);
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Split_Causes_Count_Each_Pixel_Seen_By_The_First_Cause_It_Meets_And_Write_Their_Rows_From_The_Bottom()
    {
        // Six pixels, three across and two down, one unseen, one the store does not hold, one
        // taking the trilinear weights alone, one fresh and lent from behind its plane, which
        // counts as fresh, one lent from behind alone and one meeting no cause, written as
        // gi.compare writes a frame, its rows from the bottom.
        float[] causes = [-1, -1, -1, -1, 0, 0, 0, -1, 0, 0.5f, 0, 20, 0.4f, 0.5f, 0.6f, 3, 0.4f, 0.5f, 0.6f, 40, 0.5f, 0.5f, 0, 40];
        SplitCauses.Count(causes, 6).Should().Be((5, 1, 1, 1, 1));
        var path = Path.Combine(_folder.Path, "causes");
        SplitCauses.Write(path, causes, 3, 2).Should().StartWith("5 pixels seen, 1 gathering nothing, 1 taking their probes by their trilinear weights alone, 1 holding");
        var (width, height, weights) = BounceReference.ReadPfm(path + "-weights.pfm");
        (width, height).Should().Be((3, 2));
        weights.Should().Equal(Enumerable.Range(0, 6).SelectMany(i => causes.Skip(4 * i).Take(3)), "the weights keep the rows from the top once read");
        var ages = File.ReadAllBytes(path + "-age.pfm");
        var header = "Pf\n3 2\n-1.0\n"u8.ToArray();
        ages.Take(header.Length).Should().Equal(header);
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(ages.AsSpan(header.Length)).ToArray().Should()
            .Equal([3f, 40, 40, -1, -1, 20], "the ages' rows run from the bottom");
    }
}
