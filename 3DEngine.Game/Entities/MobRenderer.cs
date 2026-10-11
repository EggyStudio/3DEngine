using System.Numerics;
using System.Runtime.InteropServices;
using static Engine.E3D;

namespace Engine.Game;

/// <summary>Draws the mobs, each piece of each kind's model one mesh, and every copy of a mesh one instanced draw.</summary>
/// <remarks>
/// <para>
/// A piece's boxes are one mesh with a color at each vertex, built facing north, so a mob is drawn
/// by turning each piece about its pivot by the head's look, the legs' stride or a wing's flap, and
/// the whole by its yaw. A piece that takes its mob's own color, a sheep's wool or a villager's
/// robe, is meshed once for each variant. A mob hit, or dying, is drawn through a red material for
/// the half second it stays red, and one dying falls onto its side, as Minecraft's do.
/// </para>
/// <para>
/// Mobs are drawn within 64 blocks of the eye, as Minecraft draws its entities, through the
/// engine's own shader, so the haze the sections fade into is not on them. They cast shadows, which
/// puts them in the scene's distance field as well, where a mesh that moves is held as boxes painted
/// its color, so a pig bounces pink light onto the grass under it.
/// </para>
/// </remarks>
public sealed class MobRenderer : IDisposable
{
    private const float Distance = 64;

    private sealed class Slot(ModelMesh mesh)
    {
        public readonly ModelMesh Mesh = mesh;
        public readonly List<Matrix4x4> Plain = [], Hurt = [];
    }

    private readonly ModelMaterial _skin = new(Color.White) { AlphaMode = MaterialAlphaMode.Opaque, Roughness = 0.85f };
    private readonly ModelMaterial _hurt = new(new Color(255, 96, 96)) { AlphaMode = MaterialAlphaMode.Opaque, Roughness = 0.85f };
    private readonly List<Slot> _slots = [];
    // Each kind's pieces' slots, by kind, piece and variant, a piece of one color holding one.
    private readonly Slot[][][] _byKind;

    public MobRenderer()
    {
        _byKind = new Slot[MobKinds.All.Length][][];
        foreach (var kind in MobKinds.All)
        {
            _byKind[kind.Id] = new Slot[kind.Parts.Length][];
            for (int p = 0; p < kind.Parts.Length; p++)
            {
                var part = kind.Parts[p];
                var variants = part.Varies ? kind.Variants.Length : 1;
                _byKind[kind.Id][p] = new Slot[variants];
                for (int v = 0; v < variants; v++)
                {
                    var slot = new Slot(Build(part, kind.Variants[v]));
                    _slots.Add(slot);
                    _byKind[kind.Id][p][v] = slot;
                }
            }
        }
    }

    /// <summary>Instanced draws the last <see cref="Draw"/> made.</summary>
    public int Draws { get; private set; }

    /// <summary>Mobs the last <see cref="Draw"/> drew.</summary>
    public int Drawn { get; private set; }

    // The corners of a box's six faces, +x, -x, +y, -y, +z and -z, as fractions of its size, each
    // face's two triangles turning counterclockwise seen from outside.
    private static readonly Vector3[][] Corners =
    [
        [new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1)],
        [new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)],
        [new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0)],
        [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)],
        [new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
        [new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0)],
    ];

    private static readonly Vector3[] Normals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];

    private static readonly Vector2[] Uvs = [new(0, 1), new(0, 0), new(1, 0), new(1, 1)];

    // A piece's boxes as one mesh in blocks, its own boxes in a variant's color.
    private static ModelMesh Build(ModelPart part, Color variant)
    {
        var vertices = new List<ModelVertex>();
        var colors = new List<Color>();
        var indices = new List<uint>();
        foreach (var box in part.Boxes)
        {
            Vector3 min = box.Min / 16, size = (box.Max - box.Min) / 16;
            for (int face = 0; face < 6; face++)
            {
                var first = (uint)vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(new ModelVertex(min + Corners[face][i] * size, Normals[face], Uvs[i]));
                    colors.Add(box.Color ?? variant);
                }
                indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
            }
        }
        return UploadMesh([.. vertices], [.. indices], [.. colors], null);
    }

    /// <summary>Draws the mobs within reach of the eye. Called inside <c>BeginMode3D</c>.</summary>
    public void Draw(IReadOnlyList<Mob> mobs, Vector3 eye)
    {
        foreach (var slot in _slots)
        {
            slot.Plain.Clear();
            slot.Hurt.Clear();
        }
        var drawn = 0;
        foreach (var mob in mobs)
        {
            if (Vector3.DistanceSquared(mob.Body.Position, eye) > Distance * Distance) continue;
            drawn++;
            var body = Matrix4x4.CreateRotationY(mob.Yaw) * Matrix4x4.CreateTranslation(mob.Body.Position);
            // A mob dying falls onto its side over most of a second.
            if (mob.Dying >= 0) body = Matrix4x4.CreateRotationZ(MathF.Sqrt(MathF.Min(1, mob.Dying / 0.6f)) * MathF.PI / 2) * body;
            var red = mob.Hurt > 0 || mob.Dying >= 0;
            var swing = MathF.Sin(mob.StridePhase) * 0.8f * mob.Stride;
            var parts = mob.Kind.Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                var part = parts[p];
                var turn = part.Swing switch
                {
                    Swing.Head => Matrix4x4.CreateRotationX(mob.HeadPitch) * Matrix4x4.CreateRotationY(mob.HeadYaw - mob.Yaw),
                    Swing.LegA => Matrix4x4.CreateRotationX(swing),
                    Swing.LegB => Matrix4x4.CreateRotationX(-swing),
                    Swing.LeftWing => Matrix4x4.CreateRotationZ(-mob.WingAngle),
                    Swing.RightWing => Matrix4x4.CreateRotationZ(mob.WingAngle),
                    _ => Matrix4x4.Identity,
                };
                var pivot = part.Pivot / 16;
                var local = turn.IsIdentity ? Matrix4x4.Identity : Matrix4x4.CreateTranslation(-pivot) * turn * Matrix4x4.CreateTranslation(pivot);
                var variants = _byKind[mob.Kind.Id][p];
                var slot = variants[variants.Length == 1 ? 0 : mob.Variant % variants.Length];
                (red ? slot.Hurt : slot.Plain).Add(local * body);
            }
        }

        var draws = 0;
        foreach (var slot in _slots)
        {
            if (slot.Plain.Count > 0)
            {
                DrawMeshInstanced(slot.Mesh, _skin, CollectionsMarshal.AsSpan(slot.Plain));
                draws++;
            }
            if (slot.Hurt.Count > 0)
            {
                DrawMeshInstanced(slot.Mesh, _hurt, CollectionsMarshal.AsSpan(slot.Hurt));
                draws++;
            }
        }
        (Draws, Drawn) = (draws, drawn);
    }

    public void Dispose()
    {
        foreach (var slot in _slots) UnloadMesh(slot.Mesh);
    }
}
