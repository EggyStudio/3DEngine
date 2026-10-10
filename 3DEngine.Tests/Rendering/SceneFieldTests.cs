using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The scene's distance field: where its cascades lie and what each frame builds and stamps, on
/// the CPU, and the distances the GPU builds read back against those to the meshes they were built
/// from.
/// </summary>
[Collection("Engine3D")]
public sealed class SceneFieldTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static readonly ModelVertex[] Cube = GenMeshCubeVertices(2);

    // A cube's eight corners as vertices, which is all the plan reads of a mesh.
    private static ModelVertex[] GenMeshCubeVertices(float size) =>
        [.. Enumerable.Range(0, 8).Select(c => new ModelVertex(new Vector3((c & 1) == 0 ? -size / 2 : size / 2, (c & 2) == 0 ? -size / 2 : size / 2,
            (c & 4) == 0 ? -size / 2 : size / 2), Vector3.UnitY, Vector2.Zero))];

    private static (SceneFieldPlan.Instance, bool) At(Vector3 position, ModelVertex[]? vertices = null) =>
        (new SceneFieldPlan.Instance(1, vertices ?? Cube, Matrix4x4.CreateTranslation(position), false), false);

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Cascade_Lies_Around_The_Eye_And_Moves_Only_Past_Its_Grid()
    {
        var plan = new SceneFieldPlan(2, 0.25f, 2);
        plan.Update(new Vector3(0.1f, 2, 6), []);

        plan.BuiltOrigin(0).Should().Be(new Vector3(-8, -6, -2), "the corner is 32 cells of 0.25 short of the eye's 2-unit grid point");
        plan.BuiltOrigin(1).Should().Be(new Vector3(-16, -16, -12), "the second cascade's cells and grid are twice as wide");
        plan.Builds.Select(build => build.Cascade).Should().Equal(0, 1);

        plan.Update(new Vector3(1.9f, 2, 6), []);
        plan.Builds.Should().BeEmpty("the eye has not left the 2-unit cell of the grid");
        plan.Update(new Vector3(2.1f, 2, 6), []);
        plan.Builds.Select(build => build.Cascade).Should().Equal([0], "the first cascade's grid is passed, the second's 4-unit one not");
        plan.BuiltOrigin(0).Should().Be(new Vector3(-6, -6, -2));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Mesh_Drawn_The_Same_For_Eight_Frames_Is_Built_And_Stamped_Until_Then()
    {
        var plan = new SceneFieldPlan(1, 0.25f, 1);
        var eye = new Vector3(0, 2, 6);
        for (int frame = 1; frame < SceneFieldPlan.SettleFrames; frame++)
        {
            plan.Update(eye, [At(Vector3.Zero)]);
            plan.StillCount.Should().Be(0);
            plan.Shapes.Should().ContainSingle("a mesh not yet still is stamped as its box");
        }
        plan.Update(eye, [At(Vector3.Zero)]);
        plan.StillCount.Should().Be(1);
        plan.Builds.Should().ContainSingle().Which.Instances.Should().ContainSingle("the cascade it is in is built again with it");
        plan.Shapes.Should().BeEmpty("the build holds it");

        var moved = Vector3.UnitX;
        plan.Update(eye, [At(moved)]);
        plan.StillCount.Should().Be(0, "a mesh drawn somewhere else is another, which has not settled");
        plan.Builds.Should().ContainSingle().Which.Instances.Should().BeEmpty("the cascade it left is built again without it");
        plan.Shapes.Should().ContainSingle().Which.ToOwn.Translation.Should().Be(-moved);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Budget_Builds_The_Finest_Dirty_Cascades_First()
    {
        var plan = new SceneFieldPlan(3, 0.25f, 1);
        plan.Update(Vector3.Zero, []);
        plan.Builds.Select(build => build.Cascade).Should().Equal([0], "one cascade a frame");
        plan.BuiltOrigin(1).Should().BeNull("the second waits its turn");
        plan.Update(Vector3.Zero, []);
        plan.Builds.Select(build => build.Cascade).Should().Equal(1);
        plan.Update(Vector3.Zero, []);
        plan.Builds.Select(build => build.Cascade).Should().Equal(2);
        plan.Update(Vector3.Zero, []);
        plan.Builds.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Moving_Mesh_Is_Stamped_Into_The_Bricks_Around_It_And_They_Are_Put_Back_When_It_Leaves()
    {
        var plan = new SceneFieldPlan(1, 0.25f, 1);
        plan.Update(Vector3.Zero, []);
        plan.Update(Vector3.Zero, [At(new Vector3(0.1f, 0, 0))]);

        // The cube's 2 units and the band's 1 on each side, over bricks of one unit from the
        // corner at -8, are bricks 6 to 10 along each axis.
        plan.Bricks.Should().HaveCount(5 * 5 * 5);
        plan.Bricks.Should().Contain((0, 6, 6, 6)).And.Contain((0, 10, 10, 10));
        var stamped = plan.Bricks.ToHashSet();

        plan.Update(Vector3.Zero, []);
        plan.Shapes.Should().BeEmpty();
        plan.Bricks.Should().BeEquivalentTo(stamped, "the bricks it was stamped into go back to the still field");
        plan.Update(Vector3.Zero, []);
        plan.Bricks.Should().BeEmpty("and need nothing after");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Each_Brick_Is_Given_The_Shapes_That_Come_Within_It_Alone()
    {
        // Three moving cubes in a row along X, their middles four units apart, whose bands meet in
        // the bricks between them, so a brick reads one or two of them and never the three.
        var plan = new SceneFieldPlan(1, 0.25f, 1);
        plan.Update(Vector3.Zero, []);
        plan.Update(Vector3.Zero, [At(new Vector3(-4, 0, 0)), At(new Vector3(4, 0, 0)), At(new Vector3(0, 0, 0))]);

        IEnumerable<int> Of(int brick) => plan.BrickShapes.Skip(plan.BrickRanges[brick].First).Take(plan.BrickRanges[brick].Count);
        var index = plan.Bricks.FindIndex(b => b == (0, 4, 8, 8));
        Of(index).Should().ContainSingle("the brick at x from -4 to -3 meets the left cube's alone");
        plan.Bricks.Select((_, b) => Of(b).Count()).Should().OnlyContain(n => n >= 1 && n <= 2, "no brick reads every shape");
        plan.BrickShapes.Should().HaveCount(plan.Bricks.Select((_, b) => Of(b).Count()).Sum());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void An_Edge_Is_Open_Where_No_Other_Triangle_Shares_Its_Ends_Whatever_Vertices_Hold_Them()
    {
        // A quad of two triangles, open at its four sides and closed along its diagonal, and the
        // same quad with each triangle keeping vertices of its own, as a cube's faces do.
        ModelVertex At(float x, float z) => new(new Vector3(x, 0, z), Vector3.UnitY, Vector2.Zero);
        ModelVertex[] shared = [At(0, 0), At(1, 0), At(1, 1), At(0, 1)];
        SceneFieldRenderer.OpenEdges(shared, [0, 1, 2, 0, 2, 3]).Should().Equal([true, true, false, false, true, true]);
        ModelVertex[] apart = [At(0, 0), At(1, 0), At(1, 1), At(0, 0), At(1, 1), At(0, 1)];
        SceneFieldRenderer.OpenEdges(apart, [0, 1, 2, 3, 4, 5]).Should().Equal([true, true, false, false, true, true]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Scene_Drawn_In_The_Same_Order_Is_Read_By_Place_And_Planned_As_One_Drawn_In_Another_Order_Each_Frame()
    {
        // Three meshes that settle, one of which moves at frame 12 and settles again, another left
        // out from frame 25, and a skinned figure whose limbs are posed afresh each frame, drawn
        // to one plan in the same order every frame and to another turned a place each frame,
        // which never reads by place, so its plans are those of working each frame out whole.
        var (byPlace, whole) = (new SceneFieldPlan(2, 0.25f, 2), new SceneFieldPlan(2, 0.25f, 2));
        var eye = new Vector3(0.3f, 0.2f, 0.1f);
        for (int frame = 0; frame < 32; frame++)
        {
            var limbs = new[] { new SceneFieldPlan.Part(new SceneFieldPlan.Box(new Vector3(-0.2f), new Vector3(0.2f)), Matrix4x4.CreateTranslation(0, frame * 0.01f, 0)) };
            List<(SceneFieldPlan.Instance, bool)> drawn =
            [
                At(new Vector3(1, 0, 0)),
                .. frame < 25 ? [At(new Vector3(-2, 0, 1))] : Array.Empty<(SceneFieldPlan.Instance, bool)>(),
                At(frame < 12 ? new Vector3(0, 1, -2) : new Vector3(0.5f, 1, -2)),
                (new SceneFieldPlan.Instance(2, Cube, Matrix4x4.CreateTranslation(2, 0, 2), false, Parts: limbs), true),
            ];
            byPlace.Update(eye, drawn);
            whole.Update(eye, [.. drawn.Skip(frame % drawn.Count), .. drawn.Take(frame % drawn.Count)]);

            if (frame is > 0 and not 12 and not 25) byPlace.ReadByPlace.Should().BeTrue($"frame {frame} draws what the one before did, in its order");
            whole.ReadByPlace.Should().Be(frame % drawn.Count == (frame - 1) % drawn.Count && frame is > 0 and not 12 and not 25, "the other is turned a place each frame");
            byPlace.StillCount.Should().Be(whole.StillCount, $"frame {frame}");
            byPlace.Builds.Select(b => (b.Cascade, string.Join(";", b.Instances.Select(i => i.World.Translation.ToString()).Order(StringComparer.Ordinal))))
                .Should().Equal(whole.Builds.Select(b => (b.Cascade, string.Join(";", b.Instances.Select(i => i.World.Translation.ToString()).Order(StringComparer.Ordinal)))), $"frame {frame}");
            byPlace.Shapes.Select(shape => shape.ToOwn.Translation).Should().BeEquivalentTo(whole.Shapes.Select(shape => shape.ToOwn.Translation), $"frame {frame}");
            byPlace.Bricks.Should().BeEquivalentTo(whole.Bricks, $"frame {frame}");
        }
        byPlace.StillCount.Should().Be(2, "the mesh that moved has settled again and the one left out is gone");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Figures_Past_What_A_Frame_Stamps_Are_Boxes_Beyond_The_Nearest_And_None_Is_Left_Out()
    {
        // Thirty skinned figures of twenty limbs each, one after another away from the eye, where
        // a frame stamps 256 boxes, so the nearest take their limbs while the rest still have room for
        // a box each, eleven of them, and the nineteen beyond are a box each.
        var limbs = Enumerable.Range(0, 20).Select(j => new SceneFieldPlan.Part(new SceneFieldPlan.Box(new Vector3(-0.1f), new Vector3(0.1f)),
            Matrix4x4.CreateTranslation(0, j * 0.1f, 0))).ToArray();
        var plan = new SceneFieldPlan(1, 0.25f, 1);
        plan.Update(Vector3.Zero, [.. Enumerable.Range(0, 30).Select(i =>
            (new SceneFieldPlan.Instance(1, Cube, Matrix4x4.CreateTranslation(i * 3 + 3, 0, 0), false, Parts: limbs), true))]);

        plan.Shapes.Should().HaveCount(11 * 20 + 19);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Moving_Mesh_That_Does_Not_Bend_Is_Stamped_As_The_Parts_Its_Triangles_Are_Cut_Into()
    {
        // An L of two boxes, one along X and one standing on its end, each face a grid of four by
        // four squares as a modeled mesh is cut finer than its shape, whose one box would fill the
        // corner between its arms.
        var (vertices, indices) = (new List<ModelVertex>(), new List<uint>());
        void Face(Vector3 corner, Vector3 across, Vector3 up)
        {
            var first = (uint)vertices.Count;
            for (int j = 0; j <= 4; j++)
                for (int i = 0; i <= 4; i++)
                    vertices.Add(new ModelVertex(corner + across * (i / 4f) + up * (j / 4f), Vector3.UnitY, Vector2.Zero));
            for (uint j = 0; j < 4; j++)
                for (uint i = 0; i < 4; i++)
                {
                    uint a = first + j * 5 + i, b = a + 1, c = a + 6, d = a + 5;
                    indices.AddRange([a, b, c, a, c, d]);
                }
        }
        void Box(Vector3 min, Vector3 max)
        {
            var size = max - min;
            var (x, y, z) = (new Vector3(size.X, 0, 0), new Vector3(0, size.Y, 0), new Vector3(0, 0, size.Z));
            Face(min, x, y);
            Face(min + z, x, y);
            Face(min, x, z);
            Face(min + y, x, z);
            Face(min, y, z);
            Face(min + x, y, z);
        }
        Box(Vector3.Zero, new Vector3(3, 1, 1));
        Box(new Vector3(0, 1, 0), new Vector3(1, 3, 1));

        var parts = SceneFieldRenderer.Cut([.. vertices], [.. indices]);

        parts.Should().NotBeNull().And.HaveCountGreaterThan(1);
        bool Inside(Vector3 point) => parts!.Any(part => Vector3.Clamp(point, part.Rest.Min, part.Rest.Max) == point);
        vertices.Where(v => !Inside(v.Position)).Should().BeEmpty("every corner of the mesh is in a part");
        Inside(new Vector3(2.5f, 2.5f, 0.5f)).Should().BeFalse("and the corner between its arms in none");
        SceneFieldRenderer.Cut([.. vertices], [.. indices.Take(3 * 15)]).Should().BeNull("a mesh of fewer than sixteen triangles is its one box");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Skinned_Mesh_Is_Held_As_The_Boxes_Of_Its_Joints_Where_Its_Pose_Puts_Them()
    {
        // The arm of arm.gltf bent at its elbow, its forearm along -X at the elbow's height, which
        // the field held as the box around the arm standing upright at rest.
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.1f);
        var arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
        var model = LoadModel(arm);
        var clip = LoadModelAnimations(arm)[0];
        var camera = new Camera3D(new Vector3(0, 1, 4), new Vector3(0, 1, 0), Vector3.UnitY, 45, CameraProjection.Perspective);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            UpdateModelAnimation(model, clip, clip.KeyframeCount - 1);
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;
        float At(Vector3 point)
        {
            var cell = Vector3.Clamp((point - origin) / 0.1f, Vector3.Zero, new Vector3(SceneFieldPlan.Resolution - 1));
            var (x, y, z) = ((int)cell.X, (int)cell.Y, (int)cell.Z);
            return distances[(z * SceneFieldPlan.Resolution + y) * SceneFieldPlan.Resolution + x];
        }

        At(new Vector3(-0.6f, 1, 0)).Should().BeLessThan(0.05f, "the forearm lies along -X at the elbow's height");
        At(new Vector3(0, 1.75f, 0)).Should().BeGreaterThan(0.15f, "and no longer stands above the elbow");
        At(new Vector3(0, 0.5f, 0)).Should().BeLessThan(0.05f, "where the upper arm stays");
        UnloadModel(model);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Field_Built_On_The_GPU_Holds_The_Distances_To_Its_Meshes()
    {
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        var camera = new Camera3D(new Vector3(0, 2, 6), Vector3.Zero, Vector3.UnitY, 45, CameraProjection.Perspective);

        // Settled, built, and the build finished on the GPU before it is read.
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            BeginMode3D(camera);
            DrawModel(cube, new Vector3(0.3f, 0.6f, -0.2f), 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        fields.Plan!.StillCount.Should().Be(1);
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var distances = device.ReadSceneField(fields.Field!);

        // Each cell against the distance from its middle to the cube's surface, below zero inside,
        // which is exact within the band and held at the band beyond.
        var origin = fields.Plan.BuiltOrigin(0)!.Value;
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.25f, band = SceneFieldPlan.Band * cell;
        var center = new Vector3(0.3f, 0.6f, -0.2f);
        int near = 0, far = 0;
        float worst = 0;
        for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = origin + (new Vector3(x, y, z) + new Vector3(0.5f)) * cell - center;
                    var q = Vector3.Abs(p) - Vector3.One;
                    var expected = Vector3.Max(q, Vector3.Zero).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0);
                    var found = distances[(z * size + y) * size + x];
                    if (MathF.Abs(expected) < band - 0.01f)
                    {
                        near++;
                        worst = MathF.Max(worst, MathF.Abs(found - expected));
                    }
                    else if (expected > band + 0.01f)
                    {
                        far++;
                        found.Should().BeApproximately(band, 0.01f, "a cell past the band holds the band's distance");
                    }
                }
        near.Should().BeGreaterThan(1000);
        far.Should().BeGreaterThan(100_000);
        worst.Should().BeLessThan(0.01f, "within the band each cell holds its distance to the nearest face, as half a float holds it");

        // The cells near the cube are painted its gray, decoded to linear, and those past the band
        // are not painted at all.
        var (albedo, glow) = device.ReadSceneFieldColors(fields.Field!);
        int painted = 0, bare = 0;
        for (int i = 0; i < distances.Length; i++)
        {
            if (MathF.Abs(distances[i]) < 0.5f)
            {
                albedo[i * 4 + 3].Should().Be(1);
                albedo[i * 4].Should().BeApproximately(0.216f, 0.01f, "Color.Gray's 130 is 0.22 in linear light");
                glow[i * 4].Should().Be(0);
                painted++;
            }
            else if (distances[i] > band - 0.01f && albedo[i * 4 + 3] == 0) bare++;
        }
        painted.Should().BeGreaterThan(1000);
        bare.Should().BeGreaterThan(100_000);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Field_Paints_A_Mesh_In_Its_Vertices_Colors_As_The_Model_Pass_Draws_Them()
    {
        // One white mesh of two boxes a unit wide, the left one's vertices red and the right one's
        // green, as a game meshes blocks of many colors into one draw.
        var config = Config.Default.WithWindow("scene field test", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var cube = GenMeshCube(1, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(cube.Id, out var vertices, out var indices).Should().BeTrue();
        ModelVertex[] both = [.. vertices.Select(v => v with { Position = v.Position - new Vector3(1.5f, 0, 0) }),
            .. vertices.Select(v => v with { Position = v.Position + new Vector3(1.5f, 0, 0) })];
        uint[] joined = [.. indices, .. indices.Select(i => i + (uint)vertices.Length)];
        Color[] colors = [.. vertices.Select(_ => Color.Red), .. vertices.Select(_ => Color.Green)];
        var mesh = UploadMesh(both, joined, colors, null);
        var camera = new Camera3D(new Vector3(0, 2, 6), Vector3.Zero, Vector3.UnitY, 45, CameraProjection.Perspective);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            BeginMode3D(camera);
            DrawMesh(mesh, new ModelMaterial(Color.White), Matrix4x4.Identity);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        fields.Plan!.StillCount.Should().Be(1);
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var distances = device.ReadSceneField(fields.Field!);
        var (albedo, _) = device.ReadSceneFieldColors(fields.Field!);

        // Each cell near the surface by its side: red by the left box, green by the right.
        var origin = fields.Plan.BuiltOrigin(0)!.Value;
        const int size = SceneFieldPlan.Resolution;
        int reds = 0, greens = 0;
        for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var i = (z * size + y) * size + x;
                    if (MathF.Abs(distances[i]) >= 0.3f || albedo[i * 4 + 3] == 0) continue;
                    var (r, g) = (albedo[i * 4], albedo[i * 4 + 1]);
                    if (origin.X + (x + 0.5f) * 0.25f < 0)
                    {
                        r.Should().BeApproximately(0.79f, 0.02f, "the left box's vertices are Color.Red, whose 230 is 0.79 in linear light");
                        g.Should().BeLessThan(0.05f);
                        reds++;
                    }
                    else
                    {
                        g.Should().BeApproximately(0.77f, 0.02f, "the right box's vertices are Color.Green, whose 228 is 0.77 in linear light");
                        r.Should().BeLessThan(0.05f);
                        greens++;
                    }
                }
        reds.Should().BeGreaterThan(100);
        greens.Should().BeGreaterThan(100);
        UnloadMesh(mesh);
        UnloadMesh(cube);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Moving_Mesh_Is_Stamped_In_Its_Color_Times_Its_Vertices_Mean_Color()
    {
        var plan = new SceneFieldPlan(1, 0.25f, 1, tint: _ => new Vector3(0.5f, 0.25f, 1));
        plan.Update(Vector3.Zero, []);
        plan.Update(Vector3.Zero, [(new SceneFieldPlan.Instance(1, Cube, Matrix4x4.Identity, false, Color: new Vector3(0.8f)), false)]);
        plan.Shapes.Should().ContainSingle().Which.Color.Should().Be(new Vector3(0.4f, 0.2f, 0.8f));
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(16)]
    [InlineData(48)]
    public void A_Sphere_Holds_Its_Distances_On_Every_Side_Its_Poles_Too(int rings)
    {
        // GenMeshSphere's last row of triangles meets its pole at corners the rounding of sin(pi)
        // leaves a hair apart, so some there have next to no area and a face turned any way; taken
        // as faces, they turned the cells outside the pole inside, 143 of the 1662 within half a
        // unit of the sphere at 16 rings and 97 at 48, their distances 0.86 to 0.90 off, as this
        // test measured on an RTX 4070.
        var config = Config.Default.WithWindow("sphere field", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.15f);
        var sphere = LoadModelFromMesh(GenMeshSphere(0.6f, rings, rings));
        var camera = new Camera3D(new Vector3(0, 1, 4), Vector3.Zero, Vector3.UnitY, 45);
        var center = new Vector3(0.1f, 0.6f, 0.05f);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(sphere, center, 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.15f;
        int near = 0, turned = 0;
        float worst = 0;
        for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = origin + (new Vector3(x, y, z) + new Vector3(0.5f)) * cell;
                    var expected = Vector3.Distance(p, center) - 0.6f;
                    if (MathF.Abs(expected) > 0.5f) continue;
                    var found = distances[(z * size + y) * size + x];
                    near++;
                    if (MathF.Sign(found) != MathF.Sign(expected) && MathF.Abs(expected) > 0.1f) turned++;
                    worst = MathF.Max(worst, MathF.Abs(found - expected));
                }
        near.Should().BeGreaterThan(1000);
        turned.Should().Be(0, "no cell more than a tenth of a unit from the sphere is held on the wrong side of it");
        worst.Should().BeLessThan(0.03f, "and each holds its distance within the sphere's facets, 0.014 at 16 rings");
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Glowing_Sheet_Thinner_Than_A_Cell_On_A_Wall_Gives_Off_Its_Whole_Light_Where_It_Lies()
    {
        // A sheet 0.02 thick flat on a wall, under cells of 0.15, so the cells inside the wall
        // behind it, which the field blends with those in front where it is read at the sheet,
        // are painted by the wall, nearer them, and give off what the sheet lends them. Lent to the
        // cells within half a cell at a share of its thickness over the cell, the face read 1.27
        // of the sheet's 2, as this test measured on an RTX 4070.
        var config = Config.Default.WithWindow("scene field sheet", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.15f);
        var wall = LoadModelFromMesh(GenMeshCube(4, 3, 0.3f));
        var sheet = LoadModelFromMesh(GenMeshCube(1.2f, 1.2f, 0.02f));
        sheet.Materials[0].Emissive = Color.White;
        sheet.Materials[0].EmissiveIntensity = 2;
        var camera = new Camera3D(new Vector3(0, 1.5f, 4), new Vector3(0, 1.5f, 0), Vector3.UnitY, 45);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(wall, new Vector3(0, 1.5f, -0.15f), 1, Color.White);
            DrawModel(sheet, new Vector3(0, 1.5f, 0.01f), 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var (_, glow) = device.ReadSceneFieldColors(fields.Field!);
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.15f;

        // The light the field gives off at a point, blended between the eight cells around it as
        // the passes sample it.
        float Red(Vector3 point)
        {
            var f = (point - origin) / cell - new Vector3(0.5f);
            var (x, y, z) = ((int)MathF.Floor(f.X), (int)MathF.Floor(f.Y), (int)MathF.Floor(f.Z));
            var t = f - new Vector3(x, y, z);
            float sum = 0;
            for (int corner = 0; corner < 8; corner++)
            {
                var (dx, dy, dz) = (corner & 1, (corner >> 1) & 1, corner >> 2);
                var weight = (dx == 0 ? 1 - t.X : t.X) * (dy == 0 ? 1 - t.Y : t.Y) * (dz == 0 ? 1 - t.Z : t.Z);
                sum += weight * glow[(((z + dz) * size + y + dy) * size + x + dx) * 4];
            }
            return sum;
        }

        // Across the sheet's face, a cell in from its edges.
        var reads = new List<float>();
        for (float y = 1.1f; y <= 1.9f; y += 0.05f)
            for (float x = -0.4f; x <= 0.4f; x += 0.05f)
                reads.Add(Red(new Vector3(x, y, 0.02f)));
        reads.Average().Should().BeGreaterThan(1.8f, $"the field gives off the sheet's light of 2 where it lies, the least {reads.Min():0.00}");
        UnloadModel(wall);
        UnloadModel(sheet);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void No_Cell_Away_From_Every_Mesh_Reads_As_Near_A_Surface()
    {
        var config = Config.Default.WithWindow("scene field signs", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.25f, 3);
        var ground = LoadModelFromMesh(GenMeshPlane(40, 40, 1, 1));
        var wall = LoadModelFromMesh(GenMeshCube(10, 3, 0.5f));
        var pillar = LoadModelFromMesh(GenMeshCylinder(0.35f, 3, 16));
        var crate = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(8, 6, 7), new Vector3(0, 1, 0), Vector3.UnitY, 45);

        // Where a sign is easily got wrong: a wall turned a right angle, whose side's plane runs
        // through cells above it, the rims of pillars, crates on the ground and on each other, and
        // the edges of the ground, an open mesh.
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.Gray);
            DrawModel(wall, new Vector3(0, 1.5f, -5), 1, Color.Gray);
            DrawModelEx(wall, new Vector3(-5, 1.5f, 0), Vector3.UnitY, 90, Vector3.One, Color.Gray);
            for (int i = 0; i < 4; i++) DrawModel(pillar, new Vector3(3.5f, 0, -3.5f + i * 2.3f), 1, Color.Gray);
            DrawModel(crate, new Vector3(-3.6f, 0.5f, -3.6f), 1, Color.Gray);
            DrawModel(crate, new Vector3(-3.1f, 1.5f, -3.7f), 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);

        // A cell above every mesh, or past the ground's edge and not below it, is a cell and a
        // half from any surface at least, so none holds less than a third of a cell.
        const int size = SceneFieldPlan.Resolution;
        var wrong = new List<string>();
        for (int c = 0; c < 3; c++)
        {
            var origin = fields.Plan!.BuiltOrigin(c)!.Value;
            var cell = fields.Plan.CellOf(c);
            for (int z = 0; z < size; z++)
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = origin + (new Vector3(x, y, z) + new Vector3(0.5f)) * cell;
                        var away = p.Y > 3 + 1.5f * cell || MathF.Abs(p.X) > 20 + 1.5f * cell || MathF.Abs(p.Z) > 20 + 1.5f * cell;
                        var d = distances[((c * size + z) * size + y) * size + x];
                        if (away && p.Y > -1.5f * cell && d < cell / 3) wrong.Add($"cascade {c} at {p}: {d}");
                    }
        }
        wrong.Should().BeEmpty();

        // And a cell of the second cascade inside the lower crate, where its bottom meets the
        // ground, is inside.
        var second = fields.Plan!.BuiltOrigin(1)!.Value;
        var inCrate = (new Vector3(-3.6f, 0.1f, -3.6f) - second) / 0.5f;
        distances[((size + (int)inCrate.Z) * size + (int)inCrate.Y) * size + (int)inCrate.X].Should()
            .BeNegative("a crate standing on the ground is inside where both are as near");
        UnloadModel(ground);
        UnloadModel(wall);
        UnloadModel(pillar);
        UnloadModel(crate);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Closed_Wall_Thinner_Than_A_Cell_Stops_A_Ray_And_An_Open_Plane_Puts_No_Wedge_Below_Its_Rim()
    {
        var config = Config.Default.WithWindow("scene field walls", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        // A closed slab a twentieth of a unit thick, half way between two rows of cells, which no
        // cell lies inside, and a ground plane, one-sided and open at its rim.
        var slab = LoadModelFromMesh(GenMeshCube(4, 0.05f, 4));
        var ground = LoadModelFromMesh(GenMeshPlane(2, 2, 1, 1));
        var camera = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            BeginMode3D(camera);
            DrawModel(slab, new Vector3(-2.5f, 0, 0), 1, Color.Gray);
            DrawModel(ground, new Vector3(2.5f, 0, 0), 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.25f;
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;
        float At(float x, float y, float z) => distances[((int)((z - origin.Z) / cell) * size + (int)((y - origin.Y) / cell)) * size + (int)((x - origin.X) / cell)];

        // Down the slab's middle the least distance is at zero or below, where the cells either side
        // were a tenth of a unit from its faces and a trace stepped over it.
        var column = Enumerable.Range(0, size).Select(y => (Y: origin.Y + (y + 0.5f) * cell, D: At(-2.5f, origin.Y + (y + 0.5f) * cell, 0))).ToArray();
        column.Where(c => MathF.Abs(c.Y) < cell).Min(c => c.D).Should().BeLessThanOrEqualTo(0.001f, "the slab stops a ray");
        // Below the ground a cell inside its rim is behind it, and one past its rim is not.
        At(2.5f, -0.3f, 0).Should().BeLessThan(0, "below the ground is inside it");
        At(3.8f, -0.3f, 0).Should().BeGreaterThan(0, "and below and past its rim is not, where a wedge under the rim was");
        UnloadModel(slab);
        UnloadModel(ground);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Double_Sided_Sheet_Thinner_Than_A_Cell_Crosses_Zero_Wherever_It_Lies_Between_The_Cells()
    {
        var config = Config.Default.WithWindow("scene field sheets", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var sheet = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        sheet.Materials[0].DoubleSided = true;
        var camera = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        // A sheet exactly half way between two rows of cells, where its distance in both is half a
        // cell, and one a fifth of a cell nearer the row above, as a floor of an imported room is.
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            BeginMode3D(camera);
            DrawModel(sheet, new Vector3(-2.5f, 0, 0), 1, Color.Gray);
            DrawModel(sheet, new Vector3(2.5f, 1.05f, 0), 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var distances = ((GraphicsDevice)renderer.Context.Graphics!).ReadSceneField(fields.Field!);

        // Down each column through a sheet the least distance is at zero or below, where a sheet with
        // no inside would leave half a cell, which a ray traced through the field steps over, and a
        // cell a cell and a half above holds a cell, half a cell nearer than it lies.
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.25f;
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;
        foreach (var (x, height) in new[] { (-2.5f, 0f), (2.5f, 1.05f) })
        {
            int column = (int)((x - origin.X) / cell), row = (int)((0 - origin.Z) / cell);
            var down = Enumerable.Range(0, size).Select(y => (Y: origin.Y + (y + 0.5f) * cell, D: distances[(row * size + y) * size + column])).ToArray();
            down.Where(c => MathF.Abs(c.Y - height) < cell).Min(c => c.D).Should().BeLessThanOrEqualTo(0.001f, $"the sheet at {height} stops a ray");
            var above = down.First(c => MathF.Abs(c.Y - (height + 1.5f * cell)) < cell / 2);
            above.D.Should().BeApproximately(above.Y - height - cell / 2, 0.01f);
        }
        UnloadModel(sheet);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Particles_Bounce_Off_A_Wall_Behind_The_Camera_Through_The_Field_And_Pass_It_Without()
    {
        int Seen(bool field)
        {
            var config = Config.Default.WithWindow("scene field particles", 160, 120) with
            {
                Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
            };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            if (field) SetSceneField(1, 0.25f);
            var wall = LoadModelFromMesh(GenMeshCube(8, 8, 0.5f));
            var camera = new Camera3D(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);
            // Red particles thrown from in front of the camera straight past it at a wall a unit
            // behind it, which the window's depth never holds, reaching it in three quarters of a
            // second.
            var thrown = CreateParticleEmitter(new Vector3(0, 0, -2), ParticleEmitter.Default with
            {
                MaxParticles = 60, Emitting = false, Life = 6, LifeVariation = 0, Radius = 0.3f, Spread = 0,
                Velocity = new Vector3(0, 0, 12), Gravity = Vector3.Zero, StartSize = 0.25f, EndSize = 0.25f,
                StartColor = new Color(255, 40, 20), EndColor = new Color(255, 40, 20),
                Collision = ParticleCollision.Bounce, Bounce = 0.5f,
            });
            var path = Path.Combine(_folder.Path, $"particles-{field}.png");
            // The wall settles into the field before the burst, which is seen half a second after
            // it reaches the wall, back three units in front of the camera where it bounced.
            for (int frame = 0; frame < 160 && !File.Exists(path); frame++)
            {
                if (frame == 20) EmitParticles(thrown, 60);
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(wall, new Vector3(0, 0, 7), 1, Color.Gray);
                EndMode3D();
                if (frame == 20 + 75) TakeScreenshot(path);
                EndDrawing();
            }
            var image = LoadImage(path);
            var red = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (GetImageColor(image, x, y) is var c && c.R > c.G + 100) red++;
            UnloadImage(image);
            UnloadModel(wall);
            CloseWindow();
            UseApp(null);
            return red;
        }

        Seen(field: false).Should().Be(0, "with no field the particles meet no depth behind the camera and fly on");
        Seen(field: true).Should().BeGreaterThan(20, "with the field they bounce off the wall it holds and come back into view");
    }

    private readonly TestFolder _folder = new("engine-scene-field-");
}
