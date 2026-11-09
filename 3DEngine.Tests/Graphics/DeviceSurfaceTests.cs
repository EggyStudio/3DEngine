using FluentAssertions;
using Engine.Tests.Api;

namespace Engine.Tests.Graphics;

/// <summary>
/// What the rest of the engine reaches on the Vulkan device against
/// <c>3DEngine/Graphics/DeviceSurface.txt</c>, the listing checked in beside it, every member of
/// <see cref="GraphicsDevice"/> and of its nested types that is not private.
/// </summary>
/// <remarks>
/// An area's work on the device, as the path-traced reference's, is a part the device keeps
/// (<see cref="GraphicsDevice.Part{T}"/>) and calls the device through these members, so a commit
/// that opens one more to the areas changes the listing too and is read as such a change.
/// <c>build/api.sh</c> writes it again with the public listing.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DeviceSurfaceTests
{
    [Fact]
    public void The_Device_Surface_Is_The_Listing_Checked_In() =>
        PublicSurfaceTests.IsTheListing(PublicSurface.Write(Reached(typeof(GraphicsDevice)), internals: true),
            Path.Combine(CheatsheetTests.RepoRoot(), "3DEngine", "Graphics", "DeviceSurface.txt"), "the device's surface");

    [Fact]
    public void The_Device_Listing_Names_Its_Internal_Members_And_No_Private_One()
    {
        var listing = PublicSurface.Write(Reached(typeof(GraphicsDevice)), internals: true);
        listing.Should().Contain("  internal T Part<T>(Func<GraphicsDevice, T> make) where T : class, IDisposable\n");
        listing.Should().Contain("sealed class Engine.GraphicsDevice.VulkanBuffer : IBuffer, IDisposable\n");
        listing.Should().NotContain(" _deviceApi", "a private field is the device's own");
        listing.Should().NotContain("DisposeParts", "nor a private method");
    }

    // The type and those nested in it that the rest of the assembly reaches.
    private static IEnumerable<Type> Reached(Type type) =>
        type.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Where(nested => nested.IsNestedPublic || nested.IsNestedAssembly || nested.IsNestedFamORAssem)
            .SelectMany(Reached).Prepend(type);
}
