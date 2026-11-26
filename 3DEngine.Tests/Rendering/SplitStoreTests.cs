using System.Globalization;
using System.Numerics;
using FluentAssertions;
using RadianceCascades;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The RadianceCascades module's split cascades kept on the device through the engine's store
/// (<c>gi_split_store.slang</c>), its uints changed by the device's atomics from many threads at
/// once, over the module's own corridor, room and rays among boxes, read against what the module's
/// tests read of the same store run a thread after another on the CPU.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class SplitStoreTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public SplitStoreTests(ITestOutputHelper output)
    {
        _output = output;
        var config = Config.Default.WithWindow("split store", 32, 32) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the store's dispatches");
        CloseWindow();
        UseApp(null);
    }

    // The module's tests' store, its finest level's probes half a unit apart reaching 4 spacings.
    private static readonly SplitStoreShape Shape = new() { Slots = 4096, Spacing = 0.5f, Levels = 4, Cascades = 4, Reach = 2, Age = 8 };

    // The module's passes over the engine's store, one a dispatch picked by pass, the seen points
    // and the boxes the rays meet as the module's CPU kernel gives them, each point its place with
    // 1 in w where the eye sees it then its normal, each box its low corner with its light in w
    // then its high corner, in a world's cube 128 a side about its middle.
    private const string Kernel = """
        import gi_split_store;

        uniform uint pass;
        uniform float3 eye;
        uniform uint frame;
        uniform uint cascade;
        uniform uint count;
        uniform uint boxCount;
        uniform float sky;
        uniform uint slots;
        uniform float spacing;
        uniform uint levels;
        uniform uint cascades;
        uniform float reach;
        uniform uint age;

        StructuredBuffer<float4> points;
        RWStructuredBuffer<Atomic<uint>> uints;
        RWStructuredBuffer<float4> light;
        RWStructuredBuffer<float4> taken;
        StructuredBuffer<float4> boxes;

        struct BufferPoints : IVisiblePoints
        {
            float3 placeAt(uint index) { return points[index * 2u].xyz; }
            float3 normalAt(uint index) { return points[index * 2u + 1u].xyz; }
            void keepTaken(uint index, float3 value) { taken[index] = float4(value, 1.0); }
            bool seenAt(uint index) { return points[index * 2u].w > 0.0; }
        }

        // A part of a ray meets the nearest box it enters within its interval, or starts within,
        // with the box's light, or leaves the world's cube where it leaves it first, or passes.
        struct BoxHits : IRayHits
        {
            ProbeRay along(float3 origin, float3 way, float start, float end)
            {
                float3 inv = 1.0 / select(abs(way) < 1e-8, float3(1e-8), way);
                float nearest = end, lit = 0.0;
                for (uint b = 0u; b < boxCount; b++)
                {
                    float4 low = boxes[2u * b], high = boxes[2u * b + 1u];
                    float3 t0 = (low.xyz - origin) * inv, t1 = (high.xyz - origin) * inv;
                    float enter = max(max(min(t0.x, t1.x), min(t0.y, t1.y)), min(t0.z, t1.z));
                    float leave = min(min(max(t0.x, t1.x), max(t0.y, t1.y)), max(t0.z, t1.z));
                    float t = max(enter, start);
                    if (enter <= leave && leave >= start && t < nearest)
                    {
                        nearest = t;
                        lit = low.w;
                    }
                }
                float3 wall = (select(way >= 0.0, float3(64.0), float3(-64.0)) - origin) * inv;
                float leaves = min(wall.x, min(wall.y, wall.z));
                ProbeRay ray;
                ray.carried = 1.0;
                ray.light = float3(0.0);
                ray.t = end;
                ray.ended = RayPast;
                if (nearest < end && nearest <= leaves)
                {
                    ray.ended = RayMet;
                    ray.t = nearest;
                    ray.light = float3(lit);
                }
                else if (leaves < end)
                {
                    ray.ended = RayLeft;
                    ray.t = max(leaves, start);
                }
                return ray;
            }
        }

        struct EvenSky : ISky
        {
            float3 skyAlong(float3 way) { return float3(sky); }
        }

        [shader("compute")]
        [numthreads(64, 1, 1)]
        void computeMain(uint3 id : SV_DispatchThreadID)
        {
            SplitFrame at = { eye, frame };
            SplitStore shape = { slots, spacing, levels, cascades, reach, age };
            uint index = id.x;
            BufferPoints seen;
            StoreUints store = { uints };
            StoreLight kept = { light };
            BoxHits hits;
            EvenSky even;
            if (pass == 0u)
                clearSplitTable(store, shape, index);
            else if (pass == 1u)
                keepSplitProbes(store, at, shape, index);
            else if (pass == 2u && index < count)
                askSeenProbes(seen, store, at, shape, index);
            else if (pass == 3u)
                askCascadeProbes(store, at, shape, cascade, index);
            else if (pass == 4u && index < count)
                traceSeenRays(seen, hits, even, store, at, shape, index);
            else if (pass == 5u)
                keepSplitLight(store, kept, at, shape, index);
            else if (pass == 6u)
                mergeSplitCascade(store, kept, even, at, shape, cascade, index);
            else if (pass == 7u && index < count)
                gatherSurface(store, kept, at, shape, seen, index);
            else if (pass == 8u && index < count)
            {
                // Each point's ray of the frame traced whole across every interval of its level
                // and of the next, five float4 a level, its light and how it ended, then the entry
                // and the way of each cascade's probe its parts go to with the end of that
                // cascade's interval, the finest's w where the ray ended, the next level's entries
                // -2 where the point stands outside the overlap.
                float next;
                float3 p = seen.placeAt(index), n = seen.normalAt(index);
                uint level = splitLevelAt(at, shape, p, next);
                float3 way = splitRayWay(n, index, frame);
                for (uint j = 0u; j < 2u; j++)
                {
                    uint first = index * 10u + j * 5u;
                    uint l = level + j;
                    ProbeRay whole = hits.along(splitRayOrigin(at, shape, p, n), way, 0.0, splitIntervalEnd(shape, l, shape.cascades - 1u));
                    float3 met = whole.light + (whole.ended == RayLeft ? whole.carried * even.skyAlong(way) : float3(0.0));
                    taken[first] = float4(met, float(whole.ended));
                    int3 cell = splitNearestCell(shape, l, 0u, p);
                    for (uint c = 0u; c < shape.cascades; c++)
                    {
                        uint entry = findSplitProbe(store, shape, splitKey(l, c, cell));
                        float name = j == 1u && !(next > 0.0) ? -2.0 : entry == SplitNone ? -1.0 : float(entry);
                        taken[first + 1u + c] = float4(name, float(splitWayOf(c, way)), splitIntervalEnd(shape, l, c), c == 0u ? whole.t : 0.0);
                        cell = (cell + 1) >> 1;
                    }
                }
            }
            else if (pass == 9u && index == 0u)
                taken[0] = float4(float(splitUints(shape)), float(splitLights(shape)), float(splitPoolAt(shape)), float(splitSumsAt(shape)));
        }
        """;

    // A store's buffers kept from frame to frame and the passes of a frame dispatched over them.
    private sealed class Store : IDisposable
    {
        private readonly Shader _shader;
        private readonly ShaderBuffer _points, _uints, _light, _taken, _boxes;
        private readonly int _count;

        public Store(float[] points, float[]? boxes = null)
        {
            _shader = LoadComputeShaderFromMemory(Kernel, "split_store.slang");
            _shader.IsValid.Should().BeTrue("the module's passes compile over the engine's store");
            _count = points.Length / 8;
            // One box out past the world where none is given.
            float[] lit = boxes is { Length: > 0 } ? boxes : [1000, 1000, 1000, 0, 1001, 1001, 1001, 0];
            _points = LoadShaderBuffer<float>(points.Length == 0 ? new float[8] : points);
            _uints = LoadShaderBuffer(4 * Shape.Uints);
            _light = LoadShaderBuffer(16 * Shape.Lights);
            _taken = LoadShaderBuffer(16 * Math.Max(10 * _count, Shape.Entries));
            _boxes = LoadShaderBuffer<float>(lit);
            foreach (var (name, buffer) in new[] { ("points", _points), ("uints", _uints), ("light", _light), ("taken", _taken), ("boxes", _boxes) })
                SetShaderValueBuffer(_shader, GetShaderLocation(_shader, name), buffer);
            Set("count", _count);
            Set("boxCount", lit.Length / 8);
            SetShaderValue(_shader, GetShaderLocation(_shader, "sky"), 1f);
            Set("slots", Shape.Slots);
            SetShaderValue(_shader, GetShaderLocation(_shader, "spacing"), Shape.Spacing);
            Set("levels", Shape.Levels);
            Set("cascades", Shape.Cascades);
            SetShaderValue(_shader, GetShaderLocation(_shader, "reach"), Shape.Reach);
            Set("age", Shape.Age);
        }

        private void Set(string name, int value) => SetShaderValue(_shader, GetShaderLocation(_shader, name), value);

        public void Run(int pass, Vector3 eye, uint frame, int threads, int cascade = 0)
        {
            Set("pass", pass);
            SetShaderValue(_shader, GetShaderLocation(_shader, "eye"), eye);
            Set("frame", (int)frame);
            Set("cascade", cascade);
            ComputeShaderDispatch(_shader, (threads + 63) / 64, 1, 1);
        }

        // A frame's passes, the table emptied, the probes kept refilled in it, the seen points'
        // probes asked for and each cascade's asked for from the one below, and where rays is set
        // the seen points' rays traced into them, their sums kept and merged where keep is set.
        public void Frame(Vector3 eye, uint frame, bool rays = false, bool keep = true)
        {
            Run(0, eye, frame, 4 * Shape.Slots);
            Run(1, eye, frame, Shape.Entries);
            Run(2, eye, frame, _count);
            for (int c = 0; c + 1 < Shape.Cascades; c++)
                Run(3, eye, frame, Shape.PoolProbes(c), c);
            if (!rays)
                return;
            Run(4, eye, frame, _count);
            if (!keep)
                return;
            Run(5, eye, frame, Shape.Cascades * SplitStoreShape.BaseWays * Shape.Slots);
            for (int c = Shape.Cascades - 1; c >= 0; c--)
                Run(6, eye, frame, SplitStoreShape.BaseWays * Shape.Slots, c);
        }

        public uint[] Uints()
        {
            var uints = new uint[Shape.Uints];
            ReadShaderBuffer<uint>(_uints, uints);
            return uints;
        }

        public float[] Taken(int pass, Vector3 eye, uint frame)
        {
            Run(pass, eye, frame, Math.Max(_count, 1));
            var taken = new float[4 * Math.Max(10 * _count, Shape.Entries)];
            ReadShaderBuffer<float>(_taken, taken);
            return taken;
        }

        public void Dispose()
        {
            foreach (var buffer in new[] { _points, _uints, _light, _taken, _boxes })
                UnloadShaderBuffer(buffer);
            UnloadShader(_shader);
        }
    }

    // The first uint of the pools, after the counts and the table of four slots a finest entry.
    private static int PoolAt => 16 + 2 * 4 * Shape.Slots;

    // Each entry's key and the frame it was taken, by key.
    private static Dictionary<uint, (int Entry, uint Taken)> Keys(uint[] uints) =>
        Enumerable.Range(0, Shape.Entries).Where(e => uints[PoolAt + 3 * e] != 0).ToDictionary(e => uints[PoolAt + 3 * e], e => (e, uints[PoolAt + 3 * e + 2]));

    // Where the light of entry's probe of cascade along its way lies, as splitLightAt lays it.
    private static int LightAt(int cascade, int entry, int way) =>
        cascade * SplitStoreShape.BaseWays * Shape.Slots
        + (entry - Enumerable.Range(0, cascade).Sum(Shape.PoolProbes)) * (SplitStoreShape.BaseWays << 2 * cascade) + way;

    // The inside faces of the module's corridor, from -12 to 12 along it an eighth of a unit apart,
    // the gap in its roof left out.
    private static float[] CorridorFaces()
    {
        var points = new List<(Vector3 Place, Vector3 Normal)>();
        for (int i = 0; i <= 192; i++)
        {
            var x = -12 + i * 0.125f;
            for (int j = 0; j <= 20; j++)
            {
                var z = -1.25f + j * 0.125f;
                points.Add((new(x, 0, z), Vector3.UnitY));
                if (MathF.Abs(x) >= 1)
                    points.Add((new(x, 3.5f, z), -Vector3.UnitY));
            }
            for (int j = 0; j <= 28; j++)
            {
                var y = j * 0.125f;
                points.Add((new(x, y, -1.25f), Vector3.UnitZ));
                points.Add((new(x, y, 1.25f), -Vector3.UnitZ));
            }
        }
        return Floats(points);
    }

    // The corridor's boxes as the module lights them, each sending a twentieth, the lamp in its
    // roof 5 and the floor under its gap 0.6.
    private static readonly float[] CorridorLitBoxes =
    [
        .. new (Vector3 Low, Vector3 High, float Light)[]
        {
            (new(-61, -1, -2.25f), new(61, 0, 2.25f), 0.05f), (new(-61, 0, -2.25f), new(61, 3.5f, -1.25f), 0.05f),
            (new(-61, 0, 1.25f), new(61, 3.5f, 2.25f), 0.05f), (new(-61, 3.5f, -2.25f), new(-1, 4.5f, 2.25f), 0.05f),
            (new(1, 3.5f, -2.25f), new(61, 4.5f, 2.25f), 0.05f), (new(-61, 0, -1.25f), new(-60, 3.5f, 1.25f), 0.05f),
            (new(60, 0, -1.25f), new(61, 3.5f, 1.25f), 0.05f), (new(3, 3.49f, -0.75f), new(4.5f, 3.5f, 0.75f), 5),
            (new(-1, 0, -1.25f), new(1, 0.01f, 1.25f), 0.6f),
        }.SelectMany(b => new[] { b.Low.X, b.Low.Y, b.Low.Z, b.Light, b.High.X, b.High.Y, b.High.Z, 0 }),
    ];

    // The module's closed room eight by four by six, its faces' points a quarter of a unit apart an
    // eighth in from each edge, and its six walls as boxes a unit thick, each sending light.
    private static (float[] Points, float[] Boxes) Room(float light)
    {
        var points = new List<(Vector3 Place, Vector3 Normal)>();
        for (float a = -3.875f; a < 4; a += 0.25f)
            for (float b = -2.875f; b < 3; b += 0.25f)
            {
                points.Add((new(a, 0, b), Vector3.UnitY));
                points.Add((new(a, 4, b), -Vector3.UnitY));
            }
        for (float h = 0.125f; h < 4; h += 0.25f)
        {
            for (float b = -2.875f; b < 3; b += 0.25f)
            {
                points.Add((new(-4, h, b), Vector3.UnitX));
                points.Add((new(4, h, b), -Vector3.UnitX));
            }
            for (float a = -3.875f; a < 4; a += 0.25f)
            {
                points.Add((new(a, h, -3), Vector3.UnitZ));
                points.Add((new(a, h, 3), -Vector3.UnitZ));
            }
        }
        var walls = new (Vector3 Low, Vector3 High)[]
        {
            (new(-5, -1, -4), new(5, 0, 4)), (new(-5, 4, -4), new(5, 5, 4)), (new(-5, 0, -4), new(-4, 4, 4)),
            (new(4, 0, -4), new(5, 4, 4)), (new(-4, 0, -4), new(4, 4, -3)), (new(-4, 0, 3), new(4, 4, 4)),
        };
        return (Floats(points), walls.SelectMany(w => new[] { w.Low.X, w.Low.Y, w.Low.Z, light, w.High.X, w.High.Y, w.High.Z, 0 }).ToArray());
    }

    private static float[] Floats(IEnumerable<(Vector3 Place, Vector3 Normal)> points) =>
        points.SelectMany(p => new[] { p.Place.X, p.Place.Y, p.Place.Z, 1f, p.Normal.X, p.Normal.Y, p.Normal.Z, 0f }).ToArray();

    [NeedsVulkanFact]
    public void The_Device_Lays_Out_The_Store_And_Stands_The_Probes_The_Module_Stands_On_The_CPU()
    {
        // The corridor's faces seen from an eye in it ask for the probes the module's CPU run
        // stands by cascade, each asked for once however many points ask at once on the device,
        // and each cascade's count of probes standing is the count of entries holding a key.
        var points = CorridorFaces();
        var eye = new Vector3(0.3f, 1.5f, 0);
        using var store = new Store(points);
        var layout = store.Taken(9, eye, 1);
        ((int)layout[0], (int)layout[1], (int)layout[2]).Should().Be((Shape.Uints, Shape.Lights, PoolAt), "the store's layout on the device is the library's");
        store.Frame(eye, 1);
        var uints = store.Uints();
        var keys = Keys(uints);
        var counts = Enumerable.Range(0, Shape.Cascades).Select(c => keys.Keys.Count(k => (k >> 27 & 3) == c)).ToArray();
        var standing = Enumerable.Range(0, Shape.Cascades).Select(c => (int)uints[4 * c + 2]).ToArray();
        _output.WriteLine($"the store holds {string.Join(", ", counts)} probes by cascade, {string.Join(", ", standing)} counted standing");
        standing.Should().Equal(counts);
        counts.Should().Equal([322, 108, 46, 28], "the corridor's points ask for the probes the module's CPU run stands");
    }

    [NeedsVulkanFact]
    public void A_Probe_On_The_Device_Keeps_Its_Entry_While_The_Eye_Walks()
    {
        // The eye walks the corridor in quarter steps, as the module's walk does, and a probe asked
        // for at one step and the next keeps its key, its entry and the frame it was taken, though
        // which free entry a new probe takes turns on the order the device's threads run in.
        var points = CorridorFaces();
        using var store = new Store(points);
        Dictionary<uint, (int Entry, uint Taken)>? before = null;
        int kept = 0, fresh = 0, moved = 0;
        for (uint frame = 1; frame <= 17; frame++)
        {
            store.Frame(new(-2 + (frame - 1) * 0.25f, 1.5f, 0), frame);
            var keys = Keys(store.Uints());
            if (before is not null)
                foreach (var (key, (entry, taken)) in keys)
                {
                    if (!before.TryGetValue(key, out var was))
                        fresh++;
                    else if (was == (entry, taken))
                        kept++;
                    else
                        moved++;
                }
            before = keys;
        }
        var said = $"over 16 steps of the eye {kept} probes kept their entries, {fresh} were taken and {moved} moved";
        _output.WriteLine(said);
        (kept, fresh, moved).Should().Be((8852, 368, 0), said);
    }

    [NeedsVulkanFact]
    public void Each_Part_Of_A_Ray_On_The_Device_Is_Added_Atomically_To_The_Probe_Of_Its_Interval()
    {
        // One frame's rays from the corridor's points in the lit corridor add each part to the
        // probe of the interval it lies in, many threads adding to one way at once, and the sums
        // the device's atomics leave are those the same rays traced whole and split by where they
        // ended give, every part finding its probe, the parts reaching each cascade as on the CPU.
        var points = CorridorFaces();
        var count = points.Length / 8;
        var eye = new Vector3(0, 1.5f, 0);
        using var store = new Store(points, CorridorLitBoxes);
        store.Frame(eye, 1, rays: true, keep: false);
        var whole = store.Taken(8, eye, 1);
        var uints = store.Uints();
        var sumsAt = 16 + 2 * 4 * Shape.Slots + 4 * Shape.Entries;
        var span = SplitStoreShape.BaseWays * Shape.Slots;
        var expected = new uint[5 * Shape.Cascades * span];
        var (rays, missing) = (0, 0);
        var reached = new int[Shape.Cascades];
        static uint Parts(float light) => (uint)(Math.Clamp(light, 0, 16384) * 1024 + 0.5f);
        for (int k = 0; k < count; k++)
            for (int j = 0; j < 2; j++)
            {
                var at = 4 * (10 * k + 5 * j);
                if (whole[at + 4] == -2)
                    continue;
                rays++;
                var (ended, t) = ((uint)whole[at + 3], whole[at + 7]);
                for (int c = 0; c < Shape.Cascades; c++)
                {
                    var (entry, way, end) = ((int)whole[at + 4 + 4 * c], (int)whole[at + 5 + 4 * c], whole[at + 6 + 4 * c]);
                    var passed = ended == 1 || t >= end;
                    reached[c]++;
                    if (entry < 0)
                        missing++;
                    else
                    {
                        var sums = 5 * LightAt(c, entry, way);
                        for (int i = 0; i < 3; i++)
                            expected[sums + i] += passed ? 0 : Parts(whole[at + i]);
                        expected[sums + 3] += passed ? 1024u : 0;
                        expected[sums + 4]++;
                    }
                    if (!passed)
                        break;
                }
            }
        var off = Enumerable.Range(0, expected.Length).Count(i => uints[sumsAt + i] != expected[i]);
        var said = $"{rays} rays of {count} seen points add {reached.Sum()} parts, {string.Join(", ", reached)} reaching each cascade from the finest, "
            + $"{missing} parts finding no probe and {off} of the sums' uints off the rays traced whole";
        _output.WriteLine(said);
        (rays, missing, off).Should().Be((20997, 0, 0), said);
        reached.Should().Equal([20997, 7184, 871, 242], said);
    }

    [NeedsVulkanFact]
    public void The_Store_On_The_Device_Gathers_An_Even_Sky_And_A_Closed_Rooms_One_Light()
    {
        // The corridor's points with nothing about them under a sky of 1 gather its irradiance of
        // pi after four frames, and in a closed room whose walls all send 0.3, which a ray's part
        // holds as 307 of 1024, every point gathers pi times that light, each within the module's
        // bound of a ten thousandth.
        var corridor = CorridorFaces();
        var eye = new Vector3(0, 1.5f, 0);
        using (var store = new Store(corridor))
        {
            for (uint frame = 1; frame <= 4; frame++)
                store.Frame(eye, frame, rays: true);
            var taken = store.Taken(7, eye, 4);
            var worst = Enumerable.Range(0, corridor.Length / 8).Max(k => MathF.Abs(taken[4 * k] / MathF.PI - 1));
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{corridor.Length / 8} points under a sky of 1 gather pi times it within {worst:0.000000}"));
            worst.Should().BeLessThan(1e-4f);
        }

        var (points, walls) = Room(0.3f);
        var inRoom = new Vector3(0, 2, 0);
        using (var store = new Store(points, walls))
        {
            for (uint frame = 1; frame <= 4; frame++)
                store.Frame(inRoom, frame, rays: true);
            var held = 307 / 1024f;
            var taken = store.Taken(7, inRoom, 4);
            var worst = Enumerable.Range(0, points.Length / 8).Max(k => MathF.Abs(taken[4 * k] / MathF.PI - held) / held);
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{points.Length / 8} points in the room gather pi times its walls' light within {worst:0.000000} of it"));
            worst.Should().BeLessThan(1e-4f);
        }
    }
}
