namespace Engine;

/// <summary>
/// The edit meshes the console's model commands reach by name, those the commands made and those a
/// program named with <see cref="E3D.SetEditMeshName"/>, and the entity drawing each one shown.
/// </summary>
/// <remarks>
/// <para>
/// A resource of the app's world, so the names last while the app runs and the next app starts with
/// none, since the entities that drew them were the last app's. An edit mesh itself is the
/// process's (<see cref="EditMeshStore"/>), so a mesh the commands made is freed when its name is
/// forgotten or given to another mesh, and one a program named stays the program's to free.
/// </para>
/// <para>
/// A model shown is drawn by <see cref="Draw"/> in <see cref="Stage.Render"/>, through each camera
/// mesh entities are drawn through, at its entity's transform, from a model built from the edit
/// mesh and built again in place when the mesh's version moves on, so it follows every operation
/// the commands or the program run on it.
/// </para>
/// </remarks>
internal sealed class EditMeshNames
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Named> _named = new(StringComparer.Ordinal);

    // The models of names forgotten or of entities despawned, unloaded by the next frame's draw
    // through the flat API they were built through.
    private readonly List<Model> _retired = [];

    /// <summary>A mesh by its name, with whether the commands made it and what draws it where it is shown.</summary>
    internal sealed class Named(EditMesh mesh, bool made)
    {
        public EditMesh Mesh = mesh;
        public bool Made = made;
        public Entity Shown = Entity.None;
        public Model? Model;
        public int DrawnVersion = -1;
    }

    /// <summary>The names of the world's app, made the first time a name is given.</summary>
    public static EditMeshNames Of(World world) => world.GetOrInsertResource(static () => new EditMeshNames());

    /// <summary>Whether a name is one word, as the commands read it.</summary>
    public static bool IsName(string? name) => !string.IsNullOrEmpty(name) && !name.Any(char.IsWhiteSpace);

    /// <summary>Gives a mesh a name, in place of the mesh of that name, which keeps the entity showing it.</summary>
    /// <param name="name">One word.</param>
    /// <param name="mesh">The mesh.</param>
    /// <param name="made">Whether the commands made the mesh, so forgetting it frees it.</param>
    public void Name(string name, EditMesh mesh, bool made)
    {
        lock (_gate)
        {
            if (!_named.TryGetValue(name, out var named))
            {
                _named[name] = new Named(mesh, made);
                return;
            }

            if (named.Mesh != mesh && named.Made && !Uses(named.Mesh, name)) EditMeshStore.Remove(named.Mesh);
            named.Mesh = mesh;
            named.Made = made;
            named.DrawnVersion = -1;
        }
    }

    /// <summary>The loaded mesh of a name, or null where no loaded mesh has it.</summary>
    public Named? Find(string name)
    {
        lock (_gate) return _named.TryGetValue(name, out var named) && EditMeshStore.TryGet(named.Mesh, out _) ? named : null;
    }

    /// <summary>The names whose meshes are loaded, in order.</summary>
    public IReadOnlyList<(string Name, EditMesh Mesh)> All
    {
        get
        {
            lock (_gate)
                return [.. _named.Where(pair => EditMeshStore.TryGet(pair.Value.Mesh, out _)).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => (pair.Key, pair.Value.Mesh))];
        }
    }

    /// <summary>Forgets a name, freeing its mesh where the commands made it and no other name has it, and despawning what showed it.</summary>
    /// <returns>Whether there was such a name.</returns>
    public bool Forget(string name, EcsWorld? ecs)
    {
        lock (_gate)
        {
            if (!_named.Remove(name, out var named)) return false;
            if (named.Made && !Uses(named.Mesh, name)) EditMeshStore.Remove(named.Mesh);
            if (ecs is not null && !named.Shown.IsNone) ecs.Despawn(named.Shown);
            if (named.Model is { } model) _retired.Add(model);
            return true;
        }
    }

    /// <summary>Shows a mesh at a point, spawning the entity that draws it or moving the one that does.</summary>
    public void Show(EcsWorld ecs, Named named, string name, System.Numerics.Vector3 at)
    {
        if (ecs.TryResolve(named.Shown, out var entity))
        {
            ecs.Update(entity, new Transform(at));
            return;
        }

        entity = ecs.Spawn();
        ecs.Add(entity, new Transform(at));
        ecs.Add(entity, new Name($"Model {name}"));
        named.Shown = ecs.Handle(entity);
    }

    // Whether another name than this has the mesh, which then stays loaded.
    private bool Uses(EditMesh mesh, string except) =>
        _named.Any(pair => pair.Key != except && pair.Value.Mesh == mesh);

    /// <summary>
    /// The system, for <see cref="Stage.Render"/>, in <paramref name="app"/>, which draws each shown
    /// mesh at its entity, the flat API made to run against the app while it records where it runs
    /// against another or none, as <see cref="AnimatedModelDraws"/> does.
    /// </summary>
    public static void Draw(World world, App app)
    {
        if (!world.TryGetResource<EditMeshNames>(out var names) || !world.TryGetResource<EcsWorld>(out var ecs)) return;
        var current = E3D.CurrentApp;
        var borrowed = !E3D.Holds(world) && ReferenceEquals(app.World, world);
        if (!borrowed && !E3D.Holds(world)) return;
        if (borrowed) E3D.UseApp(app);
        try
        {
            names.Record(world, ecs);
        }
        finally
        {
            if (borrowed) E3D.UseApp(current);
        }
    }

    private void Record(World world, EcsWorld ecs)
    {
        Named[] shown;
        Model[] retired;
        lock (_gate)
        {
            shown = [.. _named.Values.Where(named => !named.Shown.IsNone)];
            retired = [.. _retired];
            _retired.Clear();
        }

        foreach (var model in retired) E3D.UnloadModel(model);
        if (shown.Length == 0) return;

        var cameras = MeshEntityDraws.Cameras(world, ecs);
        foreach (var named in shown)
        {
            if (!ecs.TryResolve(named.Shown, out var entity))
            {
                // The entity was despawned by other means, as a scene cleared, so the mesh is no
                // longer shown and its model is let go.
                named.Shown = Entity.None;
                if (named.Model is { } gone) E3D.UnloadModel(gone);
                named.Model = null;
                continue;
            }

            if (!EditMeshStore.TryGet(named.Mesh, out var data)) continue;
            if (named.Model is null) named.Model = E3D.LoadModelFromEditMesh(named.Mesh);
            else if (named.DrawnVersion != data.Version) E3D.UpdateModelFromEditMesh(named.Model, named.Mesh);
            named.DrawnVersion = data.Version;

            var placed = TransformPropagation.WorldMatrix(ecs, entity);
            foreach (var (viewProjection, _, target) in cameras) E3D.RecordModel(named.Model, placed, viewProjection, target);
        }
    }
}
