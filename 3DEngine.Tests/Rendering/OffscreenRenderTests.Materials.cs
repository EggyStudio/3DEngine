using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Posed_Model_Draws_Where_Its_Bones_Moved_It()
    {
        Open(64, 64);
        var arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
        var model = LoadModel(arm);
        var bend = LoadModelAnimations(arm)[0];
        var camera = new Camera3D(new Vector3(0, 1, 5), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        UpdateModelAnimation(model, bend, 0);
        var rest = Capture(Draw, "rest");
        UpdateModelAnimation(model, bend, bend.KeyframeCount - 1);
        var bent = Capture(Draw, "bent");

        // Pixel (32, 24) is half a unit above the elbow, and (23, 32) six tenths of a unit left of it.
        GetImageColor(rest, 32, 24).R.Should().BeGreaterThan(40, "at rest the upper arm stands above the elbow");
        GetImageColor(rest, 23, 32).R.Should().BeLessThan(10, "and nothing is beside it");
        GetImageColor(bent, 32, 24).R.Should().BeLessThan(10, "bent, the upper arm has left the space above the elbow");
        GetImageColor(bent, 23, 32).R.Should().BeGreaterThan(40, "for the space to its left");
        GetApp().World.Resource<MeshStore>().TryGetData(model.Meshes[0].Id, out var vertices, out _);
        vertices.Max(v => v.Position.Y).Should().BeGreaterThan(1.99f, "the GPU posed it, and the mesh's own vertices stay at rest");

        // Its wires are drawn on the CPU, from the joints the GPU was given.
        var lines = Array.Empty<Vector3>();
        Capture(() =>
        {
            BeginMode3D(camera);
            DrawModelWires(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
            lines = [.. GetApp().World.Resource<DrawList>().Vertices.ToArray().Select(v => v.Position)];
        }, "wires");
        lines.Max(p => p.Y).Should().BeLessThan(1.5f, "the wires follow the bent arm rather than its rest");
        lines.Min(p => p.X).Should().BeLessThan(-0.9f, "and reach out to where the tip went");

        // Posed every frame, the vertices go round a ring of buffers, and each frame draws its own pose.
        for (int i = 0; i < 8; i++)
        {
            var atRest = i % 2 == 0;
            UpdateModelAnimation(model, bend, atRest ? 0 : bend.KeyframeCount - 1);
            var pose = Capture(Draw, $"pose{i}");
            (GetImageColor(pose, 32, 24).R > 40).Should().Be(atRest, $"frame {i} drew the pose given that frame");
        }
        UnloadModel(model);
    }

    [NeedsVulkanFact]
    public void A_Texture_Is_Decoded_From_SRGB_As_A_Color_Is()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var gray = LoadTextureFromImage(GenImageColor(4, 4, new Color(128, 128, 128)));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        // Gray as the material's color, then white with a gray texture. Both are sRGB 128, a
        // fifth of white's light, and a texture read as its bytes would light as half of it.
        cube.Materials[0] = new ModelMaterial(new Color(128, 128, 128));
        var colored = Capture(Draw, "colored");
        cube.Materials[0] = new ModelMaterial(Color.White, gray);
        var textured = Capture(Draw, "textured");

        ((int)GetImageColor(textured, 32, 32).R).Should().BeInRange(GetImageColor(colored, 32, 32).R - 2, GetImageColor(colored, 32, 32).R + 2);
        UnloadModel(cube);
        UnloadTexture(gray);

        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }
    }

    [NeedsVulkanFact]
    public void Emission_Shows_With_No_Light_On_The_Surface()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 0, 0), EmissiveIntensity = 0.5f };
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        // Unlit with no light, and then with one light entity pointing away from the camera's
        // side of the cube, so only the emission reaches it.
        var unlit = Capture(Draw, "unlit");
        var ecs = GetApp().World.Resource<EcsWorld>();
        var away = ecs.Spawn();
        ecs.Add(away, Light.Directional(Vector3.One, 1));
        ecs.Add(away, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), Vector3.One));
        var dark = Capture(Draw, "dark");

        foreach (var image in new[] { unlit, dark })
        {
            Linear(GetImageColor(image, 32, 32).R).Should().BeApproximately(0.5f, 0.03f, "a black cube gives off half of red's light");
            GetImageColor(image, 32, 32).G.Should().BeLessThan(5);
        }
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void Occlusion_Darkens_The_Light_From_All_Around_And_Not_A_Lamp()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var crease = LoadTextureFromImage(GenImageColor(4, 4, new Color(64, 64, 64)));
        var ecs = GetApp().World.Resource<EcsWorld>();
        var light = ecs.Spawn();
        ecs.Add(light, Light.Ambient(Vector3.One, 0.5f));
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        cube.Materials[0] = new ModelMaterial(Color.White) { Roughness = 1 };
        var open = Capture(Draw, "open");
        cube.Materials[0] = cube.Materials[0] with { OcclusionMap = crease };
        var occluded = Capture(Draw, "occluded");

        // The same under a lamp facing the cube, where occlusion leaves the light alone.
        ecs.GetRef<Light>(light) = Light.Directional(Vector3.One, 0.5f);
        var lamp = Capture(Draw, "lamp");
        cube.Materials[0] = cube.Materials[0] with { OcclusionMap = default };
        var lampOpen = Capture(Draw, "lamp-open");

        // The map's red, 64, is a quarter. An occlusion map is data rather than a color, so it is
        // read as it is and not decoded from sRGB.
        Linear(GetImageColor(occluded, 32, 32).R).Should().BeApproximately(Linear(GetImageColor(open, 32, 32).R) * 0.25f, 0.02f);
        ((int)GetImageColor(lamp, 32, 32).R).Should().BeInRange(GetImageColor(lampOpen, 32, 32).R - 2, GetImageColor(lampOpen, 32, 32).R + 2);
        UnloadModel(cube);
        UnloadTexture(crease);
    }

    [NeedsVulkanFact]
    public void The_Sky_Is_Drawn_Behind_Everything_In_The_Direction_Looked()
    {
        Open(64, 64);
        // A sky red above the horizon and blue below, with a sharp edge between, wide enough that
        // its cube keeps the edge.
        var image = GenImageColor(1024, 512, new Color(0, 0, 255));
        ImageDrawRectangle(ref image, 0, 0, 1024, 256, new Color(255, 0, 0));
        SetEnvironmentMap(image);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        Image Look(Vector3 target, string name, bool skybox = true) => Capture(() =>
        {
            ClearBackground(Color.Green);
            BeginMode3D(new Camera3D(Vector3.Zero, target, Vector3.UnitY + Vector3.UnitZ * 0.01f, 60));
            DrawModel(cube, target * 3, 1, Color.White);
            if (skybox) DrawSkybox();
            EndMode3D();
        }, name);

        var up = Look(Vector3.UnitY, "up");
        var down = Look(-Vector3.UnitY, "down");
        var bare = Look(Vector3.UnitY, "bare", skybox: false);

        var sky = GetImageColor(up, 2, 2);
        (sky.R > 200 && sky.B < 40).Should().BeTrue($"looking up shows the red sky, not {sky}");
        var ground = GetImageColor(down, 2, 2);
        (ground.B > 200 && ground.R < 40).Should().BeTrue($"looking down shows the blue below, not {ground}");
        GetImageColor(up, 32, 32).Should().NotBe(sky, "the cube hides the sky behind it");
        GetImageColor(bare, 2, 2).Should().Be(Color.Green, "with no DrawSkybox the clear color shows");
        UnloadModel(cube);
        UnloadEnvironmentMap();
    }

    [NeedsVulkanFact]
    public void A_Smooth_Metal_Reflects_The_Environment_And_A_Rough_Surface_Is_Lit_By_The_Half_It_Faces()
    {
        Open(64, 64);
        // A sky red above the horizon and blue below, and no light entities.
        var sky = GenImageColor(64, 32, new Color(0, 0, 255));
        ImageDrawRectangle(ref sky, 0, 0, 64, 16, new Color(255, 0, 0));
        SetEnvironmentMap(sky);

        // A plane facing up, seen from straight above, so it mirrors the sky overhead.
        var camera = new Camera3D(new Vector3(0, 4, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        var plane = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        plane.Materials[0] = new ModelMaterial(Color.White) { Metallic = 1, Roughness = 0.05f };
        var mirror = Capture(Draw, "mirror");
        plane.Materials[0] = new ModelMaterial(Color.White) { Metallic = 0, Roughness = 1 };
        var chalk = Capture(Draw, "chalk");

        var m = GetImageColor(mirror, 32, 32);
        Linear(m.R).Should().BeGreaterThan(0.6f, "a mirror facing up shows the red sky above it");
        Linear(m.B).Should().BeLessThan(0.05f, "and none of the blue below the horizon");
        var c = GetImageColor(chalk, 32, 32);
        Linear(c.R).Should().BeGreaterThan(0.3f, "a rough white surface facing up is lit by the red half of the sky");
        c.B.Should().BeLessThan(20, "and the blue half below the horizon is behind it, where the cosine gives it nothing");
        UnloadModel(plane);
        UnloadEnvironmentMap();
    }

    [NeedsVulkanFact]
    public void Draws_Differing_Only_In_Color_Share_One_Set_And_Keep_Their_Colors()
    {
        Open(64, 32);
        var camera = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        // A row of cubes, each a shade of its own, and the two ends red and blue.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            for (int i = 0; i < 300; i++)
            {
                var color = i == 0 ? new Color(255, 0, 0) : i == 299 ? new Color(0, 0, 255) : new Color((byte)i, (byte)(i / 2), 0);
                // The ends 10 units away, 3 either side of the middle, and the rest far behind.
                var at = i == 0 ? new Vector3(-3, 0, 0) : i == 299 ? new Vector3(3, 0, 0) : new Vector3(0, 0, -20 - i);
                DrawModel(cube, at, 1, color);
            }
            EndMode3D();
        });

        var renderer = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>();
        renderer.MaterialSetCount.Should().Be(1, "every cube has the same maps, and its factors reach it in its instance");
        renderer.DrawCalls.Should().Be(1, "the cubes share a mesh and its maps, so one instanced draw draws them all");
        var left = GetImageColor(image, 20, 16);
        var right = GetImageColor(image, 44, 16);
        (left.R > 100 && left.B < 20).Should().BeTrue($"the leftmost cube is red, not {left}");
        (right.B > 100 && right.R < 20).Should().BeTrue($"the rightmost cube is blue, not {right}");
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Translucent_Draw_Blends_With_What_Was_Drawn_Before_It_Though_Its_Mesh_Came_First()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var small = LoadModelFromMesh(GenMeshCube(1, 1, 0.1f));
        var wall = LoadModelFromMesh(GenMeshCube(4, 4, 0.1f));

        // An opaque quad of the small mesh off to the side, then a red wall behind, then a half
        // clear blue quad of the small mesh in front of the wall's middle.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(small, new Vector3(1.5f, 1.5f, 0), 1, Color.White);
            DrawModel(wall, new Vector3(0, 0, -1), 1, new Color(255, 0, 0));
            DrawModel(small, new Vector3(0, 0, 0.5f), 1, new Color(0, 0, 255, 128));
            EndMode3D();
        });

        var middle = GetImageColor(image, 32, 32);
        middle.R.Should().BeGreaterThan(40, $"the red wall shows through the glass, not {middle}");
        middle.B.Should().BeGreaterThan(40, $"and the glass tints it blue, not {middle}");
        UnloadModel(wall);
        UnloadModel(small);
    }

    // A red wall, then a blue quad of the given texture and material in front of its middle, in a
    // 64 by 64 frame opened before the texture was loaded.
    private Image WallBehind(Texture2D texture, MaterialAlphaMode mode)
    {
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshCube(4, 4, 0.1f));
        // One face, since a box's back face maps the texture mirrored behind its front.
        var front = LoadModelFromMesh(GenMeshPlane(1.5f, 1.5f, 1, 1));
        front.Materials[0] = new ModelMaterial(new Color(0, 0, 255), texture) { AlphaMode = mode };
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(front, new Vector3(0, 0, 0.5f), Vector3.UnitX, 90, Vector3.One, Color.White);
            DrawModel(wall, new Vector3(0, 0, -1), 1, new Color(255, 0, 0));
            EndMode3D();
        });
        UnloadModel(front);
        UnloadModel(wall);
        return image;
    }

    [NeedsVulkanFact]
    public void A_Masked_Surface_Shows_What_Is_Behind_Its_Holes_And_Hides_It_Elsewhere()
    {
        Open(64, 64);
        // The texture's left column below the cutoff and its right column above it, but neither solid.
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 60, 255, 255, 255, 200], 2, 1));
        SetTextureFilter(texture, TextureFilter.Point);

        var image = WallBehind(texture, MaterialAlphaMode.Mask);

        var hole = GetImageColor(image, 26, 32);
        var solid = GetImageColor(image, 38, 32);
        (hole.R > 40 && hole.B < 10).Should().BeTrue($"the wall shows through the cut-out half unmixed, not {hole}");
        (solid.B > 40 && solid.R < 10).Should().BeTrue($"the masked half is solid blue, not {solid}");
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Blended_Surface_Mixes_A_Textures_Alpha_With_What_Is_Behind()
    {
        Open(64, 64);
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 128], 1, 1));

        var image = WallBehind(texture, MaterialAlphaMode.Blend);

        var middle = GetImageColor(image, 32, 32);
        (middle.R > 40 && middle.B > 40).Should().BeTrue($"the half-clear texture mixes blue with the red behind, not {middle}");
        UnloadTexture(texture);
    }

    // A triangle around the origin wound counterclockwise seen from +Z, glTF's front, with a
    // material that is single-sided or not.
    private string SidedTriangle(bool doubleSided)
    {
        var bytes = new List<byte>();
        foreach (var f in new float[] { -1, -1, 0, 1, -1, 0, 0, 1, 0 }) bytes.AddRange(BitConverter.GetBytes(f));
        var json = $$$"""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
             "materials":[{"doubleSided":{{{(doubleSided ? "true" : "false")}}}}],
             "meshes":[{"primitives":[{"attributes":{"POSITION":0},"material":0}]}],
             "buffers":[{"byteLength":36,"uri":"data:application/octet-stream;base64,{{{Convert.ToBase64String(bytes.ToArray())}}}"}],
             "bufferViews":[{"buffer":0,"byteLength":36}],
             "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[-1,-1,0],"max":[1,1,0]}]}
            """;
        var path = _folder.File($"sided-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, json);
        return path;
    }

    [NeedsVulkanFact]
    public void A_Single_Sided_Material_Hides_Its_Back_Faces_And_A_Double_Sided_One_Does_Not()
    {
        Open(32, 32);
        var single = LoadModel(SidedTriangle(doubleSided: false));
        var both = LoadModel(SidedTriangle(doubleSided: true));
        single.Materials[0].DoubleSided.Should().BeFalse("the file says so");
        both.Materials[0].DoubleSided.Should().BeTrue("the file says so");
        byte Middle(Model model, float z) => GetImageColor(Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, z), Vector3.Zero, Vector3.UnitY, 60));
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }), 16, 18).R;

        Middle(single, 3).Should().BeGreaterThan(20, "its front faces the camera");
        Middle(single, -3).Should().Be(0, "from behind, its back face is left out");
        Middle(both, -3).Should().BeGreaterThan(0, "a double-sided one is drawn from behind too");
        UnloadModel(single);
        UnloadModel(both);
    }

    [NeedsVulkanFact]
    public void Anisotropic_Filtering_Keeps_A_Slanted_Checkerboard_Sharper_Than_Bilinear()
    {
        Open(64, 64);
        var checker = LoadTextureFromImage(GenImageChecked(256, 256, 8, 8, Color.Black, Color.White));
        GenTextureMipmaps(ref checker);
        var floor = LoadModelFromMesh(GenMeshPlane(40, 40, 1, 1));
        floor.Materials[0] = new ModelMaterial(Color.White, checker);
        // Low over the floor, looking along it, so the far squares are squashed many times over.
        var camera = new Camera3D(new Vector3(0, 0.6f, 10), new Vector3(0, 0, -20), Vector3.UnitY, 45);

        // How much the far rows of the floor still vary, from a gray smear to sharp squares.
        double Contrast(TextureFilter filter)
        {
            SetTextureFilter(checker, filter);
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(floor, Vector3.Zero, 1, Color.White);
                EndMode3D();
            }, $"slant {filter}");
            var values = new List<double>();
            for (int y = 30; y < 34; y++)
                for (int x = 8; x < 56; x++)
                    values.Add(GetImageColor(image, x, y).R);
            var mean = values.Average();
            return Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
        }

        var bilinear = Contrast(TextureFilter.Bilinear);
        var anisotropic = Contrast(TextureFilter.Anisotropic16x);
        anisotropic.Should().BeGreaterThan(bilinear * 1.2, $"16 samples along the slant keep the squares apart, {anisotropic:0.0} against {bilinear:0.0}");
        UnloadModel(floor);
        UnloadTexture(checker);
    }

    [NeedsVulkanFact]
    public void Blend_Modes_Combine_Colors_As_Raylib_Has_Them()
    {
        Open(64, 16, samples: 1);
        var image = Capture(() =>
        {
            ClearBackground(new Color(50, 50, 50));
            BeginBlendMode(BlendMode.AddColors);
            DrawRectangle(0, 0, 16, 16, new Color(100, 0, 0));
            BeginBlendMode(BlendMode.SubtractColors);
            DrawRectangle(16, 0, 16, 16, new Color(200, 200, 200));
            BeginBlendMode(BlendMode.Multiplied);
            DrawRectangle(32, 0, 16, 16, new Color(128, 255, 0));
            EndBlendMode();
            DrawRectangle(48, 0, 16, 16, new Color(255, 0, 0, 128));
        });

        static void Near(Color actual, Color expected, string because)
        {
            (Math.Abs(actual.R - expected.R) <= 2 && Math.Abs(actual.G - expected.G) <= 2 && Math.Abs(actual.B - expected.B) <= 2)
                .Should().BeTrue($"{because}, {expected} where {actual} was drawn");
        }
        Near(GetImageColor(image, 8, 8), new Color(150, 50, 50), "added colors sum");
        Near(GetImageColor(image, 24, 8), new Color(150, 150, 150), "subtracted colors take what is there from the color");
        Near(GetImageColor(image, 40, 8), new Color(25, 50, 0), "multiplied colors scale what is there");
        Near(GetImageColor(image, 56, 8), new Color(152, 25, 25), "alpha lays the color over by half");
    }

    [NeedsVulkanFact]
    public void A_Scissor_Keeps_Drawing_To_Its_Rectangle_Until_It_Ends()
    {
        Open(64, 32, samples: 1);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginScissorMode(8, 8, 16, 16);
            DrawRectangle(0, 0, 64, 32, Color.White);
            EndScissorMode();
            DrawRectangle(40, 0, 8, 8, Color.White);
        });

        GetImageColor(image, 12, 12).Should().Be(Color.White);
        GetImageColor(image, 4, 4).Should().Be(Color.Black, "the rectangle covers the window, and the scissor keeps it out of here");
        GetImageColor(image, 30, 12).Should().Be(Color.Black);
        GetImageColor(image, 44, 4).Should().Be(Color.White, "what is drawn after the scissor ends covers the whole window again");
    }

    [NeedsVulkanFact]
    public void A_Clamped_Texture_Stretches_Its_Edge_Where_A_Repeating_One_Tiles()
    {
        Open(64, 16, samples: 1);
        var image = GenImageColor(2, 1, new Color(255, 0, 0));
        ImageDrawPixel(ref image, 1, 0, new Color(0, 0, 255));
        var texture = LoadTextureFromImage(image);
        SetTextureFilter(texture, TextureFilter.Point);

        // The source runs two widths of the texture across, so the right half of the strip is past
        // its edge, or the left half where it starts two texels before the texture.
        Image Strip(TextureWrap wrap, float from = 0)
        {
            SetTextureWrap(texture, wrap);
            return Capture(() =>
            {
                ClearBackground(Color.Black);
                DrawTexturePro(texture, new Rectangle(from, 0, 4, 1), new Rectangle(0, 0, 64, 16), Vector2.Zero, 0, Color.White);
            }, $"wrap {wrap} from {from}");
        }

        var repeat = Strip(TextureWrap.Repeat);
        GetImageColor(repeat, 40, 8).Should().Be(new Color(255, 0, 0), "the texture starts again past its edge");
        var clamp = Strip(TextureWrap.Clamp);
        GetImageColor(clamp, 40, 8).Should().Be(new Color(0, 0, 255), "the edge pixel goes on");
        GetImageColor(clamp, 8, 8).Should().Be(new Color(255, 0, 0));
        var mirror = Strip(TextureWrap.MirrorRepeat);
        GetImageColor(mirror, 40, 8).Should().Be(new Color(0, 0, 255), "the mirrored copy starts with the edge it meets");
        // Before the texture, a mirror clamp shows it mirrored across its edge at 0, where a clamp
        // stretches that edge, and past its far edge it clamps as well.
        var mirrorClamp = Strip(TextureWrap.MirrorClamp, -2);
        GetImageColor(mirrorClamp, 8, 8).Should().Be(new Color(0, 0, 255), "three quarters of a width before 0 mirrors to three quarters in");
        GetImageColor(Strip(TextureWrap.Clamp, -2), 8, 8).Should().Be(new Color(255, 0, 0));
        GetImageColor(Strip(TextureWrap.MirrorClamp), 56, 8).Should().Be(new Color(0, 0, 255), "past the far edge the edge pixel goes on");
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Metal_Reflects_An_HDR_Sky_Brighter_Than_White_Could_Be()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 4, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        var plane = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        // A dark mirror, which returns a tenth of what it reflects.
        plane.Materials[0] = new ModelMaterial(new Color(89, 89, 89)) { Metallic = 1, Roughness = 0.05f };
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        Vector3[] Sky(float overhead) =>
            Enumerable.Range(0, 32 * 16).Select(i => i / 32 < 8 ? new Vector3(overhead) : Vector3.Zero).ToArray();

        GetApp().World.InsertResource(EnvironmentMap.FromLinear(Sky(1), 32, 16));
        var white = Capture(Draw, "white-sky");
        GetApp().World.InsertResource(EnvironmentMap.FromLinear(Sky(8), 32, 16));
        var bright = Capture(Draw, "bright-sky");

        Linear(GetImageColor(white, 32, 32).R).Should().BeApproximately(0.1f, 0.03f, "a white sky gives a tenth of white");
        Linear(GetImageColor(bright, 32, 32).R).Should().BeGreaterThan(0.6f, "a sky eight times white gives eight tenths, which no eight-bit sky can");
        UnloadModel(plane);
        UnloadEnvironmentMap();
    }

    // How many pixels along a white triangle's diagonal edge on black are neither black nor white,
    // drawn into the window and into a render target drawn beside it.
    private (int Window, int Target) EdgeShades(int samples)
    {
        Open(128, 32, samples);
        var target = LoadRenderTexture(64, 32);
        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            DrawTriangle(new Vector2(0, 0), new Vector2(0, 32), new Vector2(64, 32), Color.White);
            EndTextureMode();

            ClearBackground(Color.Black);
            DrawTriangle(new Vector2(0, 0), new Vector2(0, 32), new Vector2(64, 32), Color.White);
            DrawTexture(target.Texture, 64, 0, Color.White);
        }, $"edges-{samples}");
        UnloadRenderTexture(target);
        CloseWindow();
        UseApp(null);

        bool Between(Color c) => c.R is > 30 and < 225;
        return (Count(image, 0, 0, 64, 32, Between), Count(image, 64, 0, 128, 32, Between));
    }

    [NeedsVulkanFact]
    public void Multisampling_Shades_The_Pixels_An_Edge_Crosses_In_The_Window_And_A_Target()
    {
        var single = EdgeShades(1);
        var four = EdgeShades(4);

        single.Window.Should().Be(0, "with one sample a pixel is in the triangle or not");
        single.Target.Should().Be(0);
        four.Window.Should().BeGreaterThan(20, "with four a pixel the edge crosses is partly covered");
        four.Target.Should().BeGreaterThan(20, "and a render target is multisampled as the window is");
    }

    [NeedsVulkanFact]
    public void A_Flat_Point_Light_Lights_A_Model_In_Its_Color()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0] = new ModelMaterial(Color.White) { Roughness = 1 };
        var lamp = CreatePointLight(new Vector3(0, 0, 1.5f), new Color(255, 0, 0), 2);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        });

        var c = GetImageColor(image, 32, 32);
        c.R.Should().BeGreaterThan(150, "the red lamp a unit in front of the face lights it");
        c.G.Should().BeLessThan(20, "and in red only, since it is the only light");
        UnloadLight(lamp);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_2D_Camera_Draws_The_World_Around_Its_Target()
    {
        Open(64, 32);
        var camera = new Camera2D(new Vector2(32, 16), new Vector2(1000, 500), Zoom: 2);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode2D(camera);
            DrawRectangle(1000, 500, 4, 4, new Color(255, 0, 0));
            EndMode2D();
            DrawRectangle(0, 0, 4, 4, new Color(0, 0, 255));
        });

        GetImageColor(image, 34, 18).Should().Be(new Color(255, 0, 0), "the target's square is at the offset, twice its size");
        GetImageColor(image, 2, 2).Should().Be(new Color(0, 0, 255), "after EndMode2D drawing is in screen pixels again");
    }

    [NeedsVulkanFact]
    public void Characters_Past_U_FFFF_Are_Drawn_From_A_Font_File()
    {
        Open(160, 60);
        var font = LoadFontEx(Engine.Tests.Api.TrueTypeFontTests.Planes, 40, ['A', 0x1F600, 0x1F7E0]);
        font.Glyphs.Keys.Should().Contain([0x1F600, 0x1F7E0], "both are baked past the atlas builder's plane");
        var text = "A" + char.ConvertFromUtf32(0x1F600) + char.ConvertFromUtf32(0x1F7E0);
        MeasureTextEx(font, text, 40, 0).X.Should().BeApproximately(120, 1, "three glyphs 40 pixels across each");

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTextEx(font, text, new Vector2(0, 0), 40, 0, Color.White);
        }, "planes");

        // The square's glyph spans x 44 to 76 from the pen at 40, its hole 52 to 68, with its top
        // 32 pixels above a baseline 33 down.
        GetImageColor(image, 46, 20).R.Should().BeGreaterThan(200, "the square's edge is drawn");
        GetImageColor(image, 60, 20).R.Should().BeLessThan(40, "its hole is left open");
        GetImageColor(image, 100, 18).R.Should().BeGreaterThan(200, "the circle is drawn after it");
        UnloadFont(font);
    }
}
