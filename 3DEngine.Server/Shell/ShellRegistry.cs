namespace Editor.Shell;
/// <summary>
/// Central registry holding the merged <see cref="ShellDescriptor"/> assembled from
/// any number of named <see cref="ShellSource"/> contributions (e.g. the static
/// source-generator output and the dynamic Roslyn hot-reload compiler).
/// Observable - fires <see cref="Changed"/> whenever the merged descriptor is rebuilt.
/// </summary>
/// <remarks>
/// <para>
/// Sources are added via <see cref="RegisterSource"/> (upsert) and removed via
/// <see cref="RemoveSource"/>. After every mutation the registry rebuilds
/// <see cref="Current"/> by:
/// <list type="number">
///   <item><description>Running every <see cref="IEditorShellBuilder"/> from every source against a shared <see cref="ShellBuilder"/>, ordered by <see cref="IEditorShellBuilder.Order"/> ascending then by source <see cref="ShellSource.Precedence"/> ascending.</description></item>
///   <item><description>Appending all <see cref="ShellSource.PanelComponents"/> as additional <see cref="PanelDescriptor"/> entries (sorted by <see cref="EditorPanelAttribute.Order"/>).</description></item>
///   <item><description>Resolving <see cref="PanelDescriptor.Id"/> collisions by keeping the entry from the source with the highest <see cref="ShellSource.Precedence"/> (dynamic overrides static).</description></item>
///   <item><description>Concatenating all <see cref="ShellSource.CustomCss"/> snippets in source-precedence order.</description></item>
/// </list>
/// </para>
/// <para>
/// Thread safety: reads via <see cref="Current"/> / <see cref="Version"/> are guarded by a lock,
/// and <see cref="RegisterSource"/> / <see cref="RemoveSource"/> atomically swap the merged
/// descriptor before firing <see cref="Changed"/> outside the lock.
/// </para>
/// </remarks>
/// <seealso cref="ShellDescriptor"/>
/// <seealso cref="ShellSource"/>
/// <seealso cref="ShellSourceIds"/>
public sealed class ShellRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ShellSource> _sources = new(StringComparer.Ordinal);
    private ShellDescriptor _current = new();
    private int _version;
    /// <summary>Fired when the merged shell descriptor is replaced (source upserted/removed).</summary>
    public event Action? Changed;
    /// <summary>Monotonically increasing version; bumped on every merge.</summary>
    public int Version { get { lock (_lock) return _version; } }
    /// <summary>Current merged shell descriptor snapshot.</summary>
    public ShellDescriptor Current
    {
        get { lock (_lock) return _current; }
    }
    /// <summary>
    /// Atomically replaces the contribution for <paramref name="sourceId"/>, recomputes the
    /// merged descriptor, and fires <see cref="Changed"/>.
    /// </summary>
    /// <param name="sourceId">Source identifier (see <see cref="ShellSourceIds"/> for well-known values).</param>
    /// <param name="source">The new contribution. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="sourceId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public void RegisterSource(string sourceId, ShellSource source)
    {
        if (string.IsNullOrEmpty(sourceId)) throw new ArgumentException("sourceId required", nameof(sourceId));
        ArgumentNullException.ThrowIfNull(source);
        lock (_lock)
        {
            _sources[sourceId] = source;
            _current = Merge(_sources);
            _version++;
        }
        Changed?.Invoke();
    }
    /// <summary>
    /// Removes the contribution for <paramref name="sourceId"/> if present, recomputes the
    /// merged descriptor, and fires <see cref="Changed"/>. No-op when the source isn't registered.
    /// </summary>
    /// <param name="sourceId">Source identifier to remove.</param>
    public void RemoveSource(string sourceId)
    {
        bool changed;
        lock (_lock)
        {
            changed = _sources.Remove(sourceId);
            if (changed)
            {
                _current = Merge(_sources);
                _version++;
            }
        }
        if (changed) Changed?.Invoke();
    }
    /// <summary>
    /// Legacy single-source update kept for back-compat. Wraps <paramref name="descriptor"/> as a
    /// <see cref="ShellSourceIds.Dynamic"/> source and calls <see cref="RegisterSource"/>.
    /// </summary>
    /// <param name="descriptor">The descriptor to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    [Obsolete("Use RegisterSource with an explicit sourceId. Retained for back-compat.")]
    public void Update(ShellDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var source = new ShellSource
        {
            Builders = new IEditorShellBuilder[] { new PrebuiltDescriptorBuilder(descriptor) },
            CustomCss = descriptor.CustomCss.ToArray(),
            Precedence = 100,
        };
        RegisterSource(ShellSourceIds.Dynamic, source);
    }
    private static ShellDescriptor Merge(Dictionary<string, ShellSource> sources)
    {
        var orderedSources = sources
            .OrderBy(kv => kv.Value.Precedence)
            .ToList();
        // Builders ordered by IEditorShellBuilder.Order, ties broken by source precedence
        // (low precedence first so high-precedence sources execute later and overwrite).
        var allBuilders = orderedSources
            .SelectMany(kv => kv.Value.Builders.Select(b => (Builder: b, kv.Value.Precedence)))
            .OrderBy(t => t.Builder.Order)
            .ThenBy(t => t.Precedence);
        var shellBuilder = new ShellBuilder();
        foreach (var (b, _) in allBuilders)
        {
            try { b.Build(shellBuilder); }
            catch { /* one bad builder must not break the registry */ }
        }
        var merged = shellBuilder.Build();
        // Append [EditorPanel] component panels in precedence order.
        foreach (var (_, src) in orderedSources)
        {
            foreach (var (attr, type) in src.PanelComponents.OrderBy(p => p.Attr.Order))
            {
                merged.Panels.Add(new PanelDescriptor
                {
                    Id = attr.Id,
                    Title = attr.Title,
                    DefaultZone = attr.Zone,
                    ComponentType = type,
                    Icon = attr.Icon,
                    Route = attr.Route,
                    TabGroupId = attr.TabGroup,
                    TabOrder = attr.TabOrder,
                    InitialSize = attr.InitialSize,
                    Closeable = attr.Closeable,
                    Visible = attr.Visible,
                });
            }
        }
        // Collision policy: panels with the same Id - keep the LAST occurrence (higher-precedence
        // sources were appended later). Empty-id panels are kept verbatim.
        var lastIndexOfId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < merged.Panels.Count; i++)
        {
            var id = merged.Panels[i].Id;
            if (!string.IsNullOrEmpty(id)) lastIndexOfId[id] = i;
        }
        var deduped = new List<PanelDescriptor>(merged.Panels.Count);
        for (int i = 0; i < merged.Panels.Count; i++)
        {
            var p = merged.Panels[i];
            if (string.IsNullOrEmpty(p.Id) || lastIndexOfId[p.Id] == i)
                deduped.Add(p);
        }
        merged.Panels = deduped;
        merged.CustomCss = orderedSources.SelectMany(kv => kv.Value.CustomCss).ToList();
        return merged;
    }
    /// <summary>Adapter that re-exposes a pre-built <see cref="ShellDescriptor"/> as an <see cref="IEditorShellBuilder"/>.</summary>
    private sealed class PrebuiltDescriptorBuilder(ShellDescriptor descriptor) : IEditorShellBuilder
    {
        public int Order => 0;
        public void Build(IShellBuilder shell)
        {
            foreach (var p in descriptor.Panels)
            {
                shell.Panel(p.Id, p.Title, p.DefaultZone, b =>
                {
                    if (p.Icon is not null) b.Icon(p.Icon);
                    if (p.TabGroupId is not null) b.TabGroup(p.TabGroupId, p.TabOrder);
                    b.InitialSize(p.InitialSize);
                    b.Closeable(p.Closeable);
                    b.Visible(p.Visible);
                    if (p.Route is not null) b.Route(p.Route);
                    if (!string.IsNullOrEmpty(p.WidgetKey)) b.Widget(p.WidgetKey);
                });
            }
            foreach (var (k, v) in descriptor.Metadata) shell.Meta(k, v);
        }
    }
}
