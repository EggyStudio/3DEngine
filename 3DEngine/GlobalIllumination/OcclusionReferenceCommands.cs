using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The command that traces the window's ambient occlusion through the device's rays, the reference
/// the horizons and the circular occluders are measured against, which needs the meshes global
/// illumination at High holds for those rays.
/// </summary>
internal static class OcclusionReferenceCommands
{
    [Command("ao.reference", "Traces the window's ambient occlusion through the device's rays from each point of its depth at half the window's size, rays drawn by the cosine about the face there closed off by a mesh within the radius SetAmbientOcclusion gave or the one given, into a PNG and its shares as floats in <png>.f32, NaN where the depth holds nothing: ao.reference <png> <rays> [radius]")]
    internal static string Reference(string path, int rays, float radius = 0)
    {
        var world = ConsoleHost.World!;
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device)
            return "there is no renderer";
        if (!device.CanQueryRays) return "the device traces no rays, which the reference is traced through";
        var render = renderer.RenderWorld;
        if (render.TryGet<GlobalIlluminationRenderer>()?.Rays is not { } scene)
            return "the window's meshes are not held for the device's rays, which global illumination at High on a GPU that traces rays holds them for";
        if (render.TryGet<WindowDepth>() is not { } depth || render.TryGet<WindowView>() is not { } view || !Matrix4x4.Invert(view.Unjittered, out var inverse))
            return "the window's depth is not drawn, which SetAmbientOcclusion draws";
        if (!world.TryGetResource<AssetServer>(out var assets)) return "there is no asset server to compile ao_reference.slang";
        if (radius <= 0) radius = world.TryGetResource<AmbientOcclusionSettings>(out var settings) ? settings.Radius : 1;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var shares = OcclusionReference.Of(device).Trace(assets.LoadSync<ShaderProgram>("shaders/ao_reference.slang").Compute, scene,
            depth.View, depth.Sampler, depth.Extent, inverse, view.Eye, radius, Math.Max(1, rays));
        var (w, h) = ((int)depth.Extent.Width, (int)depth.Extent.Height);
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < shares.Length; i++)
        {
            var level = float.IsNaN(shares[i]) ? (byte)255 : (byte)Math.Clamp(MathF.Round(shares[i] * 255), 0, 255);
            (pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2], pixels[i * 4 + 3]) = (level, level, level, 255);
        }
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        PngWriter.Write(full, pixels, w, h);
        File.WriteAllBytes(full + ".f32", MemoryMarshal.AsBytes(shares.AsSpan()).ToArray());
        return $"traced {w} by {h} at {rays} rays a pixel within {radius} in {clock.Elapsed.TotalSeconds:0.0} s, into {full} and {full}.f32";
    }
}
