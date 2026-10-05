using System.Diagnostics.CodeAnalysis;

namespace Engine;

/// <summary>
/// Engine-neutral, central registry of <see cref="MaterialDescription"/> instances.
/// Owns every authored material in the running app and hands out lightweight
/// <see cref="MaterialHandle"/> values to ECS components, render extracts and importers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where importers fit in:</b> a model reader turns its file's materials into
/// <see cref="MaterialDescription"/> values and hands them to
/// <see cref="Create(MaterialDescription)"/> or <see cref="CreateOrGet(MaterialDescription)"/>.
/// The library knows the description alone, so materials a program makes and materials a file
/// brings are used alike, through the same handle type.
/// </para>
/// <para>
/// <b>De-duplication:</b> when <see cref="MaterialSettings.DeduplicateBySourcePath"/> is
/// enabled (the default) <see cref="CreateOrGet(MaterialDescription)"/> reuses the
/// existing handle for any description whose <see cref="MaterialDescription.SourcePath"/>
/// has already been registered, so a material a file names twice is made once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var lib = new MaterialLibrary();
/// var gold = lib.Create(new MaterialDescription
/// {
///     Name = "Gold",
///     BaseColorFactor = new Vector4(1.0f, 0.86f, 0.57f, 1f),
///     MetallicFactor = 1f,
///     RoughnessFactor = 0.2f,
/// });
/// gold.SetRoughness(0.15f);
/// </code>
/// </example>
public sealed class MaterialLibrary
{
    private static readonly ILogger Logger = Log.Category("Engine.Materials");

    private readonly MaterialSettings _settings;
    private readonly Dictionary<int, MaterialDescription> _byId = new();
    private readonly Dictionary<string, int> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _bySourcePath = new(StringComparer.Ordinal);
    private int _nextId = 1;

    /// <summary>Number of materials currently registered.</summary>
    public int Count => _byId.Count;

    /// <summary>Settings controlling default factor values and de-duplication behaviour.</summary>
    public MaterialSettings Settings => _settings;

    /// <summary>Creates a library using the default <see cref="MaterialSettings"/>.</summary>
    public MaterialLibrary() : this(new MaterialSettings()) { }

    /// <summary>Creates a library with explicit <see cref="MaterialSettings"/> (used by <see cref="MaterialPlugin"/>).</summary>
    public MaterialLibrary(MaterialSettings settings)
    {
        _settings = settings;
        Logger.Info("MaterialLibrary: created.");
    }

    /// <summary>
    /// Registers a new material and returns its handle. Settings-driven defaults are
    /// applied to the description in-place when fields are left at their type defaults.
    /// </summary>
    public MaterialHandle Create(MaterialDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        ApplyDefaults(description);

        int id = _nextId++;
        _byId[id] = description;
        _byName[description.Name] = id;
        if (description.SourcePath is { Length: > 0 } src)
            _bySourcePath[src] = id;

        Logger.Debug($"MaterialLibrary: created '{description.Name}' (id={id}, source='{description.SourcePath}').");
        return new MaterialHandle(this, id);
    }

    /// <summary>
    /// Registers <paramref name="description"/> only if no material with the same
    /// <see cref="MaterialDescription.SourcePath"/> is already registered (and
    /// <see cref="MaterialSettings.DeduplicateBySourcePath"/> is enabled). Otherwise
    /// returns the existing handle.
    /// </summary>
    internal MaterialHandle CreateOrGet(MaterialDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (_settings.DeduplicateBySourcePath
            && description.SourcePath is { Length: > 0 } src
            && _bySourcePath.TryGetValue(src, out var existing))
        {
            return new MaterialHandle(this, existing);
        }
        return Create(description);
    }

    /// <summary><c>true</c> if the handle still resolves to a live entry in this library.</summary>
    public bool Exists(MaterialHandle handle) => _byId.ContainsKey(handle.Id);

    /// <summary>Returns the display name of the material referenced by <paramref name="handle"/>.</summary>
    internal string GetName(MaterialHandle handle) => Get(handle).Name;

    /// <summary>
    /// Returns a <i>cloned</i> <see cref="MaterialDescription"/> for <paramref name="handle"/>.
    /// Mutating the clone has no effect; call <see cref="Update"/> or <see cref="Mutate"/>
    /// to write back.
    /// </summary>
    internal MaterialDescription GetDescription(MaterialHandle handle) => Get(handle).Clone();

    /// <summary>Replaces the description for <paramref name="handle"/> in-place. The handle id is preserved.</summary>
    public void Update(MaterialHandle handle, MaterialDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        var existing = Get(handle);
        if (!ReferenceEquals(existing.SourcePath, description.SourcePath))
        {
            if (existing.SourcePath is { Length: > 0 } oldSrc) _bySourcePath.Remove(oldSrc);
            if (description.SourcePath is { Length: > 0 } newSrc) _bySourcePath[newSrc] = handle.Id;
        }
        if (!string.Equals(existing.Name, description.Name, StringComparison.Ordinal))
        {
            _byName.Remove(existing.Name);
            _byName[description.Name] = handle.Id;
        }
        _byId[handle.Id] = description;
    }

    /// <summary>Applies <paramref name="mutator"/> to the in-place description for <paramref name="handle"/>.</summary>
    internal void Mutate(MaterialHandle handle, Action<MaterialDescription> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        var d = Get(handle);
        var oldName = d.Name;
        var oldSrc = d.SourcePath;
        mutator(d);
        if (!string.Equals(oldName, d.Name, StringComparison.Ordinal))
        {
            _byName.Remove(oldName);
            _byName[d.Name] = handle.Id;
        }
        if (!string.Equals(oldSrc, d.SourcePath, StringComparison.Ordinal))
        {
            if (oldSrc is { Length: > 0 }) _bySourcePath.Remove(oldSrc);
            if (d.SourcePath is { Length: > 0 } newSrc) _bySourcePath[newSrc] = handle.Id;
        }
    }

    /// <summary>Removes the material referenced by <paramref name="handle"/>.</summary>
    internal void Destroy(MaterialHandle handle)
    {
        if (!_byId.TryGetValue(handle.Id, out var d)) return;
        _byId.Remove(handle.Id);
        if (_byName.TryGetValue(d.Name, out var byNameId) && byNameId == handle.Id)
            _byName.Remove(d.Name);
        if (d.SourcePath is { Length: > 0 } src
            && _bySourcePath.TryGetValue(src, out var bySrcId) && bySrcId == handle.Id)
            _bySourcePath.Remove(src);
        Logger.Debug($"MaterialLibrary: destroyed material id={handle.Id} ('{d.Name}').");
    }

    /// <summary>Looks up a material by display <paramref name="name"/>.</summary>
    internal bool TryFindByName(string name, out MaterialHandle handle)
    {
        if (_byName.TryGetValue(name, out var id))
        {
            handle = new MaterialHandle(this, id);
            return true;
        }
        handle = default;
        return false;
    }

    /// <summary>Looks up a material by its <see cref="MaterialDescription.SourcePath"/>.</summary>
    internal bool TryFindBySourcePath(string sourcePath, out MaterialHandle handle)
    {
        if (_bySourcePath.TryGetValue(sourcePath, out var id))
        {
            handle = new MaterialHandle(this, id);
            return true;
        }
        handle = default;
        return false;
    }

    /// <summary>Enumerates every live handle in this library (snapshot at call time).</summary>
    public IEnumerable<MaterialHandle> Handles
    {
        get
        {
            foreach (var id in _byId.Keys) yield return new MaterialHandle(this, id);
        }
    }

    /// <summary>Removes every material; the next created material starts a fresh id sequence.</summary>
    public void Clear()
    {
        _byId.Clear();
        _byName.Clear();
        _bySourcePath.Clear();
        _nextId = 1;
    }

    private MaterialDescription Get(MaterialHandle handle)
    {
        if (!ReferenceEquals(handle.Library, this))
            throw new InvalidOperationException("MaterialHandle does not belong to this MaterialLibrary.");
        if (!_byId.TryGetValue(handle.Id, out var d))
            throw new InvalidOperationException($"MaterialHandle id={handle.Id} no longer exists in this library.");
        return d;
    }

    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    private void ApplyDefaults(MaterialDescription d)
    {
        // Only fill in fields the caller left at their *struct/enum* defaults so that
        // explicit author-time intent (zero metallic, opaque mode) is never overwritten.
        if (d.AlphaMode == MaterialAlphaMode.Opaque && _settings.DefaultAlphaMode != MaterialAlphaMode.Opaque)
            d.AlphaMode = _settings.DefaultAlphaMode;
        if (!d.DoubleSided && _settings.DefaultDoubleSided)
            d.DoubleSided = true;
    }
}