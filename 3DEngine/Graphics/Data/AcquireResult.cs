namespace Engine;

/// <summary>Result of a swapchain image acquisition attempt.</summary>
public enum AcquireResult
{
    /// <summary>Image acquired successfully.</summary>
    Success,
    /// <summary>Swapchain is out of date and must be recreated (e.g., window resize).</summary>
    OutOfDate,
    /// <summary>Image acquired but swapchain is suboptimal - recreation recommended.</summary>
    Suboptimal,
    /// <summary>Acquisition failed due to an unrecoverable error.</summary>
    Error
}
