using FluentAssertions;

namespace Engine.Tests.Platform.Audio;

/// <summary>
/// XM and MOD modules played by the engine's own player, from <c>tone.xm</c> and <c>tone.mod</c>,
/// which <c>build/make-test-modules.py</c> writes, each a square wave of 32 frames played as a note
/// at speed 6 and 125 beats a minute, a row of 5,760 frames at 48 kHz.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TrackerTests
{
    private const int Row = 6 * 960;

    private static readonly string Xm = Path.Combine(AppContext.BaseDirectory, "Platform", "Audio", "tone.xm");
    private static readonly string Mod = Path.Combine(AppContext.BaseDirectory, "Platform", "Audio", "tone.mod");

    private static float[] Play(TrackerMusicDecoder decoder, int frames)
    {
        var stereo = new float[frames * 2];
        decoder.Read(stereo).Should().Be(stereo.Length);
        return stereo;
    }

    // The loudest sample of one side over a row, from its middle on, past the volume's ramp.
    private static float Peak(float[] stereo, int row, int side) =>
        Enumerable.Range(row * Row + Row / 2, Row / 2).Max(frame => MathF.Abs(stereo[frame * 2 + side]));

    // The tone's frequency over a row, by how often the left side crosses zero.
    private static double Frequency(float[] stereo, int row)
    {
        int crossings = 0;
        for (int frame = row * Row + 1; frame < (row + 1) * Row; frame++)
            if (MathF.Sign(stereo[frame * 2]) != MathF.Sign(stereo[(frame - 1) * 2]) && stereo[frame * 2] != 0) crossings++;
        return crossings / 2.0 / (Row / 48000.0);
    }

    [Fact]
    public void An_Xm_Plays_Its_Note_At_Its_Pitch_Halved_By_Its_Volume_Column_And_Cut_When_Let_Go()
    {
        var module = XmReader.Read(File.ReadAllBytes(Xm));
        (module.Channels, module.Orders.Length, module.Patterns.Length, module.LinearFrequencies).Should().Be((2, 2, 2, true));
        var decoder = new TrackerMusicDecoder(module);

        var stereo = Play(decoder, 16 * Row);

        // C-4 plays the sample at 8,363 Hz, 32 frames a wave.
        Frequency(stereo, 1).Should().BeApproximately(8363 / 32.0, 3);
        // 100 of 128 at full volume, half of it on each side.
        Peak(stereo, 1, 0).Should().BeApproximately(100 / 128f / 2, 0.01f);
        Peak(stereo, 1, 1).Should().BeApproximately(Peak(stereo, 1, 0), 0.005f, "a sample panned to the middle, 128 of 255");
        Peak(stereo, 5, 0).Should().BeApproximately(100 / 128f / 4, 0.01f, "the volume column halved it at row 4");
        Peak(stereo, 9, 0).Should().Be(0, "the note was let go at row 8, with no envelope to fade by");
    }

    [Fact]
    public void A_Song_Ends_Where_It_Comes_Back_To_A_Row_It_Played()
    {
        // Sixteen rows of the first pattern, then four of the second, whose fourth jumps back.
        new TrackerMusicDecoder(XmReader.Read(File.ReadAllBytes(Xm))).TotalFrames.Should().Be(20 * Row);
        // The one pattern of 64 rows, after which the song goes back to its start.
        new TrackerMusicDecoder(ModReader.Read(File.ReadAllBytes(Mod))).TotalFrames.Should().Be(64 * Row);
    }

    [Fact]
    public void A_Mod_Plays_Its_Period_On_A_Channel_Panned_Left_Until_Its_Volume_Is_Set_To_Nothing()
    {
        var module = ModReader.Read(File.ReadAllBytes(Mod));
        (module.Channels, module.LinearFrequencies, module.Instruments.Length).Should().Be((4, false, 31));
        var decoder = new TrackerMusicDecoder(module);

        var stereo = Play(decoder, 34 * Row);

        // Period 428 plays the sample at 8,287 Hz.
        Frequency(stereo, 1).Should().BeApproximately(7093789.2 / 856 / 32, 3);
        var (left, right) = (Peak(stereo, 1, 0), Peak(stereo, 1, 1));
        (left / right).Should().BeApproximately(2f, 0.01f, "the first channel is the Amiga's left, with a third on the right");
        left.Should().BeApproximately(100 / 128f * 2 / 3f * 0.375f, 0.01f);
        Peak(stereo, 33, 0).Should().Be(0, "C00 at row 32 set its volume to nothing");
    }

    [Fact]
    public void A_Seek_Plays_The_Song_Up_To_The_Frame_So_It_Goes_On_As_If_Played_Through()
    {
        var through = new TrackerMusicDecoder(XmReader.Read(File.ReadAllBytes(Xm)));
        var whole = Play(through, 6 * Row);

        var sought = new TrackerMusicDecoder(XmReader.Read(File.ReadAllBytes(Xm)));
        sought.Seek(5 * Row);
        var rest = Play(sought, Row);

        rest.Should().Equal(whole[(5 * Row * 2)..]);
        sought.Seek(sought.TotalFrames);
        sought.Read(new float[64]).Should().Be(0, "a seek to the end leaves nothing to read");
    }

    [Theory]
    [InlineData("an empty file", new byte[0])]
    [InlineData("a header of another format", new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0 })]
    public void Bytes_That_Are_No_Module_Are_Refused(string what, byte[] bytes)
    {
        FluentActions.Invoking(() => XmReader.Read(bytes)).Should().Throw<InvalidDataException>(what);
        FluentActions.Invoking(() => ModReader.Read(bytes)).Should().Throw<InvalidDataException>(what);
    }
}
