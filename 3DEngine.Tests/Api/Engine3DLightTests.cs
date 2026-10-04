using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DLightTests : IDisposable
{
    private readonly App _app = new();
    private EcsWorld Ecs => _app.World.Resource<EcsWorld>();

    public Engine3DLightTests()
    {
        _app.World.InsertResource(new EcsWorld());
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    private (Light Light, Transform Transform) Read(LightHandle handle)
    {
        Ecs.TryResolve(handle.Entity, out var entity).Should().BeTrue();
        return (Ecs.GetReadOnly<Light>(entity), Ecs.GetReadOnly<Transform>(entity));
    }

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 1)]
    public void A_Light_Shines_Along_The_Direction_It_Was_Given(float x, float y, float z)
    {
        var direction = Vector3.Normalize(new Vector3(x, y, z));
        var (_, transform) = Read(CreateDirectionalLight(direction, Color.White));

        Vector3.Distance(Vector3.Transform(-Vector3.UnitZ, transform.Rotation), direction).Should().BeLessThan(1e-4f);
    }

    [Fact]
    public void A_Light_Is_Made_Colored_Moved_And_Removed()
    {
        var spot = CreateSpotLight(new Vector3(1, 2, 3), -Vector3.UnitY, new Color(255, 188, 0), 4, 10, 20, range: 8);
        var (light, transform) = Read(spot);
        (light.Kind, light.Intensity, light.Range, light.InnerAngle, light.OuterAngle).Should().Be((LightKind.Spot, 4f, 8f, 10f, 20f));
        light.Color.Y.Should().BeApproximately(0.5f, 0.005f, "a light's color is sRGB as the flat API's are, and linear in the light");
        transform.Position.Should().Be(new Vector3(1, 2, 3));

        SetLightPosition(spot, Vector3.Zero);
        SetLightColor(spot, Color.White, 2);
        Read(spot).Transform.Position.Should().Be(Vector3.Zero);
        Read(spot).Light.Color.Should().Be(Vector3.One);

        UnloadLight(spot);
        Ecs.TryResolve(spot.Entity, out _).Should().BeFalse();
    }

    [Fact]
    public void There_Is_One_Ambient_Light_Which_Intensity_Zero_Removes()
    {
        SetAmbientLight(Color.White, 0.2f);
        SetAmbientLight(Color.Blue, 0.3f);
        var ambient = Ecs.Query<Light>().Where(r => r.Component.Kind == LightKind.Ambient).ToArray();
        ambient.Should().ContainSingle().Which.Component.Intensity.Should().Be(0.3f);

        SetAmbientLight(Color.Blue, 0);
        Ecs.Query<Light>().Should().BeEmpty();
    }
}
