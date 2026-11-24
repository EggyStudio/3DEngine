using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>One drawn copy of a mesh for the device's ray tracing, where it is drawn and the light it sends back.</summary>
/// <param name="Mesh">What its mesh is known by, the vertices it was drawn with.</param>
/// <param name="World">Its mesh's space to the world.</param>
/// <param name="Color">Its color, linear.</param>
/// <param name="Emission">Its emission, linear.</param>
/// <param name="Roughness">Its material's roughness, which a path traced reference lights its face by.</param>
/// <param name="Metallic">Its material's metallic.</param>
/// <param name="Carried">Whether global illumination carries it as a glow light, whose light a probe's rays and a probe's path traced reference leave out where they meet it.</param>
/// <param name="LightThrough">The share of light that passes a unit of its depth (<see cref="ModelMaterial.LightThrough"/>), which a path traced reference crosses it by.</param>
/// <param name="Texture">Its color texture's <see cref="TextureStore"/> id, or 0 for none, which a path traced reference reads at each point it meets in place of <paramref name="Color"/>.</param>
/// <param name="NormalMap">Its tangent-space normal map's id, or 0 for none, which a path traced reference bends each face it meets by.</param>
/// <param name="NormalScale">How strongly the normal map bends the face.</param>
/// <param name="Tint">Its color before its texture, linear, which the texture's color is multiplied by.</param>
/// <param name="Blend">
/// For a copy the model pass blends at less than half of it, as a decal, its material's alpha,
/// which a path traced reference meets it by, times its texture's alpha where it meets it, and 0
/// for every copy that stands solid. Such a copy is held apart in the scene
/// (<see cref="GpuRayScenes.BlendedMask"/>), which a probe's rays and a reflection leave out.
/// </param>
/// <param name="DoubleSided">
/// Whether its back faces are drawn, without which a path traced reference meets a blended copy
/// from its front alone, as the model pass draws it.
/// </param>
internal readonly record struct RayInstance(object Mesh, Matrix4x4 World, Vector3 Color, Vector3 Emission, float Roughness = 1, float Metallic = 0, bool Carried = false,
    float LightThrough = 0, int Texture = 0, int NormalMap = 0, float NormalScale = 0, Vector3 Tint = default, float Blend = 0, bool DoubleSided = true);

/// <summary>
/// The window's meshes as the device's ray tracing sees them, a bottom-level acceleration structure
/// of each mesh's triangles, built the first frame the mesh is drawn, and a top-level one of every
/// drawn copy, built again each frame, with each copy's color and emission and each mesh's
/// corners, which a reflection the scene's distance field misses is traced and shaded with.
/// </summary>
/// <remarks>
/// The copies and their colors are written by the CPU into one of a ring of buffers a frame, as
/// many as frames may be in flight, so a frame never writes what one still drawing reads. The
/// corners of every mesh lie in one buffer, and their texture coordinates in another beside it,
/// which a path traced reference reads the copies' textures by. Where a new mesh does not fit, both
/// are made again at twice their room, as often as that falls short, with the corners they held
/// copied on the GPU by the frame's own commands, so the frame does not wait for the device, and
/// the buffers left are let go once no frame in flight reads them. A mesh that leaves gives its
/// corners back for the next that fits in them.
/// </remarks>
internal sealed class GpuRayScene : IDisposable
{
    internal sealed record Mesh(VkAccelerationStructureKHR Structure, AddressedBuffer Storage, ulong Address, int FirstCorner, int Corners);

    internal readonly Dictionary<object, Mesh> Meshes = new(ReferenceEqualityComparer.Instance);
    // The frame each mesh's structure was last placed in by a copy, by which one no copy has placed
    // for UnplacedFrames gives its structure back.
    internal readonly Dictionary<object, long> Placed = new(ReferenceEqualityComparer.Instance);
    internal AddressedBuffer? Corners;
    internal AddressedBuffer? Uvs;
    internal int CornerCount;
    // The corners the buffer holds of meshes whose structures were given back, until a mesh takes
    // them or the buffer is made again without them, and the runs they lie in, each its first
    // corner and its count, in the order they lie.
    internal int DeadCorners;
    internal readonly List<(int First, int Count)> Free = [];
    // The corners at the buffer's head a copy recorded by a frame still in flight writes, under
    // which the CPU writes nothing until that frame is done, and the count of copies recorded, by
    // which the frame's end clears the corners of its own copy alone.
    internal int Copying;
    internal int CopiesRecorded;
    // The meshes whose corners took new colors while a copy wrote under them, colored once it is
    // done.
    internal readonly HashSet<object> RecolorLater = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// How many corners the buffer makes room for ahead of the meshes that need them, which a
    /// program reserves as its world loads, so the buffer is not made again while it is played.
    /// </summary>
    internal int Reserved { get; set; }

    /// <summary>How many corners the buffer has room for.</summary>
    internal int Room => (int)((Corners?.Size ?? 0) / 16);
    internal VkAccelerationStructureKHR Top;
    internal AddressedBuffer? TopStorage, TopScratch;
    internal int Capacity;
    // The ring of the copies' records and their surfaces, a slot for each frame that may be in
    // flight, each slot RingCapacity copies, one buffer each for every slot.
    internal const int Slots = GpuTextures.RetireFrames + 1;
    internal AddressedBuffer? Records, Surfaces;
    internal int RingCapacity;
    internal int Slot;
    // The scratch the meshes' structures are built in, kept from frame to frame and made again larger
    // where a frame's builds need more, and how much of it the frame's builds have taken.
    internal AddressedBuffer? MeshScratch;
    internal ulong ScratchTaken;
    // The copies' records the top level was last built from, those the frame writes, and how many
    // builds it has had.
    internal byte[]? Built;
    internal byte[] Writing = [];
    internal int TopBuilds;
    // The frames the scene has recorded.
    internal long Frame;
    // The frame's copies and the frame before's, by turns, which Copies names the one of.
    internal readonly List<RayInstance>[] Placings = [[], []];

    /// <summary>How many frames a mesh no copy places keeps its structure, after which it gives it back.</summary>
    internal int UnplacedFrames { get; set; } = 60;

    /// <summary>How many buffers the scene has made, its meshes' own structures apart, which a scene that grows as it meets land makes in steps.</summary>
    internal int Makings { get; set; }

    /// <summary>The bytes of a slot of <see cref="Surfaces"/>, where the frame's copies' surfaces begin at <see cref="Slot"/>.</summary>
    internal ulong SurfaceSlotBytes => (ulong)RingCapacity * 48;

    // The origin the top level's copies are placed against, the frame's it was last built in.
    internal RenderOrigin Origin;
    internal Action? Destroy;

    /// <summary>How many copies the frame's top-level structure holds.</summary>
    public int Count { get; internal set; }

    /// <summary>The frame's copies in the order the top-level structure holds them, a copy's index there its place here.</summary>
    public IReadOnlyList<RayInstance> Copies { get; internal set; } = [];

    /// <summary>The bytes the scene holds on the device, the meshes' structures, their corners and scratch, the top-level structure and its scratch, and the ring of copies.</summary>
    public long Bytes =>
        Meshes.Values.Sum(mesh => (long)mesh.Storage.Size) + (long)(Corners?.Size ?? 0) + (long)(Uvs?.Size ?? 0) + (long)(TopStorage?.Size ?? 0) + (long)(TopScratch?.Size ?? 0)
        + (long)(Records?.Size ?? 0) + (long)(Surfaces?.Size ?? 0) + (long)(MeshScratch?.Size ?? 0);

    /// <inheritdoc />
    public void Dispose() => Destroy?.Invoke();
}

/// <summary>
/// The device's work on a <see cref="GpuRayScene"/>, building its acceleration structures and
/// binding them, written as members of the device the renderer calls on it, which reach the device
/// through the surface <c>DeviceSurface.txt</c> lists.
/// </summary>
internal static unsafe class GpuRayScenes
{
    // An instance record as the top-level build reads it, the copy's 3 by 4 transform, its index
    // and mask, its flags, and its mesh's structure.
    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceRecord
    {
        public Vector4 Row0, Row1, Row2;
        public uint IndexAndMask;
        public uint OffsetAndFlags;
        public ulong Structure;
    }

    // A copy as rayScene's RaySurface holds it, its roughness, metallic and share of light through
    // as gi_reference.slang reads them.
    [StructLayout(LayoutKind.Sequential)]
    private struct SurfaceRecord
    {
        public Vector4 Color, Emission;
        public uint FirstCorner;
        public float Roughness, Metallic, LightThrough;
    }

    private const uint CullDisableAndOpaque = 0x1 | 0x4;
    // A blended copy's flags, its faces left to the query that meets them, opaque to none.
    private const uint CullDisableAndNotOpaque = 0x1 | 0x8;

    /// <summary>
    /// The mask of every copy that stands solid, which every query of the scene meets.
    /// </summary>
    internal const uint SolidMask = 0x01;

    /// <summary>
    /// The mask of every blended copy, which a path traced reference alone meets, with the chance
    /// its coverage gives, and every other query of the scene leaves out, as the field leaves it
    /// out.
    /// </summary>
    internal const uint BlendedMask = 0x02;

    extension(GraphicsDevice device)
    {
        /// <summary>
        /// Makes the window's meshes for the device's ray tracing, holding none, its top-level
        /// structure built empty so it can be bound before the first frame fills it, or left for
        /// the frame that records it first to build where <paramref name="built"/> is false, so a
        /// scene made in a frame does not wait for the device.
        /// </summary>
        public GpuRayScene CreateRayScene(bool built = true)
        {
            var scene = new GpuRayScene();
            scene.Destroy = () =>
            {
                foreach (var mesh in scene.Meshes.Values)
                {
                    device.Api.vkDestroyAccelerationStructureKHR(mesh.Structure);
                    DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
                    mesh.Storage.Dispose();
                }
                scene.Meshes.Clear();
                scene.Placed.Clear();
                device.DestroyTop(scene);
                scene.Records?.Dispose();
                scene.Surfaces?.Dispose();
                scene.MeshScratch?.Dispose();
                scene.Corners?.Dispose();
                scene.Uvs?.Dispose();
            };
            if (!built) return scene;
            var cmd = device.BeginSingleTimeCommands();
            var held = device.BuildTop(cmd, scene, [], default);
            device.EndSingleTimeCommands(cmd);
            held.Dispose();
            return scene;
        }

        private void DestroyTop(GpuRayScene scene)
        {
            if (scene.Top.Handle != 0)
            {
                device.Api.vkDestroyAccelerationStructureKHR(scene.Top);
                DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
            }
            scene.Top = default;
            scene.TopStorage?.Dispose();
            scene.TopScratch?.Dispose();
            (scene.TopStorage, scene.TopScratch) = (null, null);
        }

        /// <summary>
        /// Records the frame's meshes for the device's ray tracing, the structure of each mesh in
        /// <paramref name="instances"/> not yet built, from the corners <paramref name="cornersOf"/>
        /// gives, three a triangle in its own space, with each corner's color <paramref name="colorsOf"/>
        /// gives, three sRGB bytes, or white where it gives none, and each corner's texture coordinates
        /// <paramref name="uvsOf"/> gives, or none, then the top-level structure of every copy, ready for
        /// the passes after to trace through. The corners of a built mesh among
        /// <paramref name="recolored"/>, whose colors were written in place, take their colors again,
        /// which its structure does not read.
        /// </summary>
        /// <returns>What the builds read, to be let go once no frame in flight reads it.</returns>
        public IDisposable RecordRayScene(ICommandBuffer commandBuffer, GpuRayScene scene, IReadOnlyList<RayInstance> instances,
            Func<object, Vector3[]?> cornersOf, Func<object, uint[]?>? colorsOf = null, IEnumerable<object>? recolored = null,
            Func<object, Vector2[]?>? uvsOf = null, RenderOrigin origin = default)
        {
            if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
            var cmd = vkCmd.Handle;
            var retired = new Retired();
            scene.Frame++;
            scene.ScratchTaken = 0;
            device.LetGo(cmd, scene, retired);
            if (scene.Reserved > scene.Room) device.Grow(cmd, scene, scene.Reserved, retired);

            // A mesh's colors written in place, or kept for a frame after the copy writing under
            // them is done, which would write over them.
            if (recolored is not null) scene.RecolorLater.UnionWith(recolored);
            foreach (var mesh in scene.RecolorLater.ToArray())
            {
                if (!scene.Meshes.TryGetValue(mesh, out var made) || colorsOf?.Invoke(mesh) is not { } colors || scene.Corners is null)
                    scene.RecolorLater.Remove(mesh);
                else if (made.FirstCorner >= scene.Copying)
                {
                    var into = (Vector4*)scene.Corners.Mapped + made.FirstCorner;
                    for (int i = 0; i < colors.Length && made.FirstCorner + i < scene.CornerCount; i++)
                        into[i].W = BitConverter.UInt32BitsToSingle(1u << 30 | colors[i]);
                    scene.RecolorLater.Remove(mesh);
                }
            }

            var built = false;
            foreach (var instance in instances)
            {
                if (scene.Meshes.ContainsKey(instance.Mesh) || cornersOf(instance.Mesh) is not { Length: >= 3 } corners) continue;
                // The scratch the frame's builds share, written by the builds of the frames before,
                // which their writes finish before.
                if (!built)
                    device.MemoryBarrier(cmd, VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureWriteKHR,
                        VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureReadKHR | VkAccessFlags2.AccelerationStructureWriteKHR);
                var first = device.AddCorners(cmd, scene, corners, colorsOf?.Invoke(instance.Mesh), uvsOf?.Invoke(instance.Mesh), retired);
                scene.Meshes[instance.Mesh] = device.BuildMesh(cmd, scene, first, corners.Length / 3, retired);
                built = true;
            }
            // The meshes' builds before the top-level one reads them.
            if (built)
                device.MemoryBarrier(cmd, VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureWriteKHR,
                    VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureReadKHR);
            retired.Add(device.BuildTop(cmd, scene, instances, origin));
            return retired;
        }

        // Gives back the structures of the meshes no copy has placed for the scene's count of frames,
        // let go once no frame in flight reads them, with their corners for the meshes after to
        // take, and makes the corners again without theirs once they are half the buffer, each mesh
        // kept moved to where its corners then lie.
        private void LetGo(VkCommandBuffer cmd, GpuRayScene scene, Retired retired)
        {
            List<object>? gone = null;
            foreach (var (key, mesh) in scene.Meshes)
                if (scene.Frame - scene.Placed.GetValueOrDefault(key, scene.Frame) > scene.UnplacedFrames) (gone ??= []).Add(key);
            foreach (var key in gone ?? [])
            {
                var mesh = scene.Meshes[key];
                scene.Meshes.Remove(key);
                scene.Placed.Remove(key);
                GiveBack(scene, mesh.FirstCorner, mesh.Corners);
                retired.Add(new Released(() =>
                {
                    device.Api.vkDestroyAccelerationStructureKHR(mesh.Structure);
                    DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
                    mesh.Storage.Dispose();
                }));
            }
            if (scene.Corners is null || scene.DeadCorners < 65536 || scene.DeadCorners * 2 < scene.CornerCount) return;

            var live = scene.CornerCount - scene.DeadCorners;
            var room = Math.Max((ulong)scene.Reserved, (ulong)Math.Max(4096, (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, live)) * 2));
            var kept = scene.Meshes.ToArray();
            var moves = new (int From, int To, int Count)[kept.Length];
            var at = 0;
            for (int i = 0; i < kept.Length; i++)
            {
                var (key, mesh) = (kept[i].Key, kept[i].Value);
                moves[i] = (mesh.FirstCorner, at, mesh.Corners);
                scene.Meshes[key] = mesh with { FirstCorner = at };
                at += mesh.Corners;
            }
            device.Remake(cmd, scene, Math.Min(room, (ulong)scene.Room), moves, at, retired);
            (scene.CornerCount, scene.DeadCorners) = (at, 0);
            scene.Free.Clear();
        }

        // Puts a mesh's corners among the runs the meshes after take from, joined to the runs
        // either side, and lowers the buffer's count where they end it and no copy writes under
        // them.
        private static void GiveBack(GpuRayScene scene, int first, int count)
        {
            if (count <= 0) return;
            var free = scene.Free;
            var at = free.FindIndex(run => run.First > first);
            if (at < 0) at = free.Count;
            free.Insert(at, (first, count));
            scene.DeadCorners += count;
            if (at + 1 < free.Count && free[at].First + free[at].Count == free[at + 1].First)
            {
                free[at] = (free[at].First, free[at].Count + free[at + 1].Count);
                free.RemoveAt(at + 1);
            }
            if (at > 0 && free[at - 1].First + free[at - 1].Count == free[at].First)
            {
                free[at - 1] = (free[at - 1].First, free[at - 1].Count + free[at].Count);
                free.RemoveAt(at--);
            }
            if (scene.Copying == 0 && free[at].First + free[at].Count == scene.CornerCount)
            {
                (scene.CornerCount, scene.DeadCorners) = (free[at].First, scene.DeadCorners - free[at].Count);
                free.RemoveAt(at);
            }
        }

        // The first corner of a run given back that holds the count given, taken from it, or -1
        // where none does or a copy in flight writes under them all.
        private static int Take(GpuRayScene scene, int count)
        {
            if (scene.Copying > 0) return -1;
            var free = scene.Free;
            for (int i = 0; i < free.Count; i++)
            {
                var (first, held) = free[i];
                if (held < count) continue;
                if (held == count) free.RemoveAt(i);
                else free[i] = (first + count, held - count);
                scene.DeadCorners -= count;
                return first;
            }
            return -1;
        }

        // Makes the corners' buffers again with room for at least the corners given, twice the
        // room they had as often as that falls short, with the corners they held at the same
        // places.
        private void Grow(VkCommandBuffer cmd, GpuRayScene scene, int corners, Retired retired)
        {
            var room = (ulong)Math.Max(4096, scene.Room);
            while (room < (ulong)corners) room *= 2;
            (int From, int To, int Count)[] moves = scene.CornerCount > 0 ? [(0, 0, scene.CornerCount)] : [];
            device.Remake(cmd, scene, room, moves, scene.CornerCount, retired);
        }

        // Makes the corners' buffers again with room for the corners given, the runs given copied
        // into them on the GPU by the frame's commands, which the frame's passes read after and the
        // CPU writes nothing under until the frame is done, since a write it makes reaches the
        // memory at once and the copy after would write over it. The buffers left are let go once
        // no frame in flight reads them.
        private void Remake(VkCommandBuffer cmd, GpuRayScene scene, ulong room, (int From, int To, int Count)[] moves, int held, Retired retired)
        {
            var corners = device.CreateAddressedBuffer(room * 16, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyKHR
                | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, host: true, lasting: true);
            var uvs = device.CreateAddressedBuffer(room * 8, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst,
                host: true, lasting: true);
            scene.Makings += 2;
            if (scene.Corners is not null && moves.Length > 0)
            {
                var (cornerMoves, uvMoves) = (new VkBufferCopy[moves.Length], new VkBufferCopy[moves.Length]);
                for (int i = 0; i < moves.Length; i++)
                {
                    var (from, to, count) = moves[i];
                    cornerMoves[i] = new VkBufferCopy { srcOffset = (ulong)from * 16, dstOffset = (ulong)to * 16, size = (ulong)count * 16 };
                    uvMoves[i] = new VkBufferCopy { srcOffset = (ulong)from * 8, dstOffset = (ulong)to * 8, size = (ulong)count * 8 };
                }
                fixed (VkBufferCopy* m = cornerMoves) device.Api.vkCmdCopyBuffer(cmd, scene.Corners.Buffer, corners.Buffer, (uint)moves.Length, m);
                if (scene.Uvs is not null)
                    fixed (VkBufferCopy* m = uvMoves) device.Api.vkCmdCopyBuffer(cmd, scene.Uvs.Buffer, uvs.Buffer, (uint)moves.Length, m);
                device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.MemoryRead);
                scene.Copying = held;
                var copy = ++scene.CopiesRecorded;
                retired.Add(new Released(() =>
                {
                    if (scene.CopiesRecorded == copy) scene.Copying = 0;
                }));
            }
            if (scene.Corners is not null) retired.Add(scene.Corners);
            if (scene.Uvs is not null) retired.Add(scene.Uvs);
            (scene.Corners, scene.Uvs) = (corners, uvs);
        }

        // Writes a mesh's corners into a run a mesh that left gave back where one holds them, or
        // after those the buffer holds, made again larger where they do not fit, and gives where
        // the first lies. Each corner's color rides in the w its structure's build leaves unread,
        // three sRGB bytes under a bit that keeps the uint a whole float rather than one a GPU may
        // read as nothing, so the reference and a ray's reflection take a mesh's vertices' colors.
        // Each corner's texture coordinates go at the same place in the buffer beside, (0, 0) where
        // it has none.
        private int AddCorners(VkCommandBuffer cmd, GpuRayScene scene, Vector3[] corners, uint[]? colors, Vector2[]? uvs, Retired retired)
        {
            var first = Take(scene, corners.Length);
            if (first < 0)
            {
                // After the corners the buffer holds, which a copy in flight writes no further
                // than, since a copy is of what it held and its count falls under it only once no
                // copy is.
                first = scene.CornerCount;
                if (scene.Corners is null || (ulong)(first + corners.Length) * 16 > scene.Corners.Size)
                    device.Grow(cmd, scene, first + corners.Length, retired);
                scene.CornerCount = first + corners.Length;
            }
            var into = new Span<Vector4>((Vector4*)scene.Corners!.Mapped + first, corners.Length);
            for (int i = 0; i < corners.Length; i++)
                into[i] = new Vector4(corners[i], BitConverter.UInt32BitsToSingle(1u << 30 | (colors is { } c && i < c.Length ? c[i] : 0xFFFFFFu)));
            var uvsInto = new Span<Vector2>((Vector2*)scene.Uvs!.Mapped + first, corners.Length);
            for (int i = 0; i < corners.Length; i++)
                uvsInto[i] = uvs is { } u && i < u.Length ? u[i] : Vector2.Zero;
            return first;
        }

        // Records the build of one mesh's structure from its corners in the scene's buffer.
        private GpuRayScene.Mesh BuildMesh(VkCommandBuffer cmd, GpuRayScene scene, int firstCorner, int triangles, Retired retired)
        {
            var geometry = new VkAccelerationStructureGeometryKHR
            {
                geometryType = VkGeometryTypeKHR.Triangles,
                flags = VkGeometryFlagsKHR.Opaque,
            };
            geometry.geometry.triangles = new VkAccelerationStructureGeometryTrianglesDataKHR
            {
                vertexFormat = VkFormat.R32G32B32Sfloat,
                vertexData = new VkDeviceOrHostAddressConstKHR { deviceAddress = scene.Corners!.Address + (ulong)firstCorner * 16 },
                vertexStride = 16,
                maxVertex = (uint)(triangles * 3 - 1),
                indexType = VkIndexType.NoneKHR,
            };
            var (structure, storage, address) = device.Build(cmd, VkAccelerationStructureTypeKHR.BottomLevel, &geometry, (uint)triangles, null, retired, scene: scene);
            return new GpuRayScene.Mesh(structure, storage, address, firstCorner, triangles * 3);
        }

        // Records the top-level structure of the frame's copies, made again larger where they do not
        // fit, with their colors in this frame's buffer of the ring, each copy's translation taken
        // against the frame's origin, as the places the rays leave from are.
        private Retired BuildTop(VkCommandBuffer cmd, GpuRayScene scene, IReadOnlyList<RayInstance> instances, RenderOrigin origin)
        {
            var retired = new Retired();
            scene.Slot = (scene.Slot + 1) % GpuRayScene.Slots;
            var count = 0;
            foreach (var instance in instances)
                if (scene.Meshes.ContainsKey(instance.Mesh)) count++;

            // The ring made again for twice the copies' power of two where they outgrow it, every
            // slot at once, the buffers before let go once no frame in flight reads them.
            if (scene.Records is null || scene.RingCapacity < count)
            {
                if (scene.Records is not null) retired.Add(scene.Records);
                if (scene.Surfaces is not null) retired.Add(scene.Surfaces);
                scene.RingCapacity = Math.Max(64, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, count)) * 2);
                scene.Records = device.CreateAddressedBuffer((ulong)scene.RingCapacity * 64 * GpuRayScene.Slots, VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyKHR, host: true);
                scene.Surfaces = device.CreateAddressedBuffer(scene.SurfaceSlotBytes * GpuRayScene.Slots, VkBufferUsageFlags.StorageBuffer, host: true);
                scene.Makings += 2;
            }
            var capacity = scene.RingCapacity;
            var recordsAt = (ulong)scene.Slot * (ulong)capacity * 64;

            // The records are written here first and copied into the ring, whose memory the CPU
            // writes through uncached and never reads, and compared with the last build's here.
            if (scene.Writing.Length < count * 64) scene.Writing = new byte[Math.Max(count * 64, scene.Writing.Length * 2)];
            var written = MemoryMarshal.Cast<byte, InstanceRecord>(scene.Writing.AsSpan(0, count * 64));
            var shaded = new Span<SurfaceRecord>((byte*)scene.Surfaces!.Mapped + (ulong)scene.Slot * scene.SurfaceSlotBytes, count);
            var copies = scene.Placings[scene.Frame & 1];
            copies.Clear();
            var i = 0;
            foreach (var instance in instances)
            {
                if (!scene.Meshes.TryGetValue(instance.Mesh, out var mesh)) continue;
                scene.Placed[instance.Mesh] = scene.Frame;
                // The world's matrix, a point a row as System.Numerics has it, as three rows of a
                // matrix a point is a column of.
                var m = instance.World;
                var moved = origin.Against(m.Translation);
                written[i] = new InstanceRecord
                {
                    Row0 = new Vector4(m.M11, m.M21, m.M31, moved.X),
                    Row1 = new Vector4(m.M12, m.M22, m.M32, moved.Y),
                    Row2 = new Vector4(m.M13, m.M23, m.M33, moved.Z),
                    IndexAndMask = (uint)i | ((instance.Blend > 0 ? BlendedMask : SolidMask) << 24),
                    OffsetAndFlags = (instance.Blend > 0 ? CullDisableAndNotOpaque : CullDisableAndOpaque) << 24,
                    Structure = mesh.Address,
                };
                shaded[i] = new SurfaceRecord
                {
                    Color = new Vector4(instance.Color, 1),
                    Emission = new Vector4(instance.Emission, instance.Carried ? 1 : 0),
                    FirstCorner = (uint)mesh.FirstCorner,
                    Roughness = instance.Roughness,
                    Metallic = instance.Metallic,
                    LightThrough = instance.LightThrough,
                };
                copies.Add(instance);
                i++;
            }
            scene.Count = count;
            scene.Copies = copies;
            scene.Origin = origin;

            // A structure too small for the copies is made again, the old one let go once no frame
            // reads it.
            if (scene.Top.Handle != 0 && scene.Capacity < count)
            {
                var (top, storage, scratch) = (scene.Top, scene.TopStorage, scene.TopScratch);
                retired.Add(new Released(() =>
                {
                    device.Api.vkDestroyAccelerationStructureKHR(top);
                    DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
                    storage?.Dispose();
                    scratch?.Dispose();
                }));
                (scene.Top, scene.TopStorage, scene.TopScratch) = (default, null, null);
            }

            // A top level whose copies' records are those its last build read is kept, the surfaces
            // written above holding the frame's colors, since its build waits on every fragment and
            // compute pass before it, a cost each frame with nothing moving. A copy moved, come or
            // gone, or a mesh's structure built anew, changes the records.
            var bytes = scene.Writing.AsSpan(0, count * 64);
            if (scene.Top.Handle != 0 && scene.Capacity >= count && scene.Built is { } last && bytes.SequenceEqual(last))
                return retired;
            bytes.CopyTo(new Span<byte>((byte*)scene.Records!.Mapped + recordsAt, count * 64));
            if (scene.Built is null || scene.Built.Length != count * 64) scene.Built = new byte[count * 64];
            bytes.CopyTo(scene.Built);
            scene.TopBuilds++;

            var geometry = new VkAccelerationStructureGeometryKHR
            {
                geometryType = VkGeometryTypeKHR.Instances,
                flags = VkGeometryFlagsKHR.Opaque,
            };
            geometry.geometry.instances = new VkAccelerationStructureGeometryInstancesDataKHR
            {
                arrayOfPointers = false,
                data = new VkDeviceOrHostAddressConstKHR { deviceAddress = scene.Records.Address + recordsAt },
            };
            // The frame before's reads of the structure, and its build's writes to the scratch, before
            // this build writes them.
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.AccelerationStructureBuildKHR,
                VkAccessFlags2.AccelerationStructureReadKHR | VkAccessFlags2.AccelerationStructureWriteKHR,
                VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureReadKHR | VkAccessFlags2.AccelerationStructureWriteKHR);
            if (scene.Top.Handle == 0)
            {
                scene.Capacity = capacity;
                var (top, storage, address) = device.Build(cmd, VkAccelerationStructureTypeKHR.TopLevel, &geometry, (uint)count, scene, retired, (uint)capacity);
                (scene.Top, scene.TopStorage) = (top, storage);
                _ = address;
            }
            else
                device.Rebuild(cmd, scene, &geometry, (uint)count);
            // The build before the passes that trace through it.
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureWriteKHR,
                VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.AccelerationStructureReadKHR);
            return retired;
        }

        // Makes a structure of a type for a geometry sized for its largest count, and records its first
        // build, its scratch for the top level kept on the scene, for a mesh of a scene a part of the
        // scene's scratch, and otherwise let go after the frame.
        private (VkAccelerationStructureKHR Structure, AddressedBuffer Storage, ulong Address) Build(VkCommandBuffer cmd,
            VkAccelerationStructureTypeKHR type, VkAccelerationStructureGeometryKHR* geometry, uint count, GpuRayScene? top, Retired retired,
            uint largest = 0, GpuRayScene? scene = null)
        {
            var info = new VkAccelerationStructureBuildGeometryInfoKHR
            {
                type = type,
                flags = VkBuildAccelerationStructureFlagsKHR.PreferFastTrace,
                mode = VkBuildAccelerationStructureModeKHR.Build,
                geometryCount = 1,
                pGeometries = geometry,
            };
            var most = Math.Max(count, largest);
            VkAccelerationStructureBuildSizesInfoKHR sizes = new();
            device.Api.vkGetAccelerationStructureBuildSizesKHR(VkAccelerationStructureBuildTypeKHR.Device, &info, &most, &sizes);

            var storage = device.CreateAddressedBuffer(sizes.accelerationStructureSize, VkBufferUsageFlags.AccelerationStructureStorageKHR, host: false);
            var create = new VkAccelerationStructureCreateInfoKHR { buffer = storage.Buffer, size = sizes.accelerationStructureSize, type = type };
            VkAccelerationStructureKHR structure;
            device.Api.vkCreateAccelerationStructureKHR(&create, null, &structure).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.AccelerationStructure);
            ulong scratchAddress;
            if (scene is not null)
                scratchAddress = device.MeshScratch(scene, sizes.buildScratchSize, retired);
            else
            {
                var scratch = device.CreateAddressedBuffer(sizes.buildScratchSize + ScratchAlignment, VkBufferUsageFlags.StorageBuffer, host: false);
                if (top is not null)
                {
                    top.TopScratch = scratch;
                    top.Makings += 2;
                }
                else retired.Add(scratch);
                scratchAddress = Aligned(scratch.Address);
            }

            info.dstAccelerationStructure = structure;
            info.scratchData = new VkDeviceOrHostAddressKHR { deviceAddress = scratchAddress };
            var range = new VkAccelerationStructureBuildRangeInfoKHR { primitiveCount = count };
            var ranges = &range;
            device.Api.vkCmdBuildAccelerationStructuresKHR(cmd, 1, &info, &ranges);
            var addressInfo = new VkAccelerationStructureDeviceAddressInfoKHR { accelerationStructure = structure };
            return (structure, storage, device.Api.vkGetAccelerationStructureDeviceAddressKHR(&addressInfo));
        }

        // An address in the scene's scratch for a mesh's build of the size given, apart from the frame's
        // builds before so they run side by side, the scratch made again twice as large where they
        // took what it holds, the one before let go once no frame in flight builds in it.
        private ulong MeshScratch(GpuRayScene scene, ulong size, Retired retired)
        {
            var need = (size + ScratchAlignment - 1) / ScratchAlignment * ScratchAlignment;
            if (scene.MeshScratch is null || scene.ScratchTaken + need + ScratchAlignment > scene.MeshScratch.Size)
            {
                if (scene.MeshScratch is not null) retired.Add(scene.MeshScratch);
                var made = Math.Max(need + ScratchAlignment, Math.Max(1ul << 20, (scene.MeshScratch?.Size ?? 0) * 2));
                scene.MeshScratch = device.CreateAddressedBuffer(made, VkBufferUsageFlags.StorageBuffer, host: false);
                scene.ScratchTaken = 0;
                scene.Makings++;
            }
            var address = Aligned(scene.MeshScratch.Address + scene.ScratchTaken);
            scene.ScratchTaken = address - scene.MeshScratch.Address + need;
            return address;
        }

        // Records the top-level structure built again in place for this frame's copies.
        private void Rebuild(VkCommandBuffer cmd, GpuRayScene scene, VkAccelerationStructureGeometryKHR* geometry, uint count)
        {
            var info = new VkAccelerationStructureBuildGeometryInfoKHR
            {
                type = VkAccelerationStructureTypeKHR.TopLevel,
                flags = VkBuildAccelerationStructureFlagsKHR.PreferFastTrace,
                mode = VkBuildAccelerationStructureModeKHR.Build,
                geometryCount = 1,
                pGeometries = geometry,
                dstAccelerationStructure = scene.Top,
                scratchData = new VkDeviceOrHostAddressKHR { deviceAddress = Aligned(scene.TopScratch!.Address) },
            };
            var range = new VkAccelerationStructureBuildRangeInfoKHR { primitiveCount = count };
            var ranges = &range;
            device.Api.vkCmdBuildAccelerationStructuresKHR(cmd, 1, &info, &ranges);
        }

        /// <summary>
        /// Binds the scene's top-level structure, this frame's copies and the meshes' corners at the
        /// three bindings from <paramref name="first"/> of <paramref name="set"/>.
        /// </summary>
        public void BindRayScene(IDescriptorSet set, uint first, GpuRayScene scene) => device.WriteRayScene(SetHandle(set), first, scene);

        internal void WriteRayScene(VkDescriptorSet handle, uint first, GpuRayScene scene)
        {
            var top = scene.Top;
            var structure = new VkWriteDescriptorSetAccelerationStructureKHR { accelerationStructureCount = 1, pAccelerationStructures = &top };
            var surfaces = scene.Surfaces!;
            var buffers = stackalloc VkDescriptorBufferInfo[2];
            buffers[0] = new VkDescriptorBufferInfo { buffer = surfaces.Buffer, offset = (ulong)scene.Slot * scene.SurfaceSlotBytes, range = scene.SurfaceSlotBytes };
            // A scene with no mesh yet has no corners, and binds the copies' buffer in their place,
            // which nothing reads while it holds no copy.
            var corners = scene.Corners ?? surfaces;
            buffers[1] = new VkDescriptorBufferInfo { buffer = corners.Buffer, offset = 0, range = corners.Size };
            var writes = stackalloc VkWriteDescriptorSet[3];
            writes[0] = new VkWriteDescriptorSet
            {
                pNext = &structure, dstSet = handle, dstBinding = first, descriptorCount = 1,
                descriptorType = VkDescriptorType.AccelerationStructureKHR,
            };
            writes[1] = new VkWriteDescriptorSet
            {
                dstSet = handle, dstBinding = first + 1, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &buffers[0],
            };
            writes[2] = new VkWriteDescriptorSet
            {
                dstSet = handle, dstBinding = first + 2, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &buffers[1],
            };
            device.Api.vkUpdateDescriptorSets(3, writes, 0, null);
        }
    }

    // The most any device asks a scratch address to be aligned to, which every buffer of scratch
    // is made that much larger for.
    private const ulong ScratchAlignment = 256;

    private static ulong Aligned(ulong address) => (address + ScratchAlignment - 1) / ScratchAlignment * ScratchAlignment;

    // What a frame's builds read, let go together.
    private sealed class Retired : IDisposable
    {
        private readonly List<IDisposable> _held = [];

        public void Add(IDisposable disposable) => _held.Add(disposable);

        public void Dispose()
        {
            foreach (var disposable in _held) disposable.Dispose();
            _held.Clear();
        }
    }

    private sealed class Released(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
