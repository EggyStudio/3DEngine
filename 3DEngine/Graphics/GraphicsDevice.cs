using System.Diagnostics;
using System.Text;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// Vulkan-backed graphics device implementing GPU resource creation, frame management, and rendering.
/// </summary>
/// <remarks>
/// <para>
/// Initialization follows a 6-step pipeline: Vulkan instance → surface → physical device selection →
/// logical device → swapchain resources → synchronization objects. The device supports triple-buffering
/// with <see cref="MaxFramesInFlight"/> frames in flight.
/// </para>
/// <para>
/// This class is split across multiple partial files for maintainability:
/// <list>
///   <item><description><c>GraphicsDevice.cs</c> shared state, <see cref="Initialize"/>, <see cref="OnResize"/>, <see cref="Dispose"/>, and orchestration logic.</description></item>
///   <item><description><c>GraphicsDevice.Instance.cs</c> Vulkan instance creation, validation layers, and debug messenger.</description></item>
///   <item><description><c>GraphicsDevice.Surface.cs</c> platform window surface binding via <c>VkSurfaceKHR</c>.</description></item>
///   <item><description><c>GraphicsDevice.PhysicalDevice.cs</c> GPU enumeration, capability scoring, and adapter selection.</description></item>
///   <item><description><c>GraphicsDevice.Device.cs</c> logical device and queue creation (graphics + present).</description></item>
///   <item><description><c>GraphicsDevice.Swapchain.cs</c> swapchain, image views, depth buffer, the frame images' attachments, and command pool.</description></item>
///   <item><description><c>GraphicsDevice.Sync.cs</c> semaphores and fences for frame-in-flight synchronization.</description></item>
///   <item><description><c>GraphicsDevice.Frame.cs</c> frame acquisition (<see cref="BeginFrame"/>) and presentation (<see cref="EndFrame"/>).</description></item>
///   <item><description><c>GraphicsDevice.Buffers.cs</c> GPU buffer creation, memory allocation, and staging uploads.</description></item>
///   <item><description><c>GraphicsDevice.Images.cs</c> image/texture creation and layout transitions.</description></item>
///   <item><description><c>GraphicsDevice.Pipeline.cs</c> graphics pipeline and shader module creation.</description></item>
///   <item><description><c>GraphicsDevice.Descriptors.cs</c> descriptor set layouts, pool management, and descriptor writes.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="IGraphicsDevice"/>
/// <seealso cref="NullGraphicsDevice"/>
internal sealed partial class GraphicsDevice : IGraphicsDevice
{
    /// <summary>Maximum number of frames that can be in flight simultaneously (triple buffering).</summary>
    private const int MaxFramesInFlight = 3;

    /// <summary>Maximum byte length for Vulkan physical device name strings.</summary>
    private const int MaxPhysicalDeviceNameSize = 256;

    private static readonly ILogger Logger = Log.Category("Engine.Graphics");

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <inheritdoc />
    public ISwapchain Swapchain => _swapchainWrapper;

    /// <inheritdoc />
    public GraphicsAdapterInfo AdapterInfo => _adapterInfo;

    /// <inheritdoc />
    public int FramesInFlight => MaxFramesInFlight;

    /// <inheritdoc />
    public IRenderPass SwapchainRenderPass => WindowPass;

    /// <inheritdoc />
    public IFramebuffer GetSwapchainFramebuffer(uint imageIndex) => _framebuffers[imageIndex];

    /// <summary>Creates a new uninitialized Vulkan graphics device. Call <see cref="Initialize"/> before use.</summary>
    public GraphicsDevice() => _swapchainWrapper = new VulkanSwapchain(this);

    /// <inheritdoc />
    public void Initialize(ISurfaceSource surfaceSource, string appName = "3DEngine")
    {
        if (IsInitialized) return;
        _surfaceSource = surfaceSource;
        _offscreen = surfaceSource.IsOffscreen;
        _finalLayout = _offscreen ? VkImageLayout.TransferSrcOptimal : VkImageLayout.PresentSrcKHR;

        Logger.Info("Initializing Vulkan graphics device...");
        var totalSw = Stopwatch.StartNew();

        Logger.Info("Step 1/6: Creating Vulkan instance, loading Vulkan library, setting up validation layers, and creating the VkInstance...");
        var sw = Stopwatch.StartNew();
        CreateInstance(appName);
        Logger.Info($"Step 1/6: Vulkan instance created in {sw.ElapsedMilliseconds}ms");

        Logger.Info("Step 2/6: Creating window surface, binding the platform window to Vulkan via VkSurfaceKHR...");
        sw.Restart();
        if (!_offscreen) CreateSurface();
        Logger.Info($"Step 2/6: Window surface created in {sw.ElapsedMilliseconds}ms");

        Logger.Info("Step 3/6: Selecting physical device, enumerating GPUs, scoring capabilities, and choosing the best adapter...");
        sw.Restart();
        SelectPhysicalDevice();
        Logger.Info($"Step 3/6: Physical device selected in {sw.ElapsedMilliseconds}ms, {_adapterInfo.Name} (Vendor=0x{_adapterInfo.VendorId:X4}, Device=0x{_adapterInfo.DeviceId:X4}, Type={_adapterInfo.DeviceType})");

        Logger.Info("Step 4/6: Creating logical device, setting up device queues (graphics + present) and enabling required extensions...");
        sw.Restart();
        CreateLogicalDevice();
        Logger.Info($"Step 4/6: Logical device created in {sw.ElapsedMilliseconds}ms (graphicsQueue={_graphicsQueueFamily}, presentQueue={_presentQueueFamily})");

        Logger.Info("Step 5/6: Creating swapchain resources (swapchain, image views, depth buffer and command pool)...");
        sw.Restart();
        CreateSwapchainResources();
        Logger.Info($"Step 5/6: Swapchain resources created in {sw.ElapsedMilliseconds}ms ({_swapchainImages.Length} images, {_swapchainExtent.width}x{_swapchainExtent.height}, format={_swapchainFormat})");

        Logger.Info("Step 6/6: Creating synchronization objects, the semaphores and fences of each frame in flight...");
        sw.Restart();
        CreateSyncObjects();
        Logger.Info($"Step 6/6: Sync objects created in {sw.ElapsedMilliseconds}ms ({MaxFramesInFlight} frames-in-flight)");

        // Initialize per-frame deferred staging buffer lists
        _deferredStagingBuffers = new List<IBuffer>?[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++)
            _deferredStagingBuffers[i] = new List<IBuffer>();

        Logger.Info("Creating descriptor resources, the descriptor set layouts and descriptor pool for uniform buffers and samplers...");
        sw.Restart();
        CreateDescriptorResources();
        Logger.Info($"Descriptor resources created in {sw.ElapsedMilliseconds}ms");

        totalSw.Stop();
        Logger.Info($"Graphics device initialized in {totalSw.ElapsedMilliseconds}ms, {_adapterInfo.Name} (Vendor=0x{_adapterInfo.VendorId:X4}, Device=0x{_adapterInfo.DeviceId:X4}, Type={_adapterInfo.DeviceType})");

        IsInitialized = true;
    }

    /// <inheritdoc />
    public void WaitIdle()
    {
        if (!IsInitialized) return;
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle();
        RetireUploads(all: true);
    }

    /// <inheritdoc />
    public void OnResize()
    {
        if (!IsInitialized) return;
        Logger.Info("Swapchain resize requested, waiting for the device to idle before recreating it...");
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        // Uploads' command buffers come from the pool the swapchain's resources go with.
        RetireUploads(all: true);
        Logger.Debug("Device idle, destroying the old swapchain resources...");
        DestroySwapchainResources();
        Logger.Debug("Old swapchain resources destroyed, creating new ones...");
        CreateSwapchainResources();
        _resizeVersion++;
        _suboptimalLogged = false;
        Logger.Info($"Swapchain resized to {_swapchainExtent.width}x{_swapchainExtent.height} (revision={_resizeVersion}, images={_swapchainImages.Length})");
    }

    /// <inheritdoc />
    public IFrameContext BeginFrame(ClearColor? clearOverride = null) => BeginFrameInternal(clearOverride ?? ClearColor.Black);

    /// <inheritdoc />
    public void EndFrame(IFrameContext frameContext)
    {
        SubmitFrame((VulkanFrameContext)frameContext);
        frameContext.Dispose();
    }

    /// <inheritdoc />
    public void FlushDeferredStagingBuffers(int inFlightIndex)
    {
        if (inFlightIndex < 0 || inFlightIndex >= _deferredStagingBuffers.Length) return;
        var list = _deferredStagingBuffers[inFlightIndex];
        if (list is null || list.Count == 0) return;

        foreach (var buffer in list)
            buffer.Dispose();
        list.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!IsInitialized) return;
        Logger.Info("Disposing graphics device, waiting for the device to idle...");
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle();
        RetireUploads(all: true);
        DestroyTimestamps();

        // Flush all deferred staging buffers
        for (int i = 0; i < _deferredStagingBuffers.Length; i++)
            FlushDeferredStagingBuffers(i);

        Logger.Debug("Destroying sync objects (semaphores, fences)...");
        DestroySyncObjects();
        Logger.Debug("Destroying swapchain resources (image views, depth buffer, command pool)...");
        DestroySwapchainResources();
        DisposeKeptScreen();
        DestroyCompute();
        DestroySkinning();
        DisposeProbeFilter();
        DestroySceneField();
        DestroyGlobalIllumination();
        DestroyParticles();
        Logger.Debug("Destroying descriptor resources (pool, layouts)...");
        DestroyDescriptorResources();
        DestroyMemoryBlocks();
        Logger.Debug("Destroying logical device...");
        DestroyLogicalDevice();
        Logger.Debug("Destroying window surface...");
        DestroySurface();
        Logger.Debug("Destroying Vulkan instance and debug messenger...");
        DestroyInstance();
        IsInitialized = false;
        Logger.Info("Graphics device disposed successfully.");
    }

    // Shared state
    private ISurfaceSource? _surfaceSource;
    private VkInstance _instance;
    private VkPhysicalDevice _physicalDevice;
    private VkDevice _device;
    private VkSurfaceKHR _surface;
    private VkSwapchainKHR _swapchain;
    private VkFormat _swapchainFormat;
    private VkExtent2D _swapchainExtent;
    private VkImage[] _swapchainImages = Array.Empty<VkImage>();
    private VkImageView[] _swapchainImageViews = Array.Empty<VkImageView>();
    private VulkanFramebuffer[] _framebuffers = [];
    private uint _graphicsQueueFamily;
    private uint _presentQueueFamily;
    private VkQueue _graphicsQueue;
    private VkQueue _presentQueue;

    // With no window, the frames are drawn into images the device owns rather than a swapchain's,
    // and left in a layout a capture copies from rather than one a display presents.
    private bool _offscreen;
    private VkImageLayout _finalLayout = VkImageLayout.PresentSrcKHR;
    private VkDeviceMemory[] _offscreenMemory = [];
    private uint _offscreenNext;
    private VkInstanceApi _instanceApi = null!;
    private VkDeviceApi _deviceApi = null!;
    private VkCommandPool _commandPool;
    private VkCommandBuffer[] _commandBuffers = Array.Empty<VkCommandBuffer>();
    private VkSemaphore[] _imageAvailableSemaphores = Array.Empty<VkSemaphore>();
    private VkSemaphore[] _renderFinishedSemaphores = Array.Empty<VkSemaphore>();
    private VkFence[] _inFlightFences = Array.Empty<VkFence>();
    private int _currentFrame;
    private uint _lastAcquiredImageIndex;
    private readonly VulkanSwapchain _swapchainWrapper;
    private ulong _resizeVersion;
    private bool _suboptimalLogged;
    private VkDebugUtilsMessengerEXT _debugMessenger;
    private bool _validationEnabled;

    // The frame's samples, and with more than one the shared color image the passes draw into.
    private VkSampleCountFlags _samples = VkSampleCountFlags.Count1;
    private VkImage _msaaColorImage;
    private VkDeviceMemory _msaaColorMemory;
    private VkImageView _msaaColorView;

    /// <summary>
    /// How many samples a pixel is drawn with, set before <see cref="Initialize"/>: 1 for none, or 2,
    /// 4 or 8 to smooth the edges of triangles, rounded down to what the device can do.
    /// </summary>
    public int RequestedSamples { get; set; } = 1;

    /// <summary>Whether frames wait for the display's refresh, set before <see cref="Initialize"/>.</summary>
    public bool Vsync { get; set; }

    /// <summary>
    /// Whether the window's frames carry their alpha to the desktop, which shows through where they
    /// are clear, set before <see cref="Initialize"/>. Where the surface composites only opaque
    /// frames they are presented opaque.
    /// </summary>
    public bool TransparentWindow { get; set; }

    /// <summary>The samples frames and render targets are drawn with, once the swapchain exists.</summary>
    public int Samples => (int)_samples;
    private GraphicsAdapterInfo _adapterInfo = GraphicsAdapterInfo.Unknown;
    private VkImage _depthImage;
    private VkDeviceMemory _depthImageMemory;
    private VkImageView _depthImageView;
    private List<IBuffer>?[] _deferredStagingBuffers = Array.Empty<List<IBuffer>?>();

    /// <summary>Decodes a null-terminated UTF-8 byte span into a managed string.</summary>
    private static string Utf8(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty) return string.Empty;
        var nullIndex = value.IndexOf((byte)0);
        if (nullIndex >= 0) value = value[..nullIndex];
        return Encoding.UTF8.GetString(value);
    }

    // Partial methods implemented across split files

    /// <summary>Creates the Vulkan instance, configures validation layers and the debug messenger.</summary>
    private partial void CreateInstance(string appName);

    /// <summary>Destroys the debug messenger and Vulkan instance.</summary>
    private partial void DestroyInstance();

    /// <summary>Creates the <c>VkSurfaceKHR</c> from the platform surface source.</summary>
    private partial void CreateSurface();

    /// <summary>Destroys the <c>VkSurfaceKHR</c> if it was created.</summary>
    private partial void DestroySurface();

    /// <summary>Enumerates physical devices, scores their capabilities, and selects the best GPU.</summary>
    private partial void SelectPhysicalDevice();

    /// <summary>Creates the Vulkan logical device and retrieves the graphics and present queues.</summary>
    private partial void CreateLogicalDevice();

    /// <summary>Destroys the Vulkan logical device.</summary>
    private partial void DestroyLogicalDevice();

    /// <summary>Creates the swapchain, image views, depth buffer, the frame images' attachments, and command pool.</summary>
    private partial void CreateSwapchainResources();

    /// <summary>Destroys all swapchain-related resources including image views, depth buffer, and command pool.</summary>
    private partial void DestroySwapchainResources();

    /// <summary>Creates semaphores and pre-signaled fences for each frame-in-flight.</summary>
    private partial void CreateSyncObjects();

    /// <summary>Destroys all synchronization fences and semaphores.</summary>
    private partial void DestroySyncObjects();

    /// <summary>Acquires the next swapchain image and begins a command buffer (render pass lifecycle managed by nodes).</summary>
    private partial IFrameContext BeginFrameInternal(ClearColor clearColor);

    /// <summary>Ends the command buffer, submits to the graphics queue, and presents the frame.</summary>
    private partial void SubmitFrame(VulkanFrameContext ctx);
}
