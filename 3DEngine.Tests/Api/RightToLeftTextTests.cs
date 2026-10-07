using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Text read right to left drawn and measured with <c>rtl.ttf</c>, whose letters alef, bet and gimel
/// are bars 800, 400 and 200 units high, which <c>build/make-color-test-fonts.py</c> writes, so the
/// order they are drawn in is read from their heights.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class RightToLeftTextTests : IDisposable
{
    private const string Alef = "א", Bet = "ב", Gimel = "ג";
    private static readonly string RightToLeftFont = Path.Combine(AppContext.BaseDirectory, "Api", "rtl.ttf");

    public RightToLeftTextTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // How many of an image's pixels down a column are drawn.
    private static int Height(Image image, int x) => Enumerable.Range(0, image.Height).Count(y => GetImageColor(image, x, y).A > 128);

    [Fact]
    public void Hebrew_Is_Drawn_From_Right_To_Left_And_Measured_As_It_Was()
    {
        var font = LoadFontEx(RightToLeftFont, 32, LoadCodepoints(Alef + Bet + Gimel + " a"));

        TextKeys(font, Alef + Bet + Gimel).Should().Equal(0x05D2, 0x05D1, 0x05D0);
        TextKeys(font, Alef + Bet + "\n" + Gimel + Alef).Should().Equal([0x05D1, 0x05D0, '\n', 0x05D0, 0x05D2], "each line is ordered alone");
        TextKeys(font, "a " + Alef + Bet).Should().Equal('a', ' ', 0x05D1, 0x05D0);
        MeasureTextEx(font, Alef + Bet + Gimel, 32, 2).Should().Be(MeasureTextEx(font, Gimel + Bet + Alef, 32, 2), "the order does not change the width");

        // Drawn into an image the em wide a letter, the first letter stored is at the right.
        var image = ImageTextEx(font, Alef + Bet + Gimel, 32, 0, Color.White);
        var (left, middle, right) = (Height(image, 16), Height(image, 48), Height(image, 80));
        left.Should().BeLessThan(middle, "gimel, the lowest bar, is drawn first from the left");
        middle.Should().BeLessThan(right, "and alef, the tallest, last");
        UnloadImage(image);
        UnloadFont(font);
    }

    [Fact]
    public void Text_Of_No_Letter_Read_Right_To_Left_Is_Keyed_As_Raylib_Draws_It()
    {
        var font = LoadFontEx(RightToLeftFont, 32, LoadCodepoints("a(1)"));

        TextKeys(font, "a(1)").Should().Equal('a', '(', '1', ')');
        UnloadFont(font);
    }
}
