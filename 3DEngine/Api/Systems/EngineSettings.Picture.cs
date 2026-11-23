using System.Globalization;
using Diagnostics;

namespace Engine;

internal static partial class EngineSettings
{
    // The word a resolution, an aspect or the interface's scale takes for the window's or the
    // screen's own.
    private const string Native = "native";

    // A size as window.resolution writes it, or native for none.
    private static string SizeText((int Width, int Height)? size) => size is { } given ? $"{given.Width}x{given.Height}" : Native;

    // A size from window.resolution's text, none for native, or why the text names neither.
    private static ((int Width, int Height)? Size, string? Refused) ParseSize(string text)
    {
        var said = text.Trim().ToLowerInvariant();
        if (said == Native) return (null, null);
        var sides = said.Split('x', '×');
        if (sides.Length == 2 && Whole(sides[0]) is { } width && Whole(sides[1]) is { } height) return ((width, height), null);
        return (null, $"A resolution is native or a width by a height in pixels, as 1280x720, not {text}.");
    }

    // A shape from render.aspect's text, none for native, or why the text names neither.
    private static ((int Width, int Height)? Shape, string? Refused) ParseAspect(string text)
    {
        var said = text.Trim().ToLowerInvariant();
        if (said == Native) return (null, null);
        var sides = said.Split(':');
        if (sides.Length == 2 && Whole(sides[0]) is { } width && Whole(sides[1]) is { } height) return ((width, height), null);
        return (null, $"An aspect is native or a width to a height, as 16:9, not {text}.");
    }

    private static int? Whole(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;

    // A share as a percent's text, and a percent's text, with or without its sign, as a share.
    private static string PercentText(float share) => $"{MathF.Round(share * 100f).ToString(CultureInfo.InvariantCulture)}%";

    private static float? Percent(string text) =>
        float.TryParse(text.Trim().TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
            ? value / 100f : null;

    // The render's page, the picture the scene is drawn as, its scale, filter, aspect and fit, and
    // the most frames a second, and the interface's page, its scale, each acting at once and kept
    // by the settings file. The picture's settings only read where there is no window to bring the
    // picture onto, and the scale while the program sets a size of its own.
    private static IEnumerable<Setting> Picture()
    {
        static World? At() => ConsoleHost.SettingsWorld;
        static AppWindow? Shown() => At()?.TryGetResource<AppWindow>(out var window) == true ? window : null;
        static string? Hidden() => At()?.TryGetResource<Config>(out var config) == true && config.Hidden ? Unshown : null;
        static string? Windowless() => Hidden() ?? (At() is not { } world || Shown() is not null ? null
            : world.TryGetResource<OffscreenSurface>(out _) ? Offscreen : Undrawn);
        static Picture Held() => Shown() is { } window ? window.Sdl.Picture : new Picture();
        static string? Change(Func<Picture, Picture> change)
        {
            if (Shown() is not { } window) return Undrawn;
            WindowModes.SetPicture(window, change(window.Sdl.Picture));
            return null;
        }

        yield return new Setting<string>("render.scale", "The share of the window's size the scene is drawn at, from 50% to 200%, the picture then brought onto the window as render.filter says, and the interface drawn at the window's own size over it.",
            "100%", () => PercentText(PictureSize.Scale(Held())),
            apply: text => Percent(text) is not { } share
                ? $"A scale is a percent, as 67%, not {text}."
                : share < PictureSize.LowScale || share > PictureSize.HighScale
                    ? $"The scale goes from {PercentText(PictureSize.LowScale)} to {PercentText(PictureSize.HighScale)}, not {PercentText(share)}."
                    : Change(picture => picture with { Scale = share }))
        {
            Saved = true, Order = 1, Offer = () => ["50%", "67%", "75%", "100%", "150%", "200%"],
            Lock = () => Windowless() ?? (Held().Size is { } size
                ? $"The program draws the scene at {size.Width}x{size.Height} whatever the window's size, so the scale waits until it sets none."
                : null),
        };
        yield return new Setting<PictureFilter>("render.filter", "How a picture drawn at a size other than the window's is brought onto it, linear filtered and soft, nearest sharp, even at a whole ratio and uneven otherwise, or sharp, each picture pixel a block of one width with one blended window pixel at most on its edges. A picture brought down is filtered linearly whatever it says.",
            PictureFilter.Linear, () => Held().Filter, apply: filter => Change(picture => picture with { Filter = filter }))
        { Saved = true, Order = 2, Lock = Windowless };
        yield return new Setting<string>("render.aspect", "The shape of the picture as a width to a height, as 4:3, the largest of its shape inside the window's size times render.scale, or native for the window's own shape.",
            Native, () => Held().Aspect is { } shape ? $"{shape.Width}:{shape.Height}" : Native,
            apply: text => ParseAspect(text) is (var shape, null) ? Change(picture => picture with { Aspect = shape }) : ParseAspect(text).Refused)
        { Saved = true, Order = 3, Lock = Windowless, Offer = () => [Native, "16:9", "16:10", "21:9", "4:3"] };
        yield return new Setting<PictureFit>("render.fit", "How a picture of a shape other than the window's stands on it, as large as fits with its shape kept and black round it, or stretched over the whole window.",
            PictureFit.Fit, () => Held().Fit, apply: fit => Change(picture => picture with { Fit = fit }))
        { Saved = true, Order = 4, Lock = Windowless };
        yield return new Setting<string>("render.fps_limit", "The most frames a second the program draws, beside window.vsync, or off for no limit.",
            "off", () => FramePace.Limit > 0 ? FramePace.Limit.ToString(CultureInfo.InvariantCulture) : "off",
            apply: text =>
            {
                if (text.Trim().Equals("off", StringComparison.OrdinalIgnoreCase)) FramePace.Limit = 0;
                else if (Whole(text) is { } limit) FramePace.Limit = limit;
                else return $"A limit is off or a whole number of frames a second, as 60, not {text}.";
                return null;
            })
        { Saved = true, Order = 5, Offer = () => ["off", "30", "60", "120", "144", "240"] };

        yield return new Setting<string>("interface.scale", "The size of the panels, the console and the overlay as a percent of their size on a screen of scale factor one, or native for the screen's own scale factor.",
            Native, () => InterfaceScale.Chosen is { } chosen ? PercentText(chosen) : Native,
            apply: text =>
            {
                if (text.Trim().Equals(Native, StringComparison.OrdinalIgnoreCase)) InterfaceScale.Chosen = null;
                else if (Percent(text) is { } share && share >= 0.5f && share <= 4f) InterfaceScale.Chosen = share;
                else return $"An interface scale is native or a percent from 50% to 400%, as 125%, not {text}.";
                return null;
            })
        { Saved = true, Offer = () => [Native, "75%", "100%", "125%", "150%", "200%"] };
    }
}
