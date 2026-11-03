using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>The Vulkan the device is made with, on a real Vulkan device.</summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class DeviceVersionTests
{
    [NeedsVulkanFact]
    public void The_Device_Chosen_Offers_Vulkan_1_4_And_Names_It_As_Its_Numbers()
    {
        // The instance asks for 1.4 and a device that offers less is passed over, so the one
        // chosen offers it, its version read in the log's plain form, 1.4.354.
        using var device = new GraphicsDevice();
        device.Initialize(new OffscreenSurface(16, 16), "version test");
        var parts = device.AdapterInfo.Api.Split('.').Select(int.Parse).ToArray();
        parts.Should().HaveCount(3, $"the version reads as its three numbers, {device.AdapterInfo.Api}");
        (parts[0] * 1000 + parts[1]).Should().BeGreaterThanOrEqualTo(1004, $"{device.AdapterInfo.Name} offers Vulkan 1.4, {device.AdapterInfo.Api}");
    }
}
