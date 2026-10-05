using System.Numerics;

namespace Engine;

/// <summary>
/// A box of the world that reflects what is around its middle, as a room reflects its walls rather
/// than the sky, placed by the entity's <see cref="Transform"/>.
/// </summary>
/// <remarks>
/// <para>
/// The renderer draws the frame's meshes from the probe's position into the six faces of a small
/// cube, the first frame it sees the probe and again whenever the probe moves, changes size or
/// <see cref="Capture"/> changes, and prefilters it as it does the environment map. A surface
/// inside the box then reflects the cube, looked up where the reflected ray leaves the box, so
/// the walls hold still as the camera moves, and takes its diffuse light from it, where it would
/// take them from the environment map. A surface in no box keeps the environment map.
/// </para>
/// <para>
/// What it captures is what the window draws in the frame of the capture, lit as the window is,
/// so a probe is captured once its room is in the frame. A probe spawned with a scene captures
/// again once every model under the same root entity has spawned, as a room's prefab streamed in
/// brings its probe at once and its models a few frames later. Four probes are bound at once, those
/// nearest the camera, and where boxes overlap the smallest that holds a point wins.
/// </para>
/// <para>
/// A component made with <c>default</c> has no size and an intensity of 0, so
/// <see cref="ReflectionProbe(Vector3, float)"/> is the one to make.
/// </para>
/// </remarks>
[SceneComponent]
public struct ReflectionProbe
{
    /// <summary>The size of the box, in world units, around the entity's position.</summary>
    public Vector3 Size;

    /// <summary>What the captured light is multiplied by, 1 as captured.</summary>
    public float Intensity;

    /// <summary>A count raised to capture the probe again, after its room has changed.</summary>
    public int Capture;

    /// <summary>A probe of a box of <paramref name="size"/>, its light as captured.</summary>
    public ReflectionProbe(Vector3 size, float intensity = 1)
    {
        Size = size;
        Intensity = intensity;
        Capture = 0;
    }
}

/// <summary>
/// Every probe the frame has and what was captured for each, which the renderer reads and fills,
/// one object shared by the main world and the render world.
/// </summary>
internal sealed class ReflectionProbes
{
    /// <summary>Each probe by the entity that holds it.</summary>
    public readonly Dictionary<int, Probe> ByEntity = [];

    /// <summary>One probe and its capture.</summary>
    public sealed class Probe
    {
        // The entity's generation, so an id given out again starts a probe of its own.
        public required int Generation;
        public Vector3 Position;
        public Vector3 HalfSize;
        public float Intensity;

        // What the probe was asked to be captured as, and what its map was captured as, which
        // differ until a capture finishes.
        public (Vector3 Position, Vector3 Size, int Capture) Wanted;
        public (Vector3 Position, Vector3 Size, int Capture)? Captured;
        public bool Capturing;

        // How many captures of what is wanted have finished, which reaches Passes.
        public int Passes;

        // The prefiltered capture, and a finished one the thread that built it hands over, which
        // Sync takes on the main thread.
        public EnvironmentMap? Map;
        public volatile Capture? Done;

        /// <summary>Whether the probe has a map to reflect.</summary>
        public bool Ready => Map is not null;
    }

    /// <summary>How many times a probe is captured for one placement, each with the one before bound.</summary>
    public const int Passes = 2;

    /// <summary>A capture finished on a worker thread, and what it was captured as.</summary>
    public sealed record Capture(EnvironmentMap Map, (Vector3 Position, Vector3 Size, int Capture) As);

    /// <summary>
    /// Takes the probes the ECS holds, adding new ones and forgetting those gone, and the captures
    /// finished since the last frame.
    /// </summary>
    public void Sync(EcsWorld ecs)
    {
        var seen = new HashSet<int>();
        foreach (var (entity, probe) in ecs.Query<ReflectionProbe>())
        {
            seen.Add(entity);
            var position = TransformPropagation.WorldMatrix(ecs, entity).Translation;
            var generation = ecs.GetGeneration(entity);
            if (!ByEntity.TryGetValue(entity, out var known) || known.Generation != generation)
                ByEntity[entity] = known = new Probe { Generation = generation };
            known.Position = position;
            known.HalfSize = Vector3.Abs(probe.Size) / 2;
            known.Intensity = probe.Intensity;
            var wanted = (position, probe.Size, probe.Capture);
            if (known.Wanted != wanted)
            {
                known.Wanted = wanted;
                known.Passes = 0;
            }
            if (known.Done is { } done)
            {
                known.Map = done.Map;
                known.Captured = done.As;
                known.Capturing = false;
                known.Done = null;
                if (done.As == known.Wanted) known.Passes++;
            }
        }
        foreach (var gone in ByEntity.Keys.Where(e => !seen.Contains(e)).ToArray()) ByEntity.Remove(gone);
    }
}
