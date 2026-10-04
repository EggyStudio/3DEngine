using System.Runtime.InteropServices;

namespace Engine;

/// <summary>A block of GPU memory a compute shader reads and writes, by its id.</summary>
/// <param name="Id">The buffer's id, 0 for none.</param>
/// <param name="Size">Its size in bytes.</param>
public readonly record struct ShaderBuffer(int Id, int Size)
{
    /// <summary>Whether this names a buffer that was loaded.</summary>
    public bool IsValid => Id > 0;
}

public static partial class Engine3D
{
    // -- Compute shaders, as raylib's rlgl has them: a Slang file with a function marked
    // [shader("compute")] and [numthreads(...)], storage buffers it reads and writes, and a dispatch
    // over groups of its threads.
    //
    //     uniform uint count;
    //     RWStructuredBuffer<float> values;
    //
    //     [shader("compute")]
    //     [numthreads(64, 1, 1)]
    //     void computeMain(uint3 id : SV_DispatchThreadID)
    //     {
    //         if (id.x < count) values[id.x] *= 2;
    //     }
    //
    // Uniforms are set as any shader's are, by name with GetShaderLocation and SetShaderValue, and a
    // storage buffer the same way with SetShaderValueBuffer.

    private const int BufferLocationBase = 1 << 21;

    private static ShaderBufferStore ShaderBuffers => Res<ShaderBufferStore>();
    private static readonly Dictionary<int, ComputePipeline> ComputePipelines = [];
    // The buffer each of a compute shader's storage buffers is set to, by its index in ShaderProgram.Buffers.
    private static readonly Dictionary<int, int[]> BufferValues = [];

    // The device compute runs on, or null in a run with no GPU.
    private static GraphicsDevice? ComputeDevice =>
        _app?.World.TryGetResource<Renderer>(out var renderer) == true && renderer.Context.IsInitialized
            ? renderer.Context.Graphics as GraphicsDevice
            : null;

    /// <summary>Loads and compiles a Slang compute shader file.</summary>
    /// <returns>The shader, or an invalid one when the file cannot be read or has no compute stage, with the reason in the log.</returns>
    public static Shader LoadComputeShader(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadComputeShader: '{fileName}' was not found beside the program or in the working directory.");
            return default;
        }
        return LoadComputeShaderFromMemory(File.ReadAllText(path), Path.GetFileName(path));
    }

    /// <summary>Compiles Slang compute shader source held in memory.</summary>
    /// <returns>The shader, or an invalid one when it does not compile or has no compute stage, with the reason in the log.</returns>
    public static Shader LoadComputeShaderFromMemory(string code, string name = "compute.slang")
    {
        var shader = LoadShaderFromMemory(code, name);
        if (shader.IsValid && !Res<ShaderStore>().Get(shader.Id)!.Stages.ContainsKey(ShaderStage.Compute))
        {
            ApiLogger.Warn($"LoadComputeShader: '{name}' has no function marked [shader(\"compute\")].");
            UnloadShader(shader);
            return default;
        }
        return shader;
    }

    /// <summary>Makes a storage buffer of <paramref name="size"/> bytes, zeroed.</summary>
    /// <returns>The buffer, or an invalid one in a run with no GPU, with the reason in the log.</returns>
    public static ShaderBuffer LoadShaderBuffer(int size)
    {
        if (ComputeDevice is not { } device)
        {
            ApiLogger.Warn("LoadShaderBuffer: compute needs a GPU, and this run has none.");
            return default;
        }
        size = Math.Max(4, (size + 3) / 4 * 4);
        return new ShaderBuffer(ShaderBuffers.Add(device.CreateStorageBuffer(size)), size);
    }

    /// <summary>Makes a storage buffer holding <paramref name="data"/>.</summary>
    public static ShaderBuffer LoadShaderBuffer<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        var buffer = LoadShaderBuffer(data.Length * Marshal.SizeOf<T>());
        UpdateShaderBuffer(buffer, data);
        return buffer;
    }

    /// <summary>Frees a storage buffer, once the dispatches using it have finished.</summary>
    public static void UnloadShaderBuffer(ShaderBuffer buffer)
    {
        if (!ShaderBuffers.Remove(buffer.Id, out var gpu)) return;
        // A frame in flight may still draw with it, as a dispatch may still write it.
        if (ComputeDevice is { } device)
        {
            device.WaitForCompute();
            device.WaitIdle();
        }
        gpu!.Dispose();
    }

    /// <summary>Whether <paramref name="buffer"/> is loaded.</summary>
    public static bool IsShaderBufferValid(ShaderBuffer buffer) => TryRes<ShaderBufferStore>(out var buffers) && buffers.Get(buffer.Id) is not null;

    /// <summary>Writes <paramref name="data"/> into a storage buffer, starting <paramref name="offset"/> bytes in.</summary>
    /// <remarks>Waits for the dispatches already submitted, so it never changes what one of them is reading.</remarks>
    public static void UpdateShaderBuffer<T>(ShaderBuffer buffer, ReadOnlySpan<T> data, int offset = 0) where T : unmanaged
    {
        if (ShaderBuffers.Get(buffer.Id) is not { } gpu || ComputeDevice is not { } device) return;
        device.WaitForCompute();
        var target = device.Map(gpu);
        var bytes = MemoryMarshal.AsBytes(data);
        if (offset < 0 || offset >= target.Length) return;
        bytes[..Math.Min(bytes.Length, target.Length - offset)].CopyTo(target[offset..]);
    }

    /// <summary>Reads a storage buffer into <paramref name="destination"/>, starting <paramref name="offset"/> bytes in.</summary>
    /// <remarks>
    /// Waits for the dispatches already submitted, so it reads what they wrote. That holds the
    /// program until the GPU has caught up, which a game reading every frame pays for each time.
    /// </remarks>
    public static void ReadShaderBuffer<T>(ShaderBuffer buffer, Span<T> destination, int offset = 0) where T : unmanaged
    {
        if (ShaderBuffers.Get(buffer.Id) is not { } gpu || ComputeDevice is not { } device) return;
        device.WaitForCompute();
        var source = device.Map(gpu);
        var bytes = MemoryMarshal.AsBytes(destination);
        if (offset < 0 || offset >= source.Length) return;
        source.Slice(offset, Math.Min(bytes.Length, source.Length - offset)).CopyTo(bytes);
    }

    /// <summary>
    /// Sets a storage buffer the shader uses, found by name with <see cref="GetShaderLocation"/>,
    /// for the dispatches and draws after it, as raylib's <c>rlBindShaderBuffer</c>.
    /// </summary>
    /// <remarks>
    /// A compute shader writes it as a <c>RWStructuredBuffer</c>, and a shader that draws, an
    /// immediate one in <see cref="BeginShaderMode"/> or a model's, reads it as a
    /// <c>StructuredBuffer</c>, so what a dispatch computed is drawn with no copy through the CPU.
    /// A draw reads what the dispatches before it in the program wrote.
    /// </remarks>
    public static void SetShaderValueBuffer(Shader shader, int location, ShaderBuffer buffer)
    {
        if (!shader.IsValid || Res<ShaderStore>().Get(shader.Id) is not { } program) return;
        var index = location - BufferLocationBase;
        if (index < 0 || index >= program.Buffers.Count) return;
        if (!BufferValues.TryGetValue(shader.Id, out var values)) BufferValues[shader.Id] = values = new int[program.Buffers.Count];
        values[index] = buffer.Id;
        if (_shader == shader) DrawList.SetShader(shader.Id, ShaderValues.GetValueOrDefault(shader.Id), UniformSnapshot(shader), TextureSnapshot(shader));
    }

    /// <summary>
    /// Runs a compute shader over <paramref name="groupsX"/> by <paramref name="groupsY"/> by
    /// <paramref name="groupsZ"/> groups of the threads its <c>numthreads</c> names, with the
    /// uniforms and buffers set on it.
    /// </summary>
    /// <remarks>
    /// The GPU runs it before the frame being drawn and after any dispatch before it, while the
    /// program goes on. <see cref="ReadShaderBuffer"/> waits for it. A storage buffer the shader
    /// declares and has not been given is left out, and the dispatch is skipped with the reason in
    /// the log.
    /// </remarks>
    public static void ComputeShaderDispatch(Shader shader, int groupsX, int groupsY, int groupsZ)
    {
        if (!shader.IsValid || Res<ShaderStore>().Get(shader.Id) is not { } program || ComputeDevice is not { } device) return;
        if (!program.Stages.TryGetValue(ShaderStage.Compute, out var spirv))
        {
            ApiLogger.Warn($"ComputeShaderDispatch: '{program.Name}' has no compute stage.");
            return;
        }

        var values = BufferValues.GetValueOrDefault(shader.Id) ?? new int[program.Buffers.Count];
        var buffers = new List<(int, IBuffer)>(program.Buffers.Count);
        for (int i = 0; i < program.Buffers.Count; i++)
        {
            if (ShaderBuffers.Get(values[i]) is not { } gpu)
            {
                ApiLogger.Warn($"ComputeShaderDispatch: '{program.Name}' has no buffer set for '{program.Buffers[i].Name}'.");
                return;
            }
            buffers.Add((program.Buffers[i].Binding, gpu));
        }

        // The textures it writes and samples, as the renderer holds them.
        var gpuTextures = Res<Renderer>().RenderWorld.TryGet<GpuTextures>();
        var images = new List<(int, IImage, IImageView)>(program.Images.Count);
        if (program.Images.Count > 0)
        {
            if (!device.CanWriteImages || gpuTextures is null)
            {
                ApiLogger.Warn($"ComputeShaderDispatch: '{program.Name}' writes a texture, which this GPU cannot do without its format named.");
                return;
            }
            var imageIds = ImageValues.GetValueOrDefault(shader.Id) ?? new int[program.Images.Count];
            for (int i = 0; i < program.Images.Count; i++)
            {
                if (gpuTextures.StorageFor(imageIds[i]) is not { } storage)
                {
                    ApiLogger.Warn($"ComputeShaderDispatch: '{program.Name}' has no texture on the GPU for '{program.Images[i].Name}'. "
                                   + "A texture reaches the GPU in the frame after it is loaded, and a render texture cannot be written.");
                    return;
                }
                images.Add((program.Images[i].Binding, storage.Image, storage.View));
            }
        }
        var textures = new List<(int, IImageView, ISampler)>(program.Textures.Count);
        var textureIds = TextureValues.GetValueOrDefault(shader.Id);
        for (int i = 0; i < program.Textures.Count && gpuTextures is not null; i++)
        {
            var (view, sampler) = gpuTextures.ViewFor(device, textureIds is { } ids && i < ids.Length ? ids[i] : 0);
            textures.Add((program.Textures[i].Binding, view, sampler));
        }

        if (!ComputePipelines.TryGetValue(shader.Id, out var pipeline))
            ComputePipelines[shader.Id] = pipeline = device.CreateComputePipeline(spirv, program.UniformSize, [.. program.Buffers.Select(b => b.Binding)],
                [.. program.Images.Select(i => i.Binding)], [.. program.Textures.Select(t => t.Binding)]);
        device.Dispatch(pipeline, UniformValues.GetValueOrDefault(shader.Id) ?? [], buffers, (uint)groupsX, (uint)groupsY, (uint)groupsZ, images, textures);
    }

    // A compute shader's pipeline goes with it.
    private static void ForgetComputeShader(int id)
    {
        BufferValues.Remove(id);
        if (ComputePipelines.Remove(id, out var pipeline)) pipeline.Dispose();
    }

    // Frees the buffers and pipelines a program left loaded, before the device they belong to goes.
    private static void ForgetCompute()
    {
        ComputeDevice?.WaitForCompute();
        if (TryRes<ShaderBufferStore>(out var buffers))
            foreach (var buffer in buffers.TakeAll()) buffer.Dispose();
        foreach (var pipeline in ComputePipelines.Values) pipeline.Dispose();
        ComputePipelines.Clear();
        BufferValues.Clear();
    }
}
