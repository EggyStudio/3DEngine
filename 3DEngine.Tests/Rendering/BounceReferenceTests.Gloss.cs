using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The reference's gloss, the first face's and every face's, held to planes and a wall whose light
// is known.
public sealed partial class BounceReferenceTests
{
    [NeedsRayQueryTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Smooth_Metal_Plane_Under_The_Sky_Reads_The_Sky_Times_Its_Fresnel(bool every)
    {
        // A metal of a linear 0.87 reflects the sky by its Fresnel, its color at a square view and
        // more of the sky toward a grazing one. Read on the development machine's GPU it reads 0.943
        // with the first face's gloss and with every face's, the plane being the first face.
        var gloss = every ? ReferenceGloss.Every : ReferenceGloss.First;
        var (plane, sky) = PlaneUnderTheSky(new Color(240, 240, 240), metallic: 1, roughness: 0.05f, gloss);
        (plane / sky).Should().BeInRange(0.87f, 1.0f, $"the metal plane reads {plane / sky:0.000} of the sky");
    }

    [NeedsRayQueryTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Rough_Dielectric_At_A_Grazing_View_Reads_Fresnels_Share_Of_The_Sky_Over_Its_Diffuse(bool every)
    {
        // A gray of a linear half under the sky reads its diffuse share, a half less what its
        // Fresnel takes, and the gloss its Fresnel reflects at a grazing view of a tenth, about 0.15
        // at a roughness of a half, as the bridge's reference reads such a plane at 0.59 to 0.72. It
        // reads 0.658 on the development machine's GPU with the first face's gloss and every face's.
        var gloss = every ? ReferenceGloss.Every : ReferenceGloss.First;
        var (plane, sky) = PlaneUnderTheSky(new Color(188, 188, 188), metallic: 0, roughness: 0.5f, gloss);
        (plane / sky).Should().BeInRange(0.59f, 0.72f, $"the gray plane reads {plane / sky:0.000} of the sky");
    }

    [NeedsRayQueryTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Metal_Wall_Under_The_Sun_Mirrors_It_Onto_The_Floor_Beside_It_Where_Every_Face_Is_Glossy(bool every)
    {
        // A white metal wall four units high stands on a white floor, the sun at 45 degrees coming
        // toward it, so the wall mirrors the sun back onto the floor within four units of its foot at
        // the same slant. With every face glossy the floor there takes the sun twice, less what the
        // wall's roughness of 0.3 scatters past it, and reads 1.906 times the floor six to eight
        // units off on the development machine's GPU. With the first face's gloss alone the wall is
        // a face after the first, lit and going on as a white diffuse face, and the floor by it
        // takes a fifth more, 1.204.
        var gloss = every ? ReferenceGloss.Every : ReferenceGloss.First;
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        SetAmbientLight(Color.Black, 0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -1, 0)), Color.White, 1, castsShadows: true);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(Color.White) { Roughness = 1 };
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        wall.Materials[0] = new ModelMaterial(Color.White) { Metallic = 1, Roughness = 0.3f };
        var camera = new Camera3D(new Vector3(6, 3.5f, 7), new Vector3(3.5f, 0, 0), Vector3.UnitY, 60);
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(floor, new Vector3(5, -0.1f, 0), Vector3.UnitY, 0, new Vector3(10, 0.2f, 6), Color.White);
            DrawModelEx(wall, new Vector3(-0.1f, 2, 0), Vector3.UnitY, 0, new Vector3(0.2f, 4, 6), Color.White);
            EndMode3D();
            EndDrawing();
        }
        var path = Path.Combine(_folder.Path, $"mirrored-{gloss}.png");
        BounceReference.Trace(GetApp().World, path, 256, 1, gloss).Should().StartWith("traced");
        var (width, height, light) = BounceReference.ReadPfm(path + ".pfm");
        float Mean(float x0, float x1)
        {
            var (sum, count) = (0.0, 0);
            for (float x = x0; x <= x1; x += 0.25f)
                for (float z = -1.5f; z <= 1.5f; z += 0.25f)
                {
                    var at = GetWorldToScreenEx(new Vector3(x, 0, z), camera, width, height);
                    var (px, py) = ((int)at.X, (int)at.Y);
                    if (px < 0 || py < 0 || px >= width || py >= height) continue;
                    sum += light[(py * width + px) * 3 + 1];
                    count++;
                }
            count.Should().BeGreaterThan(20, $"the floor from {x0} to {x1} is in view");
            return (float)(sum / count);
        }
        var near = Mean(1, 3);
        var far = Mean(6, 8);
        (near / far).Should().BeInRange(every ? 1.8f : 1.1f, every ? 2.05f : 1.3f, $"the floor by the wall reads {near:0.0000} and away from it {far:0.0000}");
        UnloadModel(floor);
        UnloadModel(wall);
    }

    // The reference's mean light over a plane four units a side lying at the origin, seen from six
    // units off and a little over it at a grazing view, under a sky of one light and no sun, and
    // over the sky beyond it, paths meeting the plane and going on once.
    private (float Plane, float Sky) PlaneUnderTheSky(Color color, float metallic, float roughness, ReferenceGloss gloss)
    {
        var config = Config.Default.WithWindow("bounce reference test", 128, 128) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        SetAmbientLight(Color.White, 1);
        var plane = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        plane.Materials[0] = new ModelMaterial(color) { Metallic = metallic, Roughness = roughness };
        var camera = new Camera3D(new Vector3(0, 0.6f, 6), new Vector3(0, 0.3f, 0), Vector3.UnitY, 45);
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(plane, new Vector3(0, -0.05f, 0), Vector3.UnitY, 0, new Vector3(4, 0.1f, 4), Color.White);
            EndMode3D();
            EndDrawing();
        }
        var path = Path.Combine(_folder.Path, $"plane-{gloss}.png");
        BounceReference.Trace(GetApp().World, path, 256, 1, gloss).Should().StartWith("traced");
        var (width, height, light) = BounceReference.ReadPfm(path + ".pfm");
        var regions = MemoryMarshal.Cast<byte, uint>(File.ReadAllBytes(path + ".regions")).ToArray();
        var (planePixels, planeLight, skyPixels, skyLight) = (0, 0.0, 0, 0.0);
        for (int i = 0; i < width * height; i++)
        {
            // A region is a copy's index times eight and the axis its face turns toward most, plus
            // one, the plane's top +y, the third.
            if (regions[i] == 0)
                (skyPixels, skyLight) = (skyPixels + 1, skyLight + light[i * 3 + 1]);
            else if ((regions[i] - 1) % 8 == 2)
                (planePixels, planeLight) = (planePixels + 1, planeLight + light[i * 3 + 1]);
        }
        planePixels.Should().BeGreaterThan(200, "the plane is in view");
        skyPixels.Should().BeGreaterThan(200, "the sky is in view");
        UnloadModel(plane);
        return ((float)(planeLight / planePixels), (float)(skyLight / skyPixels));
    }
}
