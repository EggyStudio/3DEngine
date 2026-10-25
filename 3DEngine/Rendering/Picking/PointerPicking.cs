using System.Numerics;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Finds the mesh entity under the mouse through the window's camera where it has
/// <see cref="Camera.Picking"/> set, and triggers a <see cref="Pointer{TEvent}"/> on it for what
/// the mouse does, once a frame in <see cref="Stage.PreUpdate"/>.
/// </summary>
/// <remarks>
/// <para>
/// The ray goes from the camera through the mouse's place in the window and is cast at each
/// <see cref="Mesh"/> entity where the entity was drawn, its <see cref="GlobalTransform"/>, which
/// the frame before wrote, or its transforms composed where it has none yet. A mesh the ray's
/// line misses the sphere around is passed over, and the triangles of one it reaches are tested,
/// the nearest triangle met being the hit. A skinned mesh is met as it stands unposed.
/// </para>
/// <para>
/// One instance is the world's resource, made by <see cref="EcsPlugin"/>, holding what the mouse
/// was over and what each button went down on. A world with no camera asking casts nothing and
/// reads the cameras alone. The mouse over an ImGui window picks nothing, as it moves no camera.
/// </para>
/// </remarks>
internal sealed class PointerPicking
{
    private static readonly MouseButton[] Buttons = [MouseButton.Left, MouseButton.Right, MouseButton.Middle];

    // The box and the sphere round each mesh's positions, by the array, let go with it, and the
    // last array met, since entities of one shape share theirs and come in runs.
    private readonly ConditionalWeakTable<Vector3[], Bounds> _bounds = [];
    private Vector3[]? _lastPositions;
    private Bounds? _lastBounds;

    private Entity _over;
    private PointerHit _overHit;
    private Vector2 _position;
    private bool _placed;
    private readonly Pressed[] _pressed = new Pressed[3];

    /// <summary>How many rays have been cast, which a test reads to know none was where no camera asks.</summary>
    internal long Casts { get; private set; }

    private sealed class Bounds(Vector3 min, Vector3 max)
    {
        public readonly Vector3 Min = min, Max = max;
        public readonly Vector3 Center = (min + max) / 2;
        public readonly float Radius = (max - min).Length() / 2;
    }

    // What a button went down on, where, and whether a drag of it has started.
    private struct Pressed
    {
        public Entity On;
        public PointerHit Hit;
        public Vector2 At;
        public bool Dragging;
    }

    /// <summary>The system, which finds the world's picking and runs it where a camera asks.</summary>
    internal static void Run(World world)
    {
        if (!world.TryGetResource<PointerPicking>(out var picking) || !world.TryGetResource<EcsWorld>(out var ecs)) return;
        if (WindowCamera(ecs) is not { } camera) return;
        if (!world.TryGetResource<Input>(out var input)) return;
        picking.Pick(world, ecs, input, camera);
    }

    // The camera drawing the window where it asks to pick, the first camera entity without a
    // render texture, as the renderer takes it.
    private static int? WindowCamera(EcsWorld ecs)
    {
        if (ecs.Count<Camera>() == 0) return null;
        foreach (var row in ecs.QueryReadOnly<Camera>())
            if (!row.Component.Target.IsValid) return row.Component.Picking ? row.Entity : null;
        return null;
    }

    private void Pick(World world, EcsWorld ecs, Input input, int cameraEntity)
    {
        var position = new Vector2(input.MouseX, input.MouseY);
        var delta = _placed ? position - _position : Vector2.Zero;
        _position = position;
        _placed = true;

        // Over an ImGui window the pointer is ImGui's, and over nothing of the scene.
        var gui = ImGuiNET.ImGui.GetCurrentContext() != IntPtr.Zero && ImGuiNET.ImGui.GetIO().WantCaptureMouse;
        var (hit, at) = gui ? (default(Entity), default(PointerHit)) : Cast(world, ecs, cameraEntity, position);

        if (hit != _over)
        {
            var left = _over;
            _over = hit;
            _overHit = at;
            if (ecs.IsAlive(left)) ecs.Trigger(new Pointer<Out>(left, position, default));
            if (!hit.IsNone) ecs.Trigger(new Pointer<Over>(hit, position, new Over(at)));
        }
        else if (!hit.IsNone)
        {
            _overHit = at;
            if (delta != Vector2.Zero) ecs.Trigger(new Pointer<Move>(hit, position, new Move(at, delta)));
        }

        for (int i = 0; i < Buttons.Length; i++)
        {
            var button = Buttons[i];
            ref var pressed = ref _pressed[i];
            if (input.MousePressed(button))
            {
                pressed = new Pressed { On = _over, Hit = _overHit, At = position };
                if (!_over.IsNone) ecs.Trigger(new Pointer<Press>(_over, position, new Press(button, _overHit)));
            }
            else if (input.MouseDown(button) && delta != Vector2.Zero && ecs.IsAlive(pressed.On))
            {
                if (!pressed.Dragging)
                {
                    pressed.Dragging = true;
                    ecs.Trigger(new Pointer<DragStart>(pressed.On, position, new DragStart(button, pressed.Hit)));
                }
                ecs.Trigger(new Pointer<Drag>(pressed.On, position, new Drag(button, position - pressed.At, delta)));
            }

            if (!input.MouseReleased(button)) continue;
            if (!_over.IsNone)
            {
                ecs.Trigger(new Pointer<Release>(_over, position, new Release(button, _overHit)));
                if (pressed.On == _over) ecs.Trigger(new Pointer<Click>(_over, position, new Click(button, _overHit)));
            }
            if (pressed.Dragging && ecs.IsAlive(pressed.On))
                ecs.Trigger(new Pointer<DragEnd>(pressed.On, position, new DragEnd(button, position - pressed.At)));
            pressed = default;
        }
    }

    // The nearest mesh entity the ray through the pointer meets, and where.
    private (Entity Entity, PointerHit Hit) Cast(World world, EcsWorld ecs, int cameraEntity, Vector2 pointer)
    {
        Casts++;
        var camera = ecs.GetReadOnly<Camera>(cameraEntity);
        var (width, height) = WindowSize(world);
        Matrix4x4.Decompose(TransformPropagation.WorldMatrix(ecs, cameraEntity), out _, out var rotation, out var origin);
        var tan = MathF.Tan(camera.FovY / 2);
        var aspect = (float)width / height;
        var through = new Vector3((2 * pointer.X / width - 1) * tan * aspect, (1 - 2 * pointer.Y / height) * tan, -1);
        var direction = Vector3.Normalize(Vector3.Transform(through, rotation));

        var best = float.MaxValue;
        var found = Entity.None;
        var normal = Vector3.Zero;
        foreach (var row in ecs.QueryReadOnly<Mesh>())
        {
            var entity = row.Entity;
            var positions = row.Component.Positions;
            if (positions is null || positions.Length < 3) continue;
            if (ecs.TryGet<Visible>(entity, out var visible) && !visible.Value || ecs.TryGet<ShadowOnly>(entity, out var alone) && alone.Value) continue;
            if (!ReferenceEquals(positions, _lastPositions))
            {
                _lastPositions = positions;
                _lastBounds = _bounds.GetValue(positions, Measure);
            }
            var bounds = _lastBounds!;
            var matrix = ecs.TryGet<GlobalTransform>(entity, out var global) ? global.Matrix : TransformPropagation.WorldMatrix(ecs, entity);

            // The sphere round it first, scaled by the matrix's largest scale.
            var center = Vector3.Transform(bounds.Center, matrix);
            var scale = MathF.Sqrt(MathF.Max(new Vector3(matrix.M11, matrix.M12, matrix.M13).LengthSquared(),
                MathF.Max(new Vector3(matrix.M21, matrix.M22, matrix.M23).LengthSquared(), new Vector3(matrix.M31, matrix.M32, matrix.M33).LengthSquared())));
            if (!Reaches(origin, direction, center, bounds.Radius * scale, best)) continue;

            // Then its triangles in its own space, where the ray's parameter is still the world's
            // distance, since its direction is carried over and not made of length one again.
            if (!Matrix4x4.Invert(matrix, out var inverse)) continue;
            var from = Vector3.Transform(origin, inverse);
            var along = Vector3.TransformNormal(direction, inverse);
            if (!MeetsBox(from, along, bounds.Min, bounds.Max, best)) continue;
            var indices = row.Component.Indices;
            var corners = indices?.Length ?? positions.Length;
            for (int i = 0; i + 2 < corners; i += 3)
            {
                var (a, b, c) = indices is null ? (positions[i], positions[i + 1], positions[i + 2])
                    : (positions[indices[i]], positions[indices[i + 1]], positions[indices[i + 2]]);
                if (!Meets(from, along, a, b, c, out var distance) || distance >= best) continue;
                best = distance;
                found = ecs.Handle(entity);
                normal = Vector3.Normalize(Vector3.TransformNormal(Vector3.Cross(b - a, c - a), Matrix4x4.Transpose(inverse)));
            }
        }
        if (found.IsNone) return default;
        if (Vector3.Dot(normal, direction) > 0) normal = -normal;
        return (found, new PointerHit(origin + direction * best, normal, best));
    }

    private static Bounds Measure(Vector3[] positions)
    {
        var (min, max) = (positions[0], positions[0]);
        foreach (var p in positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new Bounds(min, max);
    }

    // The size the mouse's place is read against, the window's, as the renderer sizes its camera.
    private static (float Width, float Height) WindowSize(World world)
    {
        var (width, height) = world.TryGetResource<AppWindow>(out var window) ? (window.Sdl.Width, window.Sdl.Height)
            : world.TryGetResource<OffscreenSurface>(out var surface) ? ((int)surface.Size.Width, (int)surface.Size.Height)
            : world.TryGetResource<Config>(out var config) ? (config.WindowData.Width, config.WindowData.Height) : (1, 1);
        return (Math.Max(1, width), Math.Max(1, height));
    }

    // Whether a ray of length one comes within radius of center nearer than best.
    private static bool Reaches(Vector3 origin, Vector3 direction, Vector3 center, float radius, float best)
    {
        var toCenter = center - origin;
        var along = Vector3.Dot(toCenter, direction);
        var apart = toCenter.LengthSquared() - along * along;
        if (apart > radius * radius) return false;
        return along + radius >= 0 && along - radius < best;
    }

    // Whether the ray meets the box nearer than best, by the slabs between its faces.
    private static bool MeetsBox(Vector3 from, Vector3 along, Vector3 min, Vector3 max, float best)
    {
        float near = 0, far = best;
        for (int axis = 0; axis < 3; axis++)
        {
            var (o, d, lo, hi) = (from[axis], along[axis], min[axis], max[axis]);
            if (MathF.Abs(d) < 1e-12f)
            {
                if (o < lo || o > hi) return false;
                continue;
            }
            var (t1, t2) = ((lo - o) / d, (hi - o) / d);
            if (t1 > t2) (t1, t2) = (t2, t1);
            near = MathF.Max(near, t1);
            far = MathF.Min(far, t2);
            if (near > far) return false;
        }
        return true;
    }

    // Where the ray meets a triangle from either side, as GetRayCollisionTriangle finds it, with a
    // direction of any length so the distance stays the world's.
    private static bool Meets(Vector3 from, Vector3 along, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0;
        var (edge1, edge2) = (b - a, c - a);
        var h = Vector3.Cross(along, edge2);
        var det = Vector3.Dot(edge1, h);
        if (MathF.Abs(det) < 1e-12f) return false;
        var f = 1 / det;
        var s = from - a;
        var u = f * Vector3.Dot(s, h);
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(along, q);
        if (v < 0 || u + v > 1) return false;
        distance = f * Vector3.Dot(edge2, q);
        return distance > 0;
    }
}
