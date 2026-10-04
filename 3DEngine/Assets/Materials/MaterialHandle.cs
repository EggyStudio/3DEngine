using System.Numerics;

namespace Engine;

/// <summary>
/// Lightweight value-type handle to a material inside a <see cref="MaterialLibrary"/>.
/// Safe to store on ECS components, copy by value, and use across frames; all operations
/// are forwarded to the owning library via the embedded reference, so backend types
/// (MaterialX documents, USD shade graphs, etc.) never leak into user code.
/// </summary>
/// <example>
/// <code>
/// var lib = ctx.World.Resource&lt;MaterialLibrary&gt;();
/// MaterialHandle gold = lib.Create(new MaterialDescription { Name = "Gold", MetallicFactor = 1f });
/// gold.SetBaseColor(new Vector4(1f, 0.86f, 0.57f, 1f));
/// </code>
/// </example>
public readonly struct MaterialHandle : IEquatable<MaterialHandle>
{
    /// <summary>The library that owns this material. <c>null</c> for the default/uninitialised handle.</summary>
    public readonly MaterialLibrary? Library;

    /// <summary>Backend-agnostic numeric id assigned by the owning library at creation.</summary>
    public readonly int Id;

    /// <summary>Constructs a material handle. Use <see cref="MaterialLibrary"/> creation methods instead of calling this directly.</summary>
    public MaterialHandle(MaterialLibrary library, int id)
    {
        Library = library;
        Id = id;
    }

    /// <summary><c>true</c> if this handle still resolves to a live entry in its owning library.</summary>
    public bool IsValid => Library is not null && Library.Exists(this);

    /// <summary>The display name of the underlying material.</summary>
    public string Name => Library!.GetName(this);

    /// <summary>Returns a snapshot of the underlying <see cref="MaterialDescription"/> (callers may mutate the clone freely).</summary>
    public MaterialDescription GetDescription() => Library!.GetDescription(this);

    /// <summary>Replaces the underlying description in-place. The handle id is preserved.</summary>
    public void SetDescription(MaterialDescription description) => Library!.Update(this, description);

    // -- convenience factor setters

    /// <summary>Updates the linear-RGBA base-color factor on the underlying description.</summary>
    public void SetBaseColor(Vector4 color) => Library!.Mutate(this, m => m.BaseColorFactor = color);

    /// <summary>Updates the metallic factor on the underlying description.</summary>
    public void SetMetallic(float metallic) => Library!.Mutate(this, m => m.MetallicFactor = metallic);

    /// <summary>Updates the roughness factor on the underlying description.</summary>
    public void SetRoughness(float roughness) => Library!.Mutate(this, m => m.RoughnessFactor = roughness);

    /// <summary>Updates the linear emissive-color factor on the underlying description.</summary>
    public void SetEmissive(Vector3 emissive) => Library!.Mutate(this, m => m.EmissiveFactor = emissive);

    /// <summary>Removes the material from the library. The handle becomes invalid afterwards.</summary>
    public void Destroy() => Library!.Destroy(this);

    /// <inheritdoc />
    public bool Equals(MaterialHandle other) => ReferenceEquals(Library, other.Library) && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MaterialHandle h && Equals(h);

    /// <inheritdoc />
    public override int GetHashCode() => Id;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(MaterialHandle a, MaterialHandle b) => a.Equals(b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(MaterialHandle a, MaterialHandle b) => !a.Equals(b);
}