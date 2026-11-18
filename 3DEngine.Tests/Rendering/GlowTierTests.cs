using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Which glow lights a screen probe marches to by their faces' halves, those that give an eighth of
/// the light the probe sees of them all or more, so a light hidden from the probe moves no other
/// into the fainter lights' sum.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlowTierTests : IDisposable
{
    private const int Size = 192;
    private const int Around = 5;
    private readonly TestFolder _folder = new("engine-glow-tier-");

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Floor_Keeps_A_Near_Lamps_Light_Beside_A_Stronger_Lamp_Hidden_Behind_A_Wall()
    {
        // Four small lamps a meter from a point of the floor, each giving it near twice what a lamp
        // two meters off the fifth way gives, which sends the point some eighth of the light it
        // sees. A block ten meters off that same way, behind a wall five off that hides it from the
        // point, would send the point more than the fifth lamp does were it seen. The point takes
        // the fifth lamp's light whether the far block glows or not. Taken of the lights' light
        // unhidden, the far block's raises the sum the eighth is taken of and the fifth lamp falls
        // to the fainter lights at every probe, and the floor takes some 3% less with it lit.
        var with = PointLight(farGlows: true, GlobalIllumination.Low);
        var without = PointLight(farGlows: false, GlobalIllumination.Low);
        (with / without).Should().BeApproximately(1, 0.02,
            $"the floor keeps the near lamp's light, {with:0.0000} with the far block lit behind the wall and {without:0.0000} without");
    }

    [NeedsRayQueryFact]
    public void The_Floor_By_Five_Lamps_And_A_Hidden_One_Reads_As_Its_Reference()
    {
        // The same floor at Low with the far block lit and dark, each against a
        // path-traced reference of 16384 paths a pixel at the same camera, in which the far block
        // behind its wall sends the point nothing. Taken of the lights' light unhidden, the floor
        // with the far block lit reads some 7% under.
        foreach (var farGlows in new[] { true, false })
        {
            var frame = PointLight(farGlows, GlobalIllumination.Low);
            var reference = PointLight(farGlows, GlobalIllumination.High, Path.Combine(_folder.Path, $"floor-{farGlows}.png"));
            (frame / reference).Should().BeApproximately(1, 0.05,
                $"the floor reads {frame:0.0000} where its reference reads {reference:0.0000}, the far block {(farGlows ? "lit" : "dark")}");
        }
    }

    // The light on the floor at the point under a camera over it, the mean of the frame's linear
    // light over the 11 by 11 pixels about it, or with a path given, of the reference traced there.
    // Over so many the reference's mean stands within 0.3% of one traced at four times the paths,
    // where over 3 by 3 it climbs with the paths as the rarer ones find the small lamps.
    private static double PointLight(bool farGlows, GlobalIllumination quality, string? traced = null)
    {
        var config = Config.Default.WithWindow("glow tier", Size, Size) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 4);
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var small = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        small.Materials[0] = new ModelMaterial(Color.Black) { Emissive = Color.White, EmissiveIntensity = 2 };
        var near = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        near.Materials[0] = new ModelMaterial(Color.Black) { Emissive = Color.White, EmissiveIntensity = 4 };
        var far = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        far.Materials[0] = new ModelMaterial(Color.Black) { Emissive = Color.White, EmissiveIntensity = 8 };
        var camera = new Camera3D(new Vector3(0, 6, 0), Vector3.Zero, -Vector3.UnitZ, 40);
        // The near lamp and the far block the same way from the point, toward +x and a little +z,
        // and the four small lamps toward the other ways across the floor.
        var way = Vector3.Normalize(new Vector3(1, 0, 0.3f));
        var turn = -float.RadiansToDegrees(MathF.Atan2(way.Z, way.X));
        Vector3[] smallAt = [new(-1, 0.5f, 0), new(0, 0.5f, -1), new(-0.707f, 0.5f, -0.707f), new(-0.707f, 0.5f, 0.707f)];
        for (int frame = 0; frame < 60; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(cube, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(64, 1, 64), Color.White);
            foreach (var at in smallAt) DrawModelEx(small, at, Vector3.UnitY, 0, new Vector3(0.2f), Color.White);
            DrawModelEx(near, way * 2 + new Vector3(0, 1, 0), Vector3.UnitY, 0, new Vector3(0.2f), Color.White);
            DrawModelEx(cube, way * 5 + new Vector3(0, 2, 0), Vector3.UnitY, turn, new Vector3(0.4f, 4, 6), Color.White);
            if (farGlows) DrawModelEx(far, way * 10 + new Vector3(0, 5, 0), Vector3.UnitY, 0, Vector3.One, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var point = GetWorldToScreen(Vector3.Zero, camera);
        float[] light;
        if (traced is null)
        {
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var rgba = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            light = new float[Size * Size * 3];
            for (int i = 0; i < Size * Size; i++)
                for (int c = 0; c < 3; c++)
                    light[i * 3 + c] = rgba[i * 4 + c];
        }
        else
        {
            BounceReference.Trace(GetApp().World, traced, 16384).Should().StartWith("traced");
            light = BounceReference.ReadPfm(traced + ".pfm").Light;
        }
        double sum = 0;
        for (int dy = -Around; dy <= Around; dy++)
            for (int dx = -Around; dx <= Around; dx++)
            {
                int p = (((int)MathF.Round(point.Y) + dy) * Size + (int)MathF.Round(point.X) + dx) * 3;
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            }
        UnloadModel(cube);
        UnloadModel(small);
        UnloadModel(near);
        UnloadModel(far);
        CloseWindow();
        UseApp(null);
        return sum / ((2 * Around + 1) * (2 * Around + 1));
    }
}
