using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// The order a line is shown in, by the reduced bidirectional algorithm of <see cref="TextDirection"/>:
/// a run read right to left reversed, its numbers in their own order, its brackets turned, and a
/// cluster's characters kept together.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TextDirectionTests
{
    private const string Alef = "א", Bet = "ב", Gimel = "ג", Qamats = "ָ";

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("abc 123 (x)", "abc 123 (x)")]
    [InlineData(Alef + Bet + Gimel, Gimel + Bet + Alef)]
    [InlineData("abc " + Alef + Bet + Gimel + " def", "abc " + Gimel + Bet + Alef + " def")]
    [InlineData(Alef + Bet + Gimel + " abc", "abc " + Gimel + Bet + Alef)]
    public void A_Run_Read_Right_To_Left_Is_Shown_Reversed_Among_The_Rest(string stored, string shown) =>
        TextDirection.Visual(stored).Should().Be(shown);

    [Fact]
    public void A_Number_In_Text_Read_Right_To_Left_Keeps_Its_Own_Order()
    {
        TextDirection.Visual(Alef + Bet + " 12").Should().Be("12 " + Bet + Alef);
        TextDirection.Visual("50% " + Alef + Bet).Should().Be(Bet + Alef + " 50%", "the percent sign goes with its number");
        // Arabic letters and Arabic-Indic digits, the digits left to right among letters right to left.
        TextDirection.Visual("عدد ١٢٣").Should().Be("١٢٣ ددع");
    }

    [Fact]
    public void A_Bracket_Read_Right_To_Left_Is_Turned_To_Face_The_Way_It_Is_Read()
    {
        TextDirection.Visual(Alef + "(" + Bet + ")").Should().Be("(" + Bet + ")" + Alef);
        TextDirection.Visual("a(b)").Should().Be("a(b)");
    }

    [Fact]
    public void A_Letter_Keeps_Its_Marks_And_An_Emoji_Sequence_Its_Order()
    {
        TextDirection.Visual(Alef + Qamats + Bet).Should().Be(Bet + Alef + Qamats, "the mark is drawn after its letter, over it");
        const string family = "\U0001F468‍\U0001F469‍\U0001F467";
        TextDirection.Visual(Alef + " " + family).Should().Be(family + " " + Alef, "a joined sequence is one cluster, kept whole");
    }

    [Fact]
    public void A_Line_Of_No_Letter_Read_Right_To_Left_Is_Shown_As_It_Is_Stored()
    {
        TextDirection.HasRightToLeft("abc 123 \U0001F600").Should().BeFalse();
        TextDirection.HasRightToLeft(Alef).Should().BeTrue();
        TextDirection.HasRightToLeft("١").Should().BeTrue("an Arabic-Indic digit is read with the Arabic around it");
    }

    [Fact]
    public void The_Levels_Are_Those_Of_The_Rules()
    {
        // A line read left to right with a Hebrew run, which is at 1, and a number in it, at 2 where
        // the text around it is read right to left.
        TextDirection.Levels([.. ("a " + Alef + Bet).EnumerateRunes().Select(r => r.Value)], out var paragraph).Should().Equal(0, 0, 1, 1);
        paragraph.Should().Be(0);
        TextDirection.Levels([.. (Alef + " 12").EnumerateRunes().Select(r => r.Value)], out paragraph).Should().Equal(1, 1, 2, 2);
        paragraph.Should().Be(1, "the first strong character is Hebrew");
        TextDirection.Order([0, 1, 1, 2, 2, 1]).Should().Equal([0, 5, 3, 4, 2, 1], "the runs reversed from the highest level down");
    }
}
