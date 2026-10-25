using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL3;

namespace Engine;

// What runs over the device's mixed samples, the program's processors, on SDL's audio thread.
internal sealed partial class SdlAudioBackend
{
    // What runs over the mixed samples, read on the audio thread as a whole array that is
    // replaced rather than changed, and the handle SDL is given back to find this backend by.
    private volatile AudioCallback[] _mixedProcessors = [];
    private GCHandle _postmixHandle;
    private bool _warnedProcessor;

    // SDL3-CS hands the postmix buffer to C# as an array it cannot size, so the call is made here
    // with a function pointer, the buffer reaching the processors in place.
    [LibraryImport("SDL3", EntryPoint = "SDL_SetAudioPostmixCallback")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static unsafe partial bool SetAudioPostmixCallback(uint devid, delegate* unmanaged[Cdecl]<IntPtr, IntPtr, float*, int, void> callback, IntPtr userdata);

    /// <inheritdoc />
    public unsafe void SetMixedProcessors(AudioCallback[] processors)
    {
        lock (_lock)
        {
            _mixedProcessors = processors;
            if (_device == 0) return;
            if (processors.Length > 0 && !_postmixHandle.IsAllocated)
            {
                _postmixHandle = GCHandle.Alloc(this);
                if (!SetAudioPostmixCallback(_device, &Postmix, GCHandle.ToIntPtr(_postmixHandle)))
                    Logger.Warn($"SdlAudioBackend: SDL_SetAudioPostmixCallback failed with '{SDL.GetError()}', so the mixed processors do not run.");
            }
            else if (processors.Length == 0 && _postmixHandle.IsAllocated)
                StopPostmix();
        }
    }

    // Takes the callback off the device, which SDL does with the device locked, so it is not
    // running when the handle is freed. Called inside the lock.
    private unsafe void StopPostmix()
    {
        if (_device != 0) SetAudioPostmixCallback(_device, null, IntPtr.Zero);
        _postmixHandle.Free();
    }

    // The mixed samples, handed to each processor in turn on SDL's audio thread. An exception
    // cannot cross back into SDL, so the first is logged and the buffer goes on as the processors
    // left it.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Postmix(IntPtr userdata, IntPtr spec, float* buffer, int buflen)
    {
        SdlAudioBackend? backend = null;
        try
        {
            backend = GCHandle.FromIntPtr(userdata).Target as SdlAudioBackend;
            if (backend is null) return;
            var samples = new Span<float>(buffer, buflen / sizeof(float));
            foreach (var processor in backend._mixedProcessors) processor(samples);
        }
        catch (Exception ex)
        {
            if (backend is null || backend._warnedProcessor) return;
            backend._warnedProcessor = true;
            Logger.Warn($"SdlAudioBackend: a mixed processor threw {ex.GetType().Name}: {ex.Message}");
        }
    }
}
