using System.Numerics;

namespace Engine;

// The material slots, each a name with a look, added, renamed and taken away, and given a color, a
// texture and the maps of the engine's material, every change a line of the recipe as a change of
// the shape is, so a saved model is drawn as it was left. The same words give the same slots in
// BevyCSharp's edit mesh.
internal sealed partial class EditMeshData
{
    private readonly List<SlotLook> _looks = [new()];

    /// <summary>What a slot is drawn with, by its index in <see cref="Slots"/>, nothing given where it is the prototype material of its name.</summary>
    public SlotLook LookAt(int slot) => _looks[slot];

    /// <summary>Adds a slot no face is in yet, drawn with the prototype material of its name.</summary>
    /// <exception cref="ArgumentException">The name is not one word, or the mesh has a slot of that name.</exception>
    public void SlotAdd(string slot)
    {
        OneWord(slot, nameof(slot));
        if (_slots.Contains(slot)) throw new ArgumentException($"There is a slot called {slot}.", nameof(slot));

        SlotIndex(slot);
        Changed();
        Did($"slot add {slot}");
    }

    /// <summary>Gives a slot another name, its faces and its look kept.</summary>
    /// <remarks>
    /// A slot whose look gives nothing is drawn with the prototype material of its name, so a slot
    /// renamed from <c>default</c> to <c>wall</c> is drawn as a wall's prototype after.
    /// </remarks>
    /// <exception cref="ArgumentException">The mesh has no slot of the first name, or the new name is not one word or is another slot's.</exception>
    public void SlotRename(string slot, string name)
    {
        var index = Existing(slot);
        OneWord(name, nameof(name));
        if (_slots.Contains(name)) throw new ArgumentException($"There is a slot called {name}.", nameof(name));

        _slots[index] = name;
        Changed();
        Did($"slot rename {slot} {name}");
    }

    /// <summary>Takes a slot away, its faces given the first slot, the slots after it moving down by one.</summary>
    /// <exception cref="ArgumentException">The mesh has no slot of that name.</exception>
    /// <exception cref="InvalidOperationException">The slot is the first, which every new face takes.</exception>
    public void SlotRemove(string slot)
    {
        var index = Existing(slot);
        if (index == 0) throw new InvalidOperationException($"{slot} is the first slot, which every new face takes, and stays.");

        foreach (var face in _faces)
        {
            if (face.Slot == index) face.Slot = 0;
            else if (face.Slot > index) face.Slot--;
        }

        _slots.RemoveAt(index);
        _looks.RemoveAt(index);
        Changed();
        Did($"slot remove {slot}");
    }

    /// <summary>Gives a slot a color, as sRGB numbers from zero to one, which tints its texture, or its grid where it has none, alpha below one drawing it blended.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A part is not from zero to one.</exception>
    public void SlotColor(string slot, Vector4 color)
    {
        foreach (var part in (ReadOnlySpan<float>)[color.X, color.Y, color.Z, color.W]) Fraction(part, "A color's parts run from zero to one.");
        Look(slot, look => look with { Color = color },
            $"slot color {slot} {F(color.X)} {F(color.Y)} {F(color.Z)}{(color.W == 1f ? "" : " " + F(color.W))}");
    }

    /// <summary>Gives a slot a map of the engine's material by its file, or takes it away with null, the base color's texture taking the grid's place.</summary>
    /// <exception cref="ArgumentException">The path has a space in it, or the map is one the engine's materials do not have.</exception>
    public void SlotMap(string slot, MaterialMapIndex map, string? path)
    {
        var file = MapPath(path);
        var (word, change) = map switch
        {
            MaterialMapIndex.Albedo => ("texture", (Func<SlotLook, SlotLook>)(look => look with { Texture = file })),
            MaterialMapIndex.Normal => ("normal-map", look => look with { NormalMap = file }),
            MaterialMapIndex.Occlusion => ("occlusion-map", look => look with { OcclusionMap = file }),
            MaterialMapIndex.Metalness or MaterialMapIndex.Roughness => ("metallic-roughness-map", look => look with { MetallicRoughnessMap = file }),
            MaterialMapIndex.Emission => ("emissive-map", look => look with { EmissiveMap = file }),
            _ => throw new ArgumentException($"A slot has no {map} map, as a material of the engine's has none.", nameof(map)),
        };
        Look(slot, change, $"slot {word} {slot} {file ?? "none"}");
    }

    /// <summary>Gives a slot an emissive color, in linear numbers that may pass one for a bright light.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A part is below zero or not finite.</exception>
    public void SlotEmissive(string slot, Vector3 color)
    {
        foreach (var part in (ReadOnlySpan<float>)[color.X, color.Y, color.Z])
            if (!(part >= 0f) || !float.IsFinite(part)) throw new ArgumentOutOfRangeException(nameof(color), part, "A light's parts are finite numbers, zero or more.");
        Look(slot, look => look with { Emissive = color }, $"slot emissive {slot} {V(color)}");
    }

    /// <summary>Sets how metal a slot's faces are, from zero to one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not from zero to one.</exception>
    public void SlotMetallic(string slot, float metallic)
    {
        Fraction(metallic, "Metalness runs from zero to one.");
        Look(slot, look => look with { Metallic = metallic }, $"slot metallic {slot} {F(metallic)}");
    }

    /// <summary>Sets how rough a slot's faces are, from zero, a mirror, to one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not from zero to one.</exception>
    public void SlotRoughness(string slot, float roughness)
    {
        Fraction(roughness, "Roughness runs from zero to one.");
        Look(slot, look => look with { Roughness = roughness }, $"slot roughness {slot} {F(roughness)}");
    }

    /// <summary>The width in pixels of the texture of the first selected face's slot, read from its file, or null where it has none or the file cannot be read.</summary>
    /// <remarks>Read for a density given without a width, which the recipe then keeps, so the line makes the same coordinates without the file.</remarks>
    public int? SelectedTextureWidth()
    {
        if (_selectedFaces.Count == 0 || _looks[_faces[_selectedFaces.Min()].Slot].Texture is not { } path) return null;
        if (E3D.ResolveFile(path) is not { } found) return null;
        try
        {
            using var stream = File.OpenRead(found);
            return StbImageSharp.ImageInfo.FromStream(stream) is { } info ? info.Width : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Changes a slot's look, the slot made where the mesh has none of its name, and writes the line.</summary>
    private void Look(string slot, Func<SlotLook, SlotLook> change, string line)
    {
        OneWord(slot, nameof(slot));
        var index = SlotIndex(slot);
        _looks[index] = change(_looks[index]);
        Changed();
        Did(line);
    }

    /// <summary>The index of a slot, made with a look, the prototype's where none is given, where the mesh has none of its name.</summary>
    /// <remarks>
    /// A slot found keeps its own look, so a model joined or cut with another is drawn in this
    /// mesh's look for a slot both have, and in the other's for a slot this one lacks.
    /// </remarks>
    private int SlotIndex(string slot, SlotLook? look = null)
    {
        var index = _slots.IndexOf(slot);
        if (index >= 0) return index;
        _slots.Add(slot);
        _looks.Add(look ?? new SlotLook());
        return _slots.Count - 1;
    }

    /// <summary>Takes every slot but the first away, and the first's name and look, as a mesh made again from nothing has.</summary>
    private void ForgetSlots()
    {
        _slots.RemoveRange(1, _slots.Count - 1);
        _slots[0] = DefaultSlot;
        _looks.RemoveRange(1, _looks.Count - 1);
        _looks[0] = new SlotLook();
    }

    private int Existing(string slot)
    {
        var index = _slots.IndexOf(slot);
        return index >= 0 ? index : throw new ArgumentException($"There is no slot called {slot}.", nameof(slot));
    }

    private static void Fraction(float value, string message)
    {
        if (!(value >= 0f && value <= 1f)) throw new ArgumentOutOfRangeException(nameof(value), value, message);
    }

    /// <summary>A map's path as the look keeps it, null taking the map away.</summary>
    private static string? MapPath(string? path)
    {
        if (path is null) return null;
        if (path.Length == 0 || path.Any(char.IsWhiteSpace) || path == "none")
            throw new ArgumentException("A texture's path is one word with no space in it, and none takes the texture away.", nameof(path));
        return path;
    }
}
