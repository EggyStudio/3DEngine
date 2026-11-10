namespace Engine;

// The triangle budget raised where the frame has room, the time it waits for the target frame rate,
// so the field builds faster in a frame that waits and as the budget says in one that does not.
internal sealed partial class SceneFieldPlan
{
    /// <summary>The most the triangle budget is raised to, as a multiple of <see cref="BaseTriangleBudget"/>.</summary>
    public const int MostRaise = 16;

    /// <summary>
    /// The frames running that bring no mesh and leave nothing to build after which the lift a cut
    /// gave the builds' budgets ends.
    /// </summary>
    public const int CutQuietFrames = 30;

    /// <summary>The most frames after a cut the builds' budgets are lifted for.</summary>
    public const int CutLiftFrames = 300;

    /// <summary>The frames over which the budget is set again from the room the frame has.</summary>
    public const int RaiseFrames = 30;

    /// <summary>The triangle budget the field was made with, which <see cref="TriangleBudget"/> is raised from.</summary>
    public int BaseTriangleBudget { get; private set; }

    /// <summary>
    /// Sets the cascades built a frame and the base triangle budget to new ones, keeping what the
    /// field holds and the builds in progress, the raised budget held between the new base and
    /// <see cref="MostRaise"/> times it.
    /// </summary>
    /// <remarks>
    /// A game raises the budget for a while after a cut of its own and puts it back. Made again for
    /// a budget alone, the field lit the view from nothing in the frame the raise landed and again
    /// in the frame it went back.
    /// </remarks>
    public void SetBudgets(int cascades, int triangles)
    {
        Budget = Math.Max(1, cascades);
        BaseTriangleBudget = Math.Max(1, triangles);
        TriangleBudget = Math.Clamp(TriangleBudget, BaseTriangleBudget, BaseTriangleBudget * MostRaise);
    }

    // The triangles the builds splatted since the budget was last set, and over how many frames.
    private long _splattedSince;
    private int _framesSince;

    // The frames since a cut lifted the builds' budgets, held on after the cascades it went past are
    // built while the meshes a program brings in for the new view still arrive and are built in, as
    // a game meshes its sections over the frames after a teleport, and the frames running that have
    // brought none and left nothing to build, or -1 where no cut holds the lift (Splat).
    private int _afterCut = -1, _quietAfterCut;
    // The cascades, a bit each, a pending mesh was built into by a build that took it in after the
    // frame it gathered in, which that cascade's built frame does not count. Without it a mesh that
    // settled while a region's build ran waited for that cascade for as long as the eye stood still,
    // and the field was never whole.
    private readonly Dictionary<Instance, int> _builtIn = new(Keys.Shared);

    // Ends the lift after a cut once CutQuietFrames running bring no mesh and leave nothing to build,
    // the cascades the eye went past built, or CutLiftFrames after the cut, so a program that streams
    // in meshes without end builds them under its own budget after. Called at the end of Update.
    private void EndLift()
    {
        if (_afterCut < 0) return;
        var quiet = Array.IndexOf(_jumped, true) < 0 && Arriving == 0 && Built;
        _quietAfterCut = quiet ? _quietAfterCut + 1 : 0;
        if (_quietAfterCut >= CutQuietFrames || ++_afterCut > CutLiftFrames) _afterCut = -1;
    }

    /// <summary>
    /// Sets the triangle budget each <see cref="RaiseFrames"/> frames from <paramref name="roomMs"/>,
    /// the milliseconds a frame waits for the target frame rate, and <paramref name="fieldMs"/>,
    /// what the field costs a frame on the CPU and the GPU, both averaged over the frames before,
    /// called before each <see cref="Update"/>.
    /// </summary>
    /// <remarks>
    /// A frame that waits a millisecond or more has room, and the budget is set so the field's
    /// triangles, at what they cost of the field's time over the frames since, take half of it more
    /// than the base budget's, up to <see cref="MostRaise"/> times the base and at most twice the
    /// budget before, so a build that adds to the frame's time takes back the room it is given. A
    /// frame with no room halves the budget toward the base. A program with no target frame rate
    /// waits none, and its budget is the base.
    /// </remarks>
    public void Raise(double roomMs, double fieldMs)
    {
        _splattedSince += Spent;
        if (++_framesSince < RaiseFrames) return;
        var perFrame = _splattedSince / (double)_framesSince;
        (_splattedSince, _framesSince) = (0, 0);
        if (roomMs < 1)
            TriangleBudget = Math.Max(BaseTriangleBudget, TriangleBudget / 2);
        else if (perFrame >= 1 && fieldMs > 0)
        {
            var triangleMs = fieldMs / perFrame;
            var wanted = BaseTriangleBudget + 0.5 * roomMs / triangleMs;
            TriangleBudget = (int)Math.Clamp(wanted, BaseTriangleBudget, Math.Min((double)BaseTriangleBudget * MostRaise, TriangleBudget * 2.0));
        }
    }
}
