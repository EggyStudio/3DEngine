using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Arabic's letters joined by the forms they take by the letters beside them: by the substitutions
/// of <c>arabic.ttf</c>, whose final, medial and initial glyphs of beh are 6, 7 and 8, alef's final
/// 9, and lam with alef 13 alone and 14 joined to the letter before, and by the presentation forms
/// <c>arabic-forms.ttf</c> maps, which <c>build/make-color-test-fonts.py</c> writes. The glyphs
/// expected are those HarfBuzz chooses in the same fonts.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class ArabicTextTests : IDisposable
{
    private const string Beh = "ب", Alef = "ا", Lam = "ل", Fatha = "َ", Dal = "د", Reh = "ر";
    private static readonly string Substituting = Path.Combine(AppContext.BaseDirectory, "Api", "arabic.ttf");
    private static readonly string Forms = Path.Combine(AppContext.BaseDirectory, "Api", "arabic-forms.ttf");

    public ArabicTextTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static ArabicJoining.Form[] FormsOf(string text) => ArabicJoining.Forms([.. text.EnumerateRunes().Select(r => r.Value)]);

    [Fact]
    public void A_Letter_Takes_Its_Form_By_The_Letters_Beside_It()
    {
        FormsOf(Beh + Beh + Beh).Should().Equal(ArabicJoining.Form.Initial, ArabicJoining.Form.Medial, ArabicJoining.Form.Final);
        FormsOf(Beh + Alef + Beh).Should().Equal([ArabicJoining.Form.Initial, ArabicJoining.Form.Final, ArabicJoining.Form.Isolated],
            "alef joins the letter before it and not the one after");
        FormsOf(Dal + Alef + Reh).Should().Equal([ArabicJoining.Form.Isolated, ArabicJoining.Form.Isolated, ArabicJoining.Form.Isolated],
            "none of the three joins the letter after it");
        FormsOf(Beh + Fatha + Beh).Should().Equal([ArabicJoining.Form.Initial, ArabicJoining.Form.None, ArabicJoining.Form.Final],
            "a mark between two letters is passed over");
        FormsOf(Beh + "ـ").Should().Equal([ArabicJoining.Form.Initial, ArabicJoining.Form.None], "the tatweel joins both ways and takes no form");
        FormsOf(Beh + " " + Beh).Should().Equal([ArabicJoining.Form.Isolated, ArabicJoining.Form.None, ArabicJoining.Form.Isolated]);
    }

    [Fact]
    public void A_Font_Joins_Arabic_By_Its_Own_Substitutions_Drawn_Right_To_Left()
    {
        var font = LoadFontEx(Substituting, 100, LoadCodepoints(Beh + Alef + Lam + Fatha + " "));

        TextKeys(font, Beh + Beh + Beh).Should().Equal(JoinedKey(6), JoinedKey(7), JoinedKey(8));
        TextKeys(font, Beh + Alef + Beh).Should().Equal([0x0628, JoinedKey(9), JoinedKey(8)], "a letter in its isolated form is the character's own glyph");
        TextKeys(font, Lam + Alef).Should().Equal([JoinedKey(13)], "lam and alef are joined into one glyph");
        TextKeys(font, Beh + Lam + Alef).Should().Equal([JoinedKey(14), JoinedKey(8)], "the joined glyph's final form after a letter it joins");
        TextKeys(font, Lam + Fatha + Alef).Should().Equal([JoinedKey(13), 0x064E], "the ligature passes over the mark, which stays after it, on it");
        TextKeys(font, Beh + " " + Beh).Should().Equal(0x0628, ' ', 0x0628);
        TextKeys(font, "a " + Beh + Beh).Should().Equal('a', ' ', JoinedKey(6), JoinedKey(8));

        // 1000 units to the em, so at 100 pixels the initial, medial and final glyphs are 60, 50 and
        // 70 wide, where three isolated ones would be 300.
        MeasureTextEx(font, Beh + Beh + Beh, 100, 0).X.Should().Be(180);
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_With_No_Substitutions_For_Arabic_Joins_It_By_The_Presentation_Forms_It_Maps()
    {
        var font = LoadFontEx(Forms, 100, LoadCodepoints(Beh + Alef + Lam));

        TextKeys(font, Beh + Beh + Beh).Should().Equal(0xFE90, 0xFE92, 0xFE91);
        TextKeys(font, Lam + Alef).Should().Equal([0xFEFB], "lam with alef is its one presentation form");
        TextKeys(font, Beh + Lam + Alef).Should().Equal(0xFEFC, 0xFE91);
        MeasureTextEx(font, Beh + Beh + Beh, 100, 0).X.Should().Be(180);
        UnloadFont(font);
    }
}
