using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Scenes;

/// <summary>
/// Pure-managed tests for the backend-agnostic payload types under
/// <c>3DEngine/Scenes/Payloads/</c>. These pin the payload contracts (defaults, required
/// fields, derived helpers) without loading any file.
/// </summary>
[Trait("Category", "Unit")]
public class ScenePayloadTests
{
    [Fact]
    public void SceneMeshPayload_Defaults_Are_Empty_And_Optional_Channels_Null()
    {
        var positions = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY };
        var indices = new[] { 0, 1, 2 };

        var payload = new SceneMeshPayload
        {
            Positions = positions,
            Indices = indices,
        };

        payload.Name.Should().Be("Mesh");
        payload.Positions.Should().BeSameAs(positions);
        payload.Indices.Should().BeSameAs(indices);
        payload.Normals.Should().BeNull();
        payload.Tangents.Should().BeNull();
        payload.Uv0.Should().BeNull();
        payload.Uv1.Should().BeNull();
        payload.Colors.Should().BeNull();
        payload.Subsets.Should().BeEmpty();
        payload.LocalBounds.Should().Be(SceneBounds.Empty);
    }

    [Fact]
    public void SceneMeshSubset_Stores_All_Fields_Including_Null_Material()
    {
        var s = new SceneMeshSubset("matte", IndexStart: 6, IndexCount: 9, MaterialPath: null);

        s.Name.Should().Be("matte");
        s.IndexStart.Should().Be(6);
        s.IndexCount.Should().Be(9);
        s.MaterialPath.Should().BeNull();
    }

    [Fact]
    public void SceneBounds_Empty_Is_Inverted_And_Invalid()
    {
        SceneBounds.Empty.IsValid.Should().BeFalse();
        SceneBounds.Empty.Min.X.Should().Be(float.PositiveInfinity);
        SceneBounds.Empty.Max.X.Should().Be(float.NegativeInfinity);
    }

    [Fact]
    public void SceneBounds_FromPositions_Computes_Aabb_Center_And_Size()
    {
        ReadOnlySpan<Vector3> pts = stackalloc Vector3[]
        {
            new(-1, 0, 2),
            new(1, 4, -2),
            new(0, 2, 0),
        };

        var b = SceneBounds.FromPositions(pts);

        b.IsValid.Should().BeTrue();
        b.Min.Should().Be(new Vector3(-1, 0, -2));
        b.Max.Should().Be(new Vector3(1, 4, 2));
        b.Center.Should().Be(new Vector3(0, 2, 0));
        b.Size.Should().Be(new Vector3(2, 4, 4));
    }

    [Fact]
    public void SceneBounds_FromPositions_Empty_Span_Returns_Empty()
    {
        SceneBounds.FromPositions(ReadOnlySpan<Vector3>.Empty).Should().Be(SceneBounds.Empty);
    }

    [Fact]
    public void SceneMaterialPayload_Defaults_Match_UsdPreviewSurface_Spec()
    {
        var mat = new SceneMaterialPayload { SourcePath = "/Looks/M" };

        mat.Name.Should().Be("Material");
        mat.SourcePath.Should().Be("/Looks/M");
        mat.BaseColorFactor.Should().Be(Vector4.One);
        mat.BaseColorTexture.Should().BeNull();
        mat.MetallicFactor.Should().Be(0f);
        mat.RoughnessFactor.Should().Be(1f);
        mat.MetallicRoughnessTexture.Should().BeNull();
        mat.NormalTexture.Should().BeNull();
        mat.NormalScale.Should().Be(1f);
        mat.EmissiveFactor.Should().Be(Vector3.Zero);
        mat.EmissiveTexture.Should().BeNull();
        mat.OcclusionTexture.Should().BeNull();
        mat.OcclusionStrength.Should().Be(1f);
        mat.AlphaMode.Should().Be(SceneAlphaMode.Opaque);
        mat.AlphaCutoff.Should().Be(0.5f);
        mat.DoubleSided.Should().BeFalse();
    }

    [Fact]
    public void SceneTextureRef_Defaults_To_Uv0_And_Repeat_Wrap()
    {
        var t = new SceneTextureRef("textures/albedo.png");

        t.AssetPath.Should().Be("textures/albedo.png");
        t.UvSet.Should().Be(0);
        t.WrapS.Should().Be(SceneWrapMode.Repeat);
        t.WrapT.Should().Be(SceneWrapMode.Repeat);
    }

    [Fact]
    public void SceneCameraPayload_Defaults_Are_35mm_Academy_Perspective()
    {
        var cam = new SceneCameraPayload();

        cam.Name.Should().Be("Camera");
        cam.Projection.Should().Be(SceneProjection.Perspective);
        cam.HorizontalAperture.Should().BeApproximately(20.955f, 1e-4f);
        cam.VerticalAperture.Should().BeApproximately(15.2908f, 1e-4f);
        cam.FocalLength.Should().Be(50f);
        cam.NearClip.Should().Be(0.1f);
        cam.FarClip.Should().Be(1000f);
        cam.FocusDistance.Should().BeNull();
        cam.FStop.Should().BeNull();
    }

    [Fact]
    public void SceneCameraPayload_VerticalFovRadians_Matches_Physical_Formula()
    {
        // 50mm focal length / 24mm vertical aperture is the standard "full-frame 50mm"
        // lens, with a vertical FOV of ~27 degrees.
        var cam = new SceneCameraPayload
        {
            Projection = SceneProjection.Perspective,
            VerticalAperture = 24f,
            FocalLength = 50f,
        };

        var expected = 2f * MathF.Atan(24f / (2f * 50f));
        cam.VerticalFovRadians.Should().BeApproximately(expected, 1e-6f);
        // Sanity: ~0.4731 rad ~= 27.1 deg.
        (cam.VerticalFovRadians * (180f / MathF.PI)).Should().BeApproximately(27.0f, 0.5f);
    }

    [Theory]
    [InlineData(SceneProjection.Orthographic, 50f)]
    [InlineData(SceneProjection.Perspective, 0f)]
    [InlineData(SceneProjection.Perspective, -10f)]
    public void SceneCameraPayload_VerticalFovRadians_Returns_Zero_For_Degenerate_Cases(
        SceneProjection projection, float focalLength)
    {
        var cam = new SceneCameraPayload
        {
            Projection = projection,
            VerticalAperture = 24f,
            FocalLength = focalLength,
        };

        cam.VerticalFovRadians.Should().Be(0f);
    }

    [Fact]
    public void SceneLightPayload_Defaults_Are_White_Unit_Intensity()
    {
        var light = new SceneLightPayload { Type = SceneLightType.Sphere };

        light.Name.Should().Be("Light");
        light.Type.Should().Be(SceneLightType.Sphere);
        light.Color.Should().Be(Vector3.One);
        light.Intensity.Should().Be(1f);
        light.Exposure.Should().Be(0f);
        light.Radius.Should().BeNull();
        light.Width.Should().BeNull();
        light.Height.Should().BeNull();
        light.Length.Should().BeNull();
        light.ConeAngle.Should().BeNull();
        light.ConeSoftness.Should().BeNull();
        light.IesProfilePath.Should().BeNull();
        light.DomeTexturePath.Should().BeNull();
    }

    [Theory]
    [InlineData(SceneLightType.Distant)]
    [InlineData(SceneLightType.Sphere)]
    [InlineData(SceneLightType.Rect)]
    [InlineData(SceneLightType.Disk)]
    [InlineData(SceneLightType.Cylinder)]
    [InlineData(SceneLightType.Dome)]
    public void SceneLightPayload_Accepts_Every_UsdLux_Shape(SceneLightType type)
    {
        var light = new SceneLightPayload { Type = type };
        light.Type.Should().Be(type);
    }

    [Fact]
    public void SceneInstancingPayload_Carries_Required_Arrays_And_Default_Name()
    {
        var transforms = new[] { Matrix4x4.Identity, Matrix4x4.CreateTranslation(1, 0, 0) };
        var protoIndices = new[] { 0, 1 };

        var inst = new SceneInstancingPayload
        {
            PrototypeRoot = "/World/Instancer/Prototypes",
            ProtoIndices = protoIndices,
            InstanceTransforms = transforms,
        };

        inst.Name.Should().Be("PointInstancer");
        inst.PrototypeRoot.Should().Be("/World/Instancer/Prototypes");
        inst.ProtoIndices.Should().BeSameAs(protoIndices);
        inst.InstanceTransforms.Should().BeSameAs(transforms);
        inst.InstanceIds.Should().BeNull();
    }

    [Fact]
    public void SceneInstancingPayload_Optional_InstanceIds_Round_Trip()
    {
        var ids = new[] { 100, 101, 102 };
        var inst = new SceneInstancingPayload
        {
            PrototypeRoot = "/p",
            ProtoIndices = new[] { 0, 0, 0 },
            InstanceTransforms = new[] { Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity },
            InstanceIds = ids,
        };

        inst.InstanceIds.Should().BeSameAs(ids);
    }

    [Fact]
    public void SceneNode_Carries_Multiple_Payloads_And_GetComponent_Resolves_Each()
    {
        var node = new SceneNode { Name = "thing" };
        var mesh = new SceneMeshPayload { Positions = Array.Empty<Vector3>(), Indices = Array.Empty<int>() };
        var cam = new SceneCameraPayload();
        var light = new SceneLightPayload { Type = SceneLightType.Distant };

        node.Components.Add(mesh);
        node.Components.Add(cam);
        node.Components.Add(light);

        node.GetComponent<SceneMeshPayload>().Should().BeSameAs(mesh);
        node.GetComponent<SceneCameraPayload>().Should().BeSameAs(cam);
        node.GetComponent<SceneLightPayload>().Should().BeSameAs(light);
        node.GetComponent<SceneMaterialPayload>().Should().BeNull();
    }
}