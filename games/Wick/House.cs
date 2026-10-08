// The house of Wick, read from a map of characters and drawn with the engine's flat API, kept apart
// from the game so the engine's tests draw a room of it as the game does.
using System.Numerics;
using Engine;
using static Engine.Engine3D;

namespace Wick;

/// <summary>
/// The house: a grid of cells two meters across, each a wall, a floor, a pit, a wick or the door,
/// from a map in which <c>#</c> is a wall, <c>.</c> a floor, <c>d</c> a doorway, <c>o</c> a pit,
/// <c>w</c> a wick to light, <c>S</c> where the player starts and <c>E</c> the door out.
/// </summary>
public sealed class House
{
    /// <summary>The width of a cell in meters.</summary>
    public const float Cell = 2;

    /// <summary>How high the walls stand, above the lamp a player carries.</summary>
    public const float WallHeight = 2.2f;

    private readonly string[] _rows;
    private Pieces? _pieces;

    private House(string[] rows)
    {
        _rows = rows;
        for (int z = 0; z < rows.Length; z++)
            for (int x = 0; x < rows[z].Length; x++)
            {
                if (rows[z][x] == 'w') Wicks.Add((x, z));
                if (rows[z][x] == 'S') Start = (x, z);
                if (rows[z][x] == 'E') Door = (x, z);
            }
    }

    /// <summary>The cells across, west to east.</summary>
    public int Width => _rows[0].Length;

    /// <summary>The cells down the map, north to south.</summary>
    public int Depth => _rows.Length;

    /// <summary>The wicks' cells, in the order the map lists them.</summary>
    public List<(int X, int Z)> Wicks { get; } = [];

    /// <summary>The cell the player starts in.</summary>
    public (int X, int Z) Start { get; }

    /// <summary>The door's cell, which leads out once every wick is lit.</summary>
    public (int X, int Z) Door { get; }

    /// <summary>Reads the map, every row as wide as the first.</summary>
    public static House Load(string path)
    {
        var text = LoadFileText(path) ?? throw new FileNotFoundException($"There is no map {path}", path);
        var rows = text.Split('\n').Select(row => row.TrimEnd('\r')).Where(row => row.Length > 0).ToArray();
        if (rows.Length == 0 || rows.Any(row => row.Length != rows[0].Length))
            throw new InvalidDataException($"{path} is not a map of rows of one width");
        return new House(rows);
    }

    /// <summary>The middle of a cell on the floor.</summary>
    public static Vector3 Center((int X, int Z) cell) => new((cell.X + 0.5f) * Cell, 0, (cell.Z + 0.5f) * Cell);

    /// <summary>The cell a point of the floor lies in.</summary>
    public static (int X, int Z) CellAt(Vector3 at) => ((int)MathF.Floor(at.X / Cell), (int)MathF.Floor(at.Z / Cell));

    /// <summary>The map's character for a cell, a wall outside it.</summary>
    public char At((int X, int Z) cell) =>
        cell.X < 0 || cell.Z < 0 || cell.X >= Width || cell.Z >= Depth ? '#' : _rows[cell.Z][cell.X];

    /// <summary>Whether a cell is a wall, which nothing walks through.</summary>
    public bool IsWall((int X, int Z) cell) => At(cell) == '#';

    /// <summary>Whether a cell is a pit, which a player falls into.</summary>
    public bool IsPit((int X, int Z) cell) => At(cell) == 'o';

    /// <summary>
    /// Turns on what lights the house: the scene's distance field over the whole house and the
    /// light that bounces through it, and no light from all around, so a room no lamp reaches is
    /// dark.
    /// </summary>
    public static void Light(GlobalIllumination quality)
    {
        // Two cascades of 0.25 reach 32 meters across, the house's 26 in the second.
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(quality);
        SetBloom(0.6f);
    }

    /// <summary>
    /// The lamp a player carries, warm and casting shadows, so its light stops at a wall and goes
    /// round a corner only as light that bounces.
    /// </summary>
    public static LightHandle Lamp(Vector3 at) => CreatePointLight(at, new Color(255, 196, 130), 7, range: 12, castsShadows: true);

    /// <summary>The light of a wick that burns, above its candle, casting shadows as the lamp does.</summary>
    public LightHandle Flame(int wick) =>
        CreatePointLight(Center(Wicks[wick]) + new Vector3(0, 0.6f, 0), new Color(255, 170, 90), 2.5f, range: 5, castsShadows: true);

    /// <summary>Lets the models the house is drawn with go, which the next draw makes again.</summary>
    public void UnloadModels()
    {
        _pieces?.Unload();
        _pieces = null;
    }

    /// <summary>
    /// Draws the house between <c>BeginMode3D</c> and <c>EndMode3D</c>: its walls, its floors,
    /// polished so they reflect the light on them, a candle at each wick, its flame where
    /// <paramref name="lit"/> says, and the door, glowing once <paramref name="open"/>.
    /// </summary>
    public void Draw(IReadOnlyList<bool> lit, bool open)
    {
        var pieces = _pieces ??= new Pieces();
        var wall = new Color(215, 200, 178);
        for (int z = 0; z < Depth; z++)
            for (int x = 0; x < Width; x++)
            {
                var at = Center((x, z));
                if (IsWall((x, z)))
                {
                    DrawModelEx(pieces.Wall, at + new Vector3(0, WallHeight / 2, 0), Vector3.UnitY, 0, new Vector3(Cell, WallHeight, Cell), wall);
                    continue;
                }
                if (IsPit((x, z))) continue;
                // A checker of two tones, so the floor's cells are told apart.
                var tone = (x + z) % 2 == 0 ? new Color(150, 128, 104) : new Color(120, 102, 84);
                DrawModelEx(pieces.Floor, at - new Vector3(0, 0.1f, 0), Vector3.UnitY, 0, new Vector3(Cell, 0.2f, Cell), tone);
            }

        for (int i = 0; i < Wicks.Count; i++)
        {
            var at = Center(Wicks[i]);
            DrawModel(pieces.Wax, at, 1, Color.White);
            if (lit[i]) DrawModel(pieces.Flame, at + new Vector3(0, 0.55f, 0), 1, Color.White);
        }

        // The door, a slab in a brass frame, its slab glowing the way out once open.
        var door = Center(Door);
        DrawModelEx(open ? pieces.Open : pieces.Shut, door + new Vector3(0, 0.02f, 0), Vector3.UnitY, 0, new Vector3(1.2f, 0.06f, 1.2f), Color.White);
        foreach (var (dx, dz, w, d) in new[] { (0f, -0.65f, 1.4f, 0.1f), (0f, 0.65f, 1.4f, 0.1f), (-0.65f, 0f, 0.1f, 1.4f), (0.65f, 0f, 0.1f, 1.4f) })
            DrawModelEx(pieces.Frame, door + new Vector3(dx, 0.05f, dz), Vector3.UnitY, 0, new Vector3(w, 0.1f, d), Color.White);
    }

    // The models the house is drawn with, made the first time it is drawn.
    private sealed class Pieces
    {
        public readonly Model Wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // The floors dark and polished, so they reflect the light on them.
        public readonly Model Floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        public readonly Model Wax = LoadModelFromMesh(GenMeshCylinder(0.12f, 0.45f, 12));
        public readonly Model Flame = LoadModelFromMesh(GenMeshSphere(0.08f, 10, 10));
        public readonly Model Frame = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        public readonly Model Shut = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        public readonly Model Open = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        public Pieces()
        {
            Wall.Materials[0] = new ModelMaterial(Color.White) { Roughness = 0.9f };
            Floor.Materials[0] = new ModelMaterial(Color.White) { Roughness = 0.12f };
            Wax.Materials[0] = new ModelMaterial(new Color(235, 225, 200)) { Roughness = 0.6f };
            Flame.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 170, 70), EmissiveIntensity = 10, CastsShadows = false };
            Frame.Materials[0] = new ModelMaterial(new Color(190, 150, 80)) { Metallic = 1, Roughness = 0.3f };
            Shut.Materials[0] = new ModelMaterial(new Color(30, 30, 34)) { Roughness = 0.5f };
            Open.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(120, 220, 200), EmissiveIntensity = 3, CastsShadows = false };
        }

        public void Unload()
        {
            foreach (var model in new[] { Wall, Floor, Wax, Flame, Frame, Shut, Open }) UnloadModel(model);
        }
    }
}
