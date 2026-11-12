using System.Globalization;
using System.Text;

namespace Engine;

/// <summary>
/// A pixel's light read by its terms, each of the model pass's terms drawn alone in turn and the
/// pixel's linear light read from the frame before its curve, less what the frame holds there with
/// none of them drawn, as a fog's or a sky's light past the surface, beside their sum and the light
/// drawn with every term.
/// </summary>
internal static class LightTermsCommands
{
    // The terms as modelpass.slang's termTaken numbers them.
    private static readonly string[] Names =
    [
        "the probes' indirect light", "the glow lights", "the reflection traced through the depth and the field",
        "the reflection the probes hold", "a reflection probe's capture, scattered", "a reflection probe's capture, reflected",
        "the environment, scattered", "the environment, reflected", "the ambient lights, scattered", "the ambient lights, reflected",
        "the ground's light past the probes", "the lights, scattered", "the lights, reflected", "what the surface gives off",
    ];

    // The frames each term is drawn before its pixel is read, so the frame read is one drawn with it.
    private const int Frames = 2;

    [Command("light.terms", "Reads a pixel's linear light by the terms of the model pass's light, each drawn alone in turn, beside their sum and the light drawn with every term, global illumination on: light.terms <x> <y>")]
    internal static string Terms(string x, string y)
    {
        if (!int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var px) || !int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var py) || px < 0 || py < 0)
            return Refuse("BAD_ARGUMENT", "light.terms takes a pixel of the window, x and y from the top left.");
        var world = ConsoleHost.World!;
        if (!world.TryGetResource<GlobalIlluminationSettings>(out var settings))
            return Refuse("NO_ILLUMINATION", "light.terms draws the terms through global illumination's switches, and SetGlobalIllumination turns it on.");
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device)
            return Refuse("NO_RENDERER", "light.terms reads a frame the renderer drew, and none draws here.");

        // The light with every term, each term's alone, and none of them, a number past the last.
        var read = new (float R, float G, float B)[Names.Length + 2];
        var (term, waited) = (0, 0);
        settings.Term = 0;
        string? failure = null;
        ConsoleHost.Later(() =>
        {
            if (++waited < Frames) return null;
            if (renderer.RenderWorld.TryGet<BloomRenderer>()?.Decoded is not { } decoded)
                failure = "the frame is drawn without its linear light kept, as a scene with no light is";
            else
            {
                var extent = decoded.ColorView.Image.Description.Extent;
                if (px >= extent.Width || py >= extent.Height)
                    failure = $"the pixel {px} {py} lies past the frame, which is {extent.Width} by {extent.Height}";
                else
                {
                    var floats = device.ReadFloats(decoded.ColorView.Image);
                    var at = (py * (int)extent.Width + px) * 4;
                    read[term] = (floats[at], floats[at + 1], floats[at + 2]);
                }
            }
            if (failure is null && term <= Names.Length)
            {
                settings.Term = ++term;
                waited = 0;
                return null;
            }
            settings.Term = 0;
            if (failure is not null) ConsoleHost.Fail("NO_FRAME", failure);
            return failure ?? Say(px, py, read);
        }, (ulong)(Frames * (Names.Length + 3)) + ConsoleHost.LaterFrames);
        return $"reading the light of the pixel {px} {py} by its {Names.Length} terms";
    }

    private static string Refuse(string code, string message)
    {
        ConsoleHost.Fail(code, message);
        return message;
    }

    // Each term's light less the light with none, and its share of their sum, the light with none,
    // the sum with it, and the light drawn with every term.
    private static string Say(int x, int y, (float R, float G, float B)[] read)
    {
        static string Rgb((float R, float G, float B) c) => string.Create(CultureInfo.InvariantCulture, $"{c.R:0.0000} {c.G:0.0000} {c.B:0.0000}");
        var none = read[^1];
        var terms = Enumerable.Range(1, Names.Length).Select(i => (read[i].R - none.R, read[i].G - none.G, read[i].B - none.B)).ToArray();
        var (sr, sg, sb) = (none.R, none.G, none.B);
        foreach (var (r, g, b) in terms) (sr, sg, sb) = (sr + r, sg + g, sb + b);
        var whole = 0.2126f * (sr - none.R) + 0.7152f * (sg - none.G) + 0.0722f * (sb - none.B);
        var text = new StringBuilder($"the pixel {x} {y}, linear light before the curve, each term drawn alone less the light with none\n");
        for (int i = 0; i < terms.Length; i++)
        {
            var luminance = 0.2126f * terms[i].Item1 + 0.7152f * terms[i].Item2 + 0.0722f * terms[i].Item3;
            var share = whole > 0 ? luminance / whole : 0;
            text.Append(CultureInfo.InvariantCulture, $"{i + 1,2} {Names[i]}: {Rgb(terms[i])}, {share:P0} of the terms\n");
        }
        text.Append(CultureInfo.InvariantCulture, $"with none of them, as a fog's or a sky's light past the surface: {Rgb(none)}\n");
        text.Append(CultureInfo.InvariantCulture, $"the terms and that: {Rgb((sr, sg, sb))}\n");
        text.Append(CultureInfo.InvariantCulture, $"drawn with every term: {Rgb(read[0])}");
        return text.ToString();
    }
}
