namespace Engine;

/// <summary>What one channel of a <see cref="TrackerPlayer"/> is playing, and the effects' memory of their last parameters.</summary>
internal sealed class TrackerChannel
{
    public TrackerInstrument? Instrument;
    public TrackerSample? Sample;
    public TrackerSlot Current;

    // The note playing, with its sample's tuning, as the pattern gave it and as effects move it,
    // its period and how far through the sample each frame steps.
    public float Note, OriginalNote, Period, Step;

    // Where in the sample the next frame is read, negative once a sample that does not loop has
    // ended, and which way a ping-pong loop is going.
    public double Position = -1;
    public bool Backward;

    public float Volume = 1f, Panning = 0.5f;
    public bool Sustained;
    public float Fadeout = 1f, EnvelopeVolume = 1f, EnvelopePanning = 0.5f;
    public int VolumeTick, PanningTick;

    public int AutoVibratoTicks;
    public float AutoVibratoOffset;

    public bool Arpeggio;
    public int ArpeggioOffset;

    public bool Vibrating;
    public int Vibrato, VibratoWave, VibratoTicks;
    public bool VibratoRetrigger = true;
    public float VibratoOffset;

    public int Tremolo, TremoloWave, TremoloTicks;
    public bool TremoloRetrigger = true;
    public float TremoloVolume;

    public int Tremor;
    public bool TremorSilent;

    public int VolumeSlide, FineVolumeSlide, GlobalVolumeSlide, PanningSlide;
    public int PortamentoUp, PortamentoDown, FinePortamentoUp, FinePortamentoDown, ExtraFineUp, ExtraFineDown;
    public int TonePortamento;
    public float TargetPeriod;
    public int MultiRetrig, NoteDelay;
    public int LoopRow, LoopCount;

    // The volume and panning a tick asks for, and those the mix has reached, moved toward them a
    // little each frame so a change does not click.
    public float TargetVolume, TargetPanning = 0.5f, MixVolume, MixPanning = 0.5f;

    // What the channel last gave the mix, and what is left of it fading out after a new note cut
    // it off, which would otherwise click.
    public float LastLeft, LastRight, TailLeft, TailRight;
    public int TailFrames;
}
