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
    public void Figures_Past_What_A_Frame_Stamps_Are_Boxes_Beyond_The_Nearest_And_None_Is_Left_Out()
    {
        // Thirty skinned figures of twenty limbs each, one after another away from the eye, where
        // a frame stamps 256 boxes: the nearest take their limbs while the rest still have room for
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
