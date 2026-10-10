using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// Where the player stood and looked, the heading and pitch in degrees, the hour, the world's kind
/// and seed, and the key of each block by the number its columns were saved with, as a save keeps them.
/// </summary>
public sealed record WorldInfo(string Kind, int Seed, float X, float Y, float Z, float Heading, float Pitch, bool Flying, float Hour, string[] Blocks);

/// <summary>
/// A world's folder on disk: the columns changed from what the generator makes, in region files of
/// 32 by 32 columns, and the world's <see cref="WorldInfo"/>.
/// </summary>
/// <remarks>
/// <para>
/// A column the player never changed is not kept, since its seed makes it again the same. A changed
/// column is kept whole, its blocks compressed, and its light worked out again when it loads, as a
/// generated one's is. A region file holds every kept column of its 32 by 32 and is written whole,
/// to a file beside it that then takes its name, so a crash in the middle of a save leaves the
/// region as it was.
/// </para>
/// <para>
/// The main thread reads a region the first time a column in it is asked for and stores columns as
/// they unload. The workers that load columns only read the compressed bytes they are handed, so
/// the dictionary of kept columns is the one thing both touch.
/// </para>
/// </remarks>
public sealed class WorldSave
{
    public const int RegionSize = 32;
    private const int Version = 1;
    private static readonly byte[] Magic = "E3DR"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly ConcurrentDictionary<(int X, int Z), byte[]> _kept = new();
    private readonly HashSet<(int X, int Z)> _readRegions = [];
    private readonly HashSet<(int X, int Z)> _changedRegions = [];
    private ushort[]? _renumber;

    public WorldSave(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(Path.Combine(folder, "regions"));
    }

    /// <summary>The folder every world is saved under, one folder a world.</summary>
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "3DEngine.Game", "saves");

    public string Folder { get; }

    public string Name => Path.GetFileName(Folder);

    // A shift floors toward negative columns as a division would not.
    private static (int X, int Z) RegionOf(int x, int z) => (x >> 5, z >> 5);

    private string RegionPath((int X, int Z) region) => Path.Combine(Folder, "regions", $"r.{region.X}.{region.Z}.bin");

    /// <summary>Reads the region holding a column the first time it is asked for, so <see cref="Kept"/> answers for every column in it.</summary>
    public void ReadRegion(int x, int z)
    {
        var region = RegionOf(x, z);
        if (!_readRegions.Add(region)) return;
        var path = RegionPath(region);
        if (!File.Exists(path)) return;
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (!reader.ReadBytes(4).AsSpan().SequenceEqual(Magic) || reader.ReadInt32() != Version)
                throw new InvalidDataException("it is not a region of this version");
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int lx = reader.ReadInt16(), lz = reader.ReadInt16(), length = reader.ReadInt32();
                _kept[(region.X * RegionSize + lx, region.Z * RegionSize + lz)] = reader.ReadBytes(length);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException)
        {
            // The region's columns are made again by the generator rather than the world not loading.
            TraceLog(LogLevel.Warning, $"The region {path} could not be read, so its columns are generated again: {ex.Message}");
        }
    }

    /// <summary>A kept column's compressed blocks, or null for one the generator makes. Safe to call from a worker.</summary>
    public byte[]? Kept(int x, int z) => _kept.GetValueOrDefault((x, z));

    /// <summary>
    /// The number each saved block takes in this build, by its number when saved, from the keys the
    /// save's info lists, so blocks added or reordered since keep what was built. A key no longer
    /// known becomes air. Null where the numbers are the same.
    /// </summary>
    public ushort[]? Renumber => _renumber;

    private void ReadBlockKeys(string[] keys)
    {
        var renumber = new ushort[keys.Length];
        var same = keys.Length <= Blocks.All.Count;
        for (int i = 0; i < keys.Length; i++)
        {
            var known = Blocks.All.FirstOrDefault(b => b.Key == keys[i]);
            renumber[i] = (ushort)(known?.Id ?? BlockId.Air);
            same &= renumber[i] == i;
        }
        _renumber = same ? null : renumber;
    }

    /// <summary>Keeps a changed column, to be written with its region at the next <see cref="Flush"/>.</summary>
    public void Keep(ChunkColumn column)
    {
        ReadRegion(column.X, column.Z);
        _kept[(column.X, column.Z)] = Encode(column);
        _changedRegions.Add(RegionOf(column.X, column.Z));
        column.Changed = false;
    }

    /// <summary>Writes every region a column was kept in since the last flush.</summary>
    public void Flush()
    {
        foreach (var region in _changedRegions)
        {
            var columns = _kept.Where(c => RegionOf(c.Key.X, c.Key.Z) == region).ToList();
            var path = RegionPath(region);
            var written = path + ".new";
            using (var writer = new BinaryWriter(File.Create(written)))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(columns.Count);
                foreach (var ((x, z), bytes) in columns)
                {
                    writer.Write((short)(x - region.X * RegionSize));
                    writer.Write((short)(z - region.Z * RegionSize));
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
            }
            File.Move(written, path, overwrite: true);
        }
        _changedRegions.Clear();
    }

    /// <summary>The world's info, or null for a folder never saved into.</summary>
    public WorldInfo? ReadInfo()
    {
        var path = Path.Combine(Folder, "world.json");
        if (!File.Exists(path)) return null;
        try
        {
            var info = JsonSerializer.Deserialize<WorldInfo>(File.ReadAllText(path), Json);
            if (info?.Blocks is { } keys) ReadBlockKeys(keys);
            return info;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            TraceLog(LogLevel.Warning, $"{path} could not be read, so the world begins as a new one of its name: {ex.Message}");
            return null;
        }
    }

    public void WriteInfo(WorldInfo info)
    {
        var path = Path.Combine(Folder, "world.json");
        File.WriteAllText(path + ".new", JsonSerializer.Serialize(info, Json));
        File.Move(path + ".new", path, overwrite: true);
    }

    // A column's blocks, every section's in turn, compressed. Its light is left out, since it is
    // worked out again when the column loads.
    private static byte[] Encode(ChunkColumn column)
    {
        using var output = new MemoryStream();
        using (var zip = new ZLibStream(output, CompressionLevel.Fastest))
            foreach (var section in column.Sections)
                zip.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(section.Blocks.AsSpan()));
        return output.ToArray();
    }

    /// <summary>Makes a kept column again from its compressed blocks, their numbers changed by <paramref name="renumber"/> where given. Safe to call from a worker.</summary>
    public static ChunkColumn Decode(int x, int z, byte[] bytes, ushort[]? renumber)
    {
        var column = new ChunkColumn(x, z);
        using var zip = new ZLibStream(new MemoryStream(bytes), CompressionMode.Decompress);
        var raw = new byte[Section.Volume * sizeof(ushort)];
        foreach (var section in column.Sections)
        {
            zip.ReadExactly(raw);
            var blocks = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(raw);
            for (int i = 0; i < Section.Volume; i++)
            {
                var block = blocks[i];
                if (renumber is not null) block = block < renumber.Length ? renumber[block] : (ushort)0;
                if (block != 0) section.Set(i & Section.Mask, i >> 8, (i >> 4) & Section.Mask, (BlockId)block);
            }
        }
        column.Changed = false;
        return column;
    }
}
