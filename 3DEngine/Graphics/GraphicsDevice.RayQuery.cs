using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>A buffer in memory of its own with the address the GPU reads it by, as an acceleration structure, what it is built from and its scratch space need.</summary>
internal sealed unsafe class AddressedBuffer : IDisposable
{
    private readonly GraphicsDevice _device;

    internal AddressedBuffer(GraphicsDevice device, VkBuffer buffer, VkDeviceMemory memory, ulong address, ulong size, void* mapped)
    {
        _device = device;
        Buffer = buffer;
        Memory = memory;
        Address = address;
        Size = size;
        Mapped = mapped;
    }

    internal VkBuffer Buffer { get; }
    internal VkDeviceMemory Memory { get; }

    /// <summary>Where the GPU reads it.</summary>
    internal ulong Address { get; }

    /// <summary>Its size in bytes.</summary>
    internal ulong Size { get; }

    /// <summary>Its bytes as the CPU writes them, or null for one only the GPU reaches.</summary>
    internal void* Mapped { get; }

    /// <inheritdoc />
    public void Dispose() => _device.DestroyAddressedBuffer(this);
}

/// <summary>One drawn copy of a mesh for the device's ray tracing, where it is drawn and the light it sends back.</summary>
/// <param name="Mesh">What its mesh is known by, the vertices it was drawn with.</param>
/// <param name="World">Its mesh's space to the world.</param>
/// <param name="Color">Its color, linear.</param>
/// <param name="Emission">The light it gives off, linear.</param>
/// <param name="Roughness">Its material's roughness, which a path traced reference lights its face by.</param>
/// <param name="Metallic">Its material's metallic.</param>
internal readonly record struct RayInstance(object Mesh, Matrix4x4 World, Vector3 Color, Vector3 Emission, float Roughness = 1, float Metallic = 0);

/// <summary>
/// The window's meshes as the device's ray tracing sees them: a bottom-level acceleration structure
/// of each mesh's triangles, built the first frame the mesh is drawn, and a top-level one of every
/// drawn copy, built again each frame, with each copy's color and light given off and each mesh's
/// corners, which a reflection the scene's distance field misses is traced and shaded with.
/// </summary>
/// <remarks>
/// The copies and their colors are written by the CPU into one of a ring of buffers a frame, as
/// many as frames may be in flight, so a frame never writes what one still drawing reads. The
/// corners of every mesh lie in one buffer, made again twice as large with the meshes it held
/// copied where a new mesh does not fit.
/// </remarks>
internal sealed class GpuRayScene : IDisposable
{
    internal sealed record Mesh(VkAccelerationStructureKHR Structure, AddressedBuffer Storage, ulong Address, int FirstCorner);

    internal readonly Dictionary<object, Mesh> Meshes = new(ReferenceEqualityComparer.Instance);
    internal AddressedBuffer? Corners;
    internal int CornerCount;
    internal VkAccelerationStructureKHR Top;
    internal AddressedBuffer? TopStorage, TopScratch;
    internal int Capacity;
    internal readonly AddressedBuffer?[] Instances = new AddressedBuffer?[GpuTextures.RetireFrames + 1];
    internal readonly AddressedBuffer?[] Surfaces = new AddressedBuffer?[GpuTextures.RetireFrames + 1];
    internal int Slot;
    internal Action? Destroy;

    /// <summary>How many copies the frame's top-level structure holds.</summary>
    public int Count { get; internal set; }

    /// <summary>The frame's copies in the order the top-level structure holds them, a copy's index there its place here.</summary>
    public IReadOnlyList<RayInstance> Copies { get; internal set; } = [];

    /// <summary>The bytes the scene holds on the device: the meshes' structures and corners, the top-level structure and its scratch, and the ring of copies.</summary>
    public long Bytes =>
        Meshes.Values.Sum(mesh => (long)mesh.Storage.Size) + (long)(Corners?.Size ?? 0) + (long)(TopStorage?.Size ?? 0) + (long)(TopScratch?.Size ?? 0)
        + Instances.Sum(buffer => (long)(buffer?.Size ?? 0)) + Surfaces.Sum(buffer => (long)(buffer?.Size ?? 0));

    /// <inheritdoc />
    public void Dispose() => Destroy?.Invoke();
}

internal sealed unsafe partial class GraphicsDevice
{
    // An instance record as the top-level build reads it: the copy's 3 by 4 transform, its index
    // and mask, its flags, and its mesh's structure.
    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceRecord
    {
        public Vector4 Row0, Row1, Row2;
        public uint IndexAndMask;
        public uint OffsetAndFlags;
        public ulong Structure;
    }

    // A copy as rayScene's RaySurface holds it, its roughness and metallic as gi_reference.slang
    // reads them.
    [StructLayout(LayoutKind.Sequential)]
    private struct SurfaceRecord
    {
        public Vector4 Color, Emission;
        public uint FirstCorner;
        public float Roughness, Metallic;
        public uint Unused;
    }

    private const uint CullDisableAndOpaque = 0x1 | 0x4;

    /// <summary>Makes a buffer of <paramref name="size"/> bytes in memory of its own with its address, the CPU's to write where <paramref name="host"/>.</summary>
    internal AddressedBuffer CreateAddressedBuffer(ulong size, VkBufferUsageFlags usage, bool host)
    {
        VkBufferCreateInfo info = new()
        {
            size = Math.Max(16, size),
            usage = usage | VkBufferUsageFlags.ShaderDeviceAddress,
            sharingMode = VkSharingMode.Exclusive,
        };
        _deviceApi.vkCreateBuffer(&info, null, out VkBuffer buffer).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Buffer);
        _deviceApi.vkGetBufferMemoryRequirements(buffer, out VkMemoryRequirements requirements);
        var flags = new VkMemoryAllocateFlagsInfo { flags = VkMemoryAllocateFlags.DeviceAddress };
        VkMemoryAllocateInfo allocation = new()
        {
            pNext = &flags,
            allocationSize = requirements.size,
            memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, host
                ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
                : VkMemoryPropertyFlags.DeviceLocal),
        };
        _deviceApi.vkAllocateMemory(&allocation, null, out VkDeviceMemory memory).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        _deviceApi.vkBindBufferMemory(buffer, memory, 0).CheckResult();
        void* mapped = null;
        if (host) _deviceApi.vkMapMemory(memory, 0, Vulkan.VK_WHOLE_SIZE, 0, &mapped).CheckResult();
        var addressInfo = new VkBufferDeviceAddressInfo { buffer = buffer };
        return new AddressedBuffer(this, buffer, memory, _deviceApi.vkGetBufferDeviceAddress(&addressInfo), info.size, mapped);
    }

    internal void DestroyAddressedBuffer(AddressedBuffer buffer)
    {
        if (buffer.Mapped != null) _deviceApi.vkUnmapMemory(buffer.Memory);
        _deviceApi.vkDestroyBuffer(buffer.Buffer);
        _deviceApi.vkFreeMemory(buffer.Memory);
        DeviceObjects.Gone(DeviceObjects.Kind.Buffer);
        DeviceObjects.Gone(DeviceObjects.Kind.Memory);
    }

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
                _deviceApi.vkDestroyAccelerationStructureKHR(mesh.Structure);
                DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
                mesh.Storage.Dispose();
            }
            scene.Meshes.Clear();
            DestroyTop(scene);
            foreach (var buffer in scene.Instances) buffer?.Dispose();
            foreach (var buffer in scene.Surfaces) buffer?.Dispose();
            scene.Corners?.Dispose();
        };
        var cmd = BeginSingleTimeCommands();
        var held = BuildTop(cmd, scene, []);
        EndSingleTimeCommands(cmd);
        held.Dispose();
        return scene;
    }

    private void DestroyTop(GpuRayScene scene)
    {
        if (scene.Top.Handle != 0)
        {
            _deviceApi.vkDestroyAccelerationStructureKHR(scene.Top);
            DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
        }
        scene.Top = default;
        scene.TopStorage?.Dispose();
        scene.TopScratch?.Dispose();
        (scene.TopStorage, scene.TopScratch) = (null, null);
    }

    /// <summary>
    /// Records the frame's meshes for the device's ray tracing: the structure of each mesh in
    /// <paramref name="instances"/> not yet built, from the corners <paramref name="cornersOf"/>
    /// gives, three a triangle in its own space, then the top-level structure of every copy,
    /// ready for the passes after to trace through.
    /// </summary>
    /// <returns>What the builds read, to be let go once no frame in flight reads it.</returns>
    public IDisposable RecordRayScene(ICommandBuffer commandBuffer, GpuRayScene scene, IReadOnlyList<RayInstance> instances,
        Func<object, Vector3[]?> cornersOf)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        var cmd = vkCmd.Handle;
        var retired = new Retired();

        var built = false;
        foreach (var instance in instances)
        {
            if (scene.Meshes.ContainsKey(instance.Mesh) || cornersOf(instance.Mesh) is not { Length: >= 3 } corners) continue;
            var first = AddCorners(scene, corners, retired);
            scene.Meshes[instance.Mesh] = BuildMesh(cmd, scene, first, corners.Length / 3, retired);
            built = true;
        }
        // The meshes' builds before the top-level one reads them.
        if (built)
            MemoryBarrier(cmd, VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureWriteKHR,
                VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureReadKHR);
        retired.Add(BuildTop(cmd, scene, instances));
        return retired;
    }

    // Writes a mesh's corners after those the buffer holds, made again larger where they do not
    // fit, and gives where the first lies.
    private int AddCorners(GpuRayScene scene, Vector3[] corners, Retired retired)
    {
        var needed = scene.CornerCount + corners.Length;
        if (scene.Corners is null || (ulong)needed * 16 > scene.Corners.Size)
        {
            var bigger = CreateAddressedBuffer((ulong)Math.Max(4096, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)needed) * 2) * 16,
                VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyKHR, host: true);
            if (scene.Corners is not null)
            {
                Buffer.MemoryCopy(scene.Corners.Mapped, bigger.Mapped, (long)bigger.Size, scene.CornerCount * 16L);
                retired.Add(scene.Corners);
            }
            scene.Corners = bigger;
        }
        var into = new Span<Vector4>((Vector4*)scene.Corners.Mapped + scene.CornerCount, corners.Length);
        for (int i = 0; i < corners.Length; i++) into[i] = new Vector4(corners[i], 1);
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
        var (structure, storage, address) = Build(cmd, VkAccelerationStructureTypeKHR.BottomLevel, &geometry, (uint)triangles, null, retired);
        return new GpuRayScene.Mesh(structure, storage, address, firstCorner);
    }

    // Records the top-level structure of the frame's copies, made again larger where they do not
    // fit, with their colors in this frame's buffer of the ring.
    private Retired BuildTop(VkCommandBuffer cmd, GpuRayScene scene, IReadOnlyList<RayInstance> instances)
    {
        var retired = new Retired();
        scene.Slot = (scene.Slot + 1) % scene.Instances.Length;
        var count = 0;
        foreach (var instance in instances)
            if (scene.Meshes.ContainsKey(instance.Mesh)) count++;
        var capacity = Math.Max(64, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, count)));

        ref var records = ref scene.Instances[scene.Slot];
        ref var surfaces = ref scene.Surfaces[scene.Slot];
        if (records is null || records.Size < (ulong)capacity * 64)
        {
            if (records is not null) retired.Add(records);
            records = CreateAddressedBuffer((ulong)capacity * 64, VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyKHR, host: true);
        }
        if (surfaces is null || surfaces.Size < (ulong)capacity * 48)
        {
            if (surfaces is not null) retired.Add(surfaces);
            surfaces = CreateAddressedBuffer((ulong)capacity * 48, VkBufferUsageFlags.StorageBuffer, host: true);
        }

        var written = new Span<InstanceRecord>(records.Mapped, count);
        var shaded = new Span<SurfaceRecord>(surfaces.Mapped, count);
        var copies = new List<RayInstance>(count);
        var i = 0;
        foreach (var instance in instances)
        {
            if (!scene.Meshes.TryGetValue(instance.Mesh, out var mesh)) continue;
            // The world's matrix, a point a row as System.Numerics has it, as three rows of a
            // matrix a point is a column of.
            var m = instance.World;
            written[i] = new InstanceRecord
            {
                Row0 = new Vector4(m.M11, m.M21, m.M31, m.M41),
                Row1 = new Vector4(m.M12, m.M22, m.M32, m.M42),
                Row2 = new Vector4(m.M13, m.M23, m.M33, m.M43),
                IndexAndMask = (uint)i | (0xFFu << 24),
                OffsetAndFlags = CullDisableAndOpaque << 24,
                Structure = mesh.Address,
            };
            shaded[i] = new SurfaceRecord
            {
                Color = new Vector4(instance.Color, 1),
                Emission = new Vector4(instance.Emission, 0),
                FirstCorner = (uint)mesh.FirstCorner,
                Roughness = instance.Roughness,
                Metallic = instance.Metallic,
            };
            copies.Add(instance);
            i++;
        }
        scene.Count = count;
        scene.Copies = copies;

        // A structure too small for the copies is made again, the old one let go once no frame
        // reads it.
        if (scene.Top.Handle != 0 && scene.Capacity < count)
        {
            var (top, storage, scratch) = (scene.Top, scene.TopStorage, scene.TopScratch);
            retired.Add(new Released(() =>
            {
                _deviceApi.vkDestroyAccelerationStructureKHR(top);
                DeviceObjects.Gone(DeviceObjects.Kind.AccelerationStructure);
                storage?.Dispose();
                scratch?.Dispose();
            }));
            (scene.Top, scene.TopStorage, scene.TopScratch) = (default, null, null);
        }

        var geometry = new VkAccelerationStructureGeometryKHR
        {
            geometryType = VkGeometryTypeKHR.Instances,
            flags = VkGeometryFlagsKHR.Opaque,
        };
        geometry.geometry.instances = new VkAccelerationStructureGeometryInstancesDataKHR
        {
            arrayOfPointers = false,
            data = new VkDeviceOrHostAddressConstKHR { deviceAddress = records.Address },
        };
        // The frame before's reads of the structure, and its build's writes to the scratch, before
        // this build writes them.
        MemoryBarrier(cmd, VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.AccelerationStructureBuildKHR,
            VkAccessFlags2.AccelerationStructureReadKHR | VkAccessFlags2.AccelerationStructureWriteKHR,
            VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureReadKHR | VkAccessFlags2.AccelerationStructureWriteKHR);
        if (scene.Top.Handle == 0)
        {
            scene.Capacity = capacity;
            var (top, storage, address) = Build(cmd, VkAccelerationStructureTypeKHR.TopLevel, &geometry, (uint)count, scene, retired, (uint)capacity);
            (scene.Top, scene.TopStorage) = (top, storage);
            _ = address;
        }
        else
            Rebuild(cmd, scene, &geometry, (uint)count);
        // The build before the passes that trace through it.
        MemoryBarrier(cmd, VkPipelineStageFlags2.AccelerationStructureBuildKHR, VkAccessFlags2.AccelerationStructureWriteKHR,
            VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.AccelerationStructureReadKHR);
        return retired;
    }

    // Makes a structure of a type for a geometry sized for its largest count, and records its first
    // build, its scratch let go after the frame or, for the top level, kept on the scene.
    private (VkAccelerationStructureKHR Structure, AddressedBuffer Storage, ulong Address) Build(VkCommandBuffer cmd,
        VkAccelerationStructureTypeKHR type, VkAccelerationStructureGeometryKHR* geometry, uint count, GpuRayScene? top, Retired retired,
        uint largest = 0)
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
        _deviceApi.vkGetAccelerationStructureBuildSizesKHR(VkAccelerationStructureBuildTypeKHR.Device, &info, &most, &sizes);

        var storage = CreateAddressedBuffer(sizes.accelerationStructureSize, VkBufferUsageFlags.AccelerationStructureStorageKHR, host: false);
        var create = new VkAccelerationStructureCreateInfoKHR { buffer = storage.Buffer, size = sizes.accelerationStructureSize, type = type };
        VkAccelerationStructureKHR structure;
        _deviceApi.vkCreateAccelerationStructureKHR(&create, null, &structure).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.AccelerationStructure);
        var scratch = CreateAddressedBuffer(sizes.buildScratchSize + ScratchAlignment, VkBufferUsageFlags.StorageBuffer, host: false);
        if (top is not null) top.TopScratch = scratch;
        else retired.Add(scratch);

        info.dstAccelerationStructure = structure;
        info.scratchData = new VkDeviceOrHostAddressKHR { deviceAddress = Aligned(scratch.Address) };
        var range = new VkAccelerationStructureBuildRangeInfoKHR { primitiveCount = count };
        var ranges = &range;
        _deviceApi.vkCmdBuildAccelerationStructuresKHR(cmd, 1, &info, &ranges);
        var addressInfo = new VkAccelerationStructureDeviceAddressInfoKHR { accelerationStructure = structure };
        return (structure, storage, _deviceApi.vkGetAccelerationStructureDeviceAddressKHR(&addressInfo));
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
        _deviceApi.vkCmdBuildAccelerationStructuresKHR(cmd, 1, &info, &ranges);
    }

    // The most any device asks a scratch address to be aligned to, which every buffer of scratch
    // is made that much larger for.
    private const ulong ScratchAlignment = 256;

    private static ulong Aligned(ulong address) => (address + ScratchAlignment - 1) / ScratchAlignment * ScratchAlignment;

    /// <summary>
    /// Binds the scene's top-level structure, this frame's copies and the meshes' corners at the
    /// three bindings from <paramref name="first"/> of <paramref name="set"/>.
    /// </summary>
    public void BindRayScene(IDescriptorSet set, uint first, GpuRayScene scene)
    {
        var handle = ((VulkanDescriptorSet)set).Handle;
        var top = scene.Top;
        var structure = new VkWriteDescriptorSetAccelerationStructureKHR { accelerationStructureCount = 1, pAccelerationStructures = &top };
        var surfaces = scene.Surfaces[scene.Slot]!;
        var buffers = stackalloc VkDescriptorBufferInfo[2];
        buffers[0] = new VkDescriptorBufferInfo { buffer = surfaces.Buffer, offset = 0, range = surfaces.Size };
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
        _deviceApi.vkUpdateDescriptorSets(3, writes, 0, null);
    }

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
