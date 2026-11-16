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
internal readonly record struct RayInstance(object Mesh, Matrix4x4 World, Vector3 Color, Vector3 Emission, float Roughness = 1, float Metallic = 0, bool Carried = false,
    float LightThrough = 0, int Texture = 0, int NormalMap = 0, float NormalScale = 0, Vector3 Tint = default);

/// <summary>
/// The window's meshes as the device's ray tracing sees them, a bottom-level acceleration structure
/// of each mesh's triangles, built the first frame the mesh is drawn, and a top-level one of every
/// drawn copy, built again each frame, with each copy's color and emission and each mesh's
/// corners, which a reflection the scene's distance field misses is traced and shaded with.
/// </summary>
/// <remarks>
/// The copies and their colors are written by the CPU into one of a ring of buffers a frame, as
/// many as frames may be in flight, so a frame never writes what one still drawing reads. The
/// corners of every mesh lie in one buffer, made again twice as large with the meshes it held
/// copied where a new mesh does not fit, and their texture coordinates in another beside it, which
/// a path traced reference reads the copies' textures by.
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
    // The corners the buffer holds of meshes whose structures were given back, until it is made
    // again without them.
    internal int DeadCorners;
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

    extension(GraphicsDevice device)
    {
        /// <summary>
        /// Makes the window's meshes for the device's ray tracing, holding none, its top-level
        /// structure built empty so it can be bound before the first frame fills it.
        /// </summary>
        public GpuRayScene CreateRayScene()
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
            device.LetGo(scene, retired);

            foreach (var mesh in recolored ?? [])
                if (scene.Meshes.TryGetValue(mesh, out var made) && colorsOf?.Invoke(mesh) is { } colors && scene.Corners is not null)
                {
                    var into = (Vector4*)scene.Corners.Mapped + made.FirstCorner;
                    for (int i = 0; i < colors.Length && made.FirstCorner + i < scene.CornerCount; i++)
                        into[i].W = BitConverter.UInt32BitsToSingle(1u << 30 | colors[i]);
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
                var first = device.AddCorners(scene, corners, colorsOf?.Invoke(instance.Mesh), uvsOf?.Invoke(instance.Mesh), retired);
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
        // let go once no frame in flight reads them, and makes the corners again without theirs once
        // they are half the buffer, each mesh kept moved to where its corners then lie.
        private void LetGo(GpuRayScene scene, Retired retired)
        {
            List<object>? gone = null;
            foreach (var (key, mesh) in scene.Meshes)
                if (scene.Frame - scene.Placed.GetValueOrDefault(key, scene.Frame) > scene.UnplacedFrames) (gone ??= []).Add(key);
            foreach (var key in gone ?? [])
            {
                var mesh = scene.Meshes[key];
                scene.Meshes.Remove(key);
                scene.Placed.Remove(key);
                scene.DeadCorners += mesh.Corners;
                retired.Add(new Released(() =>
                {
                    device.Api.vkDestroyAccelerationStructureKHR(mesh.Structure);
                    DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
                    mesh.Storage.Dispose();
                }));
            }
            if (scene.Corners is null || scene.DeadCorners < 65536 || scene.DeadCorners * 2 < scene.CornerCount) return;

            var live = scene.CornerCount - scene.DeadCorners;
            var room = (ulong)Math.Max(4096, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, live)) * 2);
            var corners = device.CreateAddressedBuffer(room * 16, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyKHR
                | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, host: true, lasting: true);
            var uvs = device.CreateAddressedBuffer(room * 8, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst,
                host: true, lasting: true);
            scene.Makings += 2;
            var kept = scene.Meshes.ToArray();
            var (moves, uvMoves) = (new VkBufferCopy[Math.Max(1, kept.Length)], new VkBufferCopy[Math.Max(1, kept.Length)]);
            var at = 0;
            for (int i = 0; i < kept.Length; i++)
            {
                var (key, mesh) = (kept[i].Key, kept[i].Value);
                moves[i] = new VkBufferCopy { srcOffset = (ulong)mesh.FirstCorner * 16, dstOffset = (ulong)at * 16, size = (ulong)mesh.Corners * 16 };
                uvMoves[i] = new VkBufferCopy { srcOffset = (ulong)mesh.FirstCorner * 8, dstOffset = (ulong)at * 8, size = (ulong)mesh.Corners * 8 };
                scene.Meshes[key] = mesh with { FirstCorner = at };
                at += mesh.Corners;
            }
            // Copied on the GPU and waited for, as a buffer made again larger is, before the frame's
            // corners are written after them.
            if (kept.Length > 0)
            {
                var cmd = device.BeginSingleTimeCommands();
                fixed (VkBufferCopy* m = moves) device.Api.vkCmdCopyBuffer(cmd, scene.Corners.Buffer, corners.Buffer, (uint)kept.Length, m);
                if (scene.Uvs is not null)
                    fixed (VkBufferCopy* m = uvMoves) device.Api.vkCmdCopyBuffer(cmd, scene.Uvs.Buffer, uvs.Buffer, (uint)kept.Length, m);
                device.EndSingleTimeCommands(cmd);
            }
            retired.Add(scene.Corners);
            if (scene.Uvs is not null) retired.Add(scene.Uvs);
            (scene.Corners, scene.Uvs, scene.CornerCount, scene.DeadCorners) = (corners, uvs, at, 0);
        }

        // Writes a mesh's corners after those the buffer holds, made again larger where they do not
        // fit, and gives where the first lies. Each corner's color rides in the w its structure's build
        // leaves unread, three sRGB bytes under a bit that keeps the uint a whole float rather than one
        // a GPU may read as nothing, so the reference and a ray's reflection take a mesh's vertices'
        // colors. Each corner's texture coordinates go at the same place in the buffer beside,
        // (0, 0) where it has none.
        private int AddCorners(GpuRayScene scene, Vector3[] corners, uint[]? colors, Vector2[]? uvs, Retired retired)
        {
            var needed = scene.CornerCount + corners.Length;
            if (scene.Corners is null || (ulong)needed * 16 > scene.Corners.Size)
            {
                var room = (ulong)Math.Max(4096, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)needed) * 2);
                var bigger = device.CreateAddressedBuffer(room * 16,
                    VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyKHR
                    | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, host: true, lasting: true);
                var biggerUvs = device.CreateAddressedBuffer(room * 8, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst,
                    host: true, lasting: true);
                scene.Makings += 2;
                if (scene.Corners is not null)
                {
                    // Copied on the GPU and waited for, since the CPU reads memory it writes
                    // through uncached, and the corners it writes into the new buffer after, a
                    // mesh's colors among them, come after the copy.
                    var cmd = device.BeginSingleTimeCommands();
                    var region = new VkBufferCopy { size = (ulong)scene.CornerCount * 16 };
                    device.Api.vkCmdCopyBuffer(cmd, scene.Corners.Buffer, bigger.Buffer, 1, &region);
                    if (scene.Uvs is not null)
                    {
                        var uvRegion = new VkBufferCopy { size = (ulong)scene.CornerCount * 8 };
                        device.Api.vkCmdCopyBuffer(cmd, scene.Uvs.Buffer, biggerUvs.Buffer, 1, &uvRegion);
                    }
                    device.EndSingleTimeCommands(cmd);
                    retired.Add(scene.Corners);
                    if (scene.Uvs is not null) retired.Add(scene.Uvs);
                }
                (scene.Corners, scene.Uvs) = (bigger, biggerUvs);
            }
            var into = new Span<Vector4>((Vector4*)scene.Corners.Mapped + scene.CornerCount, corners.Length);
            for (int i = 0; i < corners.Length; i++)
                into[i] = new Vector4(corners[i], BitConverter.UInt32BitsToSingle(1u << 30 | (colors is { } c && i < c.Length ? c[i] : 0xFFFFFFu)));
            var uvsInto = new Span<Vector2>((Vector2*)scene.Uvs!.Mapped + scene.CornerCount, corners.Length);
            for (int i = 0; i < corners.Length; i++)
                uvsInto[i] = uvs is { } u && i < u.Length ? u[i] : Vector2.Zero;
            var first = scene.CornerCount;
            scene.CornerCount = needed;
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
                    IndexAndMask = (uint)i | (0xFFu << 24),
                    OffsetAndFlags = CullDisableAndOpaque << 24,
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
