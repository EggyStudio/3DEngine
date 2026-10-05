namespace Engine;

/// <summary>
/// Tunable defaults applied by <see cref="MaterialLibrary"/> when creating new materials.
/// Insert as a resource before adding <see cref="MaterialPlugin"/> to override the
/// engine-wide defaults.
/// </summary>
/// <example>
/// <code>
/// app.World.InsertResource(new MaterialSettings { DefaultDoubleSided = true });
/// app.AddPlugin(new MaterialPlugin());
/// </code>
/// </example>
internal sealed class MaterialSettings
{
    /// <summary>Default value of <see cref="MaterialDescription.DoubleSided"/> for newly created materials.</summary>
    public bool DefaultDoubleSided { get; set; }

    /// <summary>Default value of <see cref="MaterialDescription.AlphaMode"/> for newly created materials.</summary>
    public MaterialAlphaMode DefaultAlphaMode { get; set; } = MaterialAlphaMode.Opaque;

    /// <summary>Default U-axis wrap mode used by texture importers when the source asset is silent on it.</summary>
    public TextureWrapMode DefaultWrapS { get; set; } = TextureWrapMode.Repeat;

    /// <summary>Default V-axis wrap mode used by texture importers when the source asset is silent on it.</summary>
    public TextureWrapMode DefaultWrapT { get; set; } = TextureWrapMode.Repeat;

    /// <summary>
    /// When <c>true</c>, <see cref="MaterialLibrary.CreateOrGet(MaterialDescription)"/> uses
    /// <see cref="MaterialDescription.SourcePath"/> as a stable cache key so importers that
    /// re-load the same source path don't duplicate entries. Default <c>true</c>.
    /// </summary>
    public bool DeduplicateBySourcePath { get; set; } = true;
}