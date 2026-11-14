using System.Numerics;

namespace Engine;

/// <summary>
/// The views the model passes of a frame draw through, each worked against the frame's origin from
/// its eye where the frame noted one, and the pushes made of them, each worked once a frame and kept
/// by its view-projection, with the last of each asked for kept beside, so the batches of a pass
/// drawn through one view, which ask for it batch after batch, find it by comparing the matrices.
/// </summary>
internal sealed class ViewMemo
{
    private readonly Dictionary<Matrix4x4, Matrix4x4> _placed = [];
    private readonly Dictionary<(Matrix4x4 ViewProjection, Matrix4x4? After), ViewPush> _pushes = [];
    private RenderOrigin _origin;
    private ModelDrawList? _draws;
    private bool _placedHeld, _pushHeld;
    private Matrix4x4 _placedFrom, _lastPlaced, _pushFrom;
    private Matrix4x4? _pushAfter;
    private ViewPush _lastPush;

    /// <summary>How many times the kept views and pushes were looked in since they were last forgotten.</summary>
    internal int Lookups { get; private set; }

    /// <summary>
    /// The origin the views are worked against and the draws whose noted eyes they are worked from,
    /// every view and push kept forgotten where the origin is another.
    /// </summary>
    public void Use(RenderOrigin origin, ModelDrawList? draws)
    {
        if (origin != _origin) Forget();
        (_origin, _draws) = (origin, draws);
    }

    /// <summary>Forgets every view and push, for a frame whose origin may stand elsewhere or whose eyes are noted again.</summary>
    public void Forget()
    {
        _placed.Clear();
        _pushes.Clear();
        (_placedHeld, _pushHeld, Lookups) = (false, false, 0);
    }

    /// <summary>A view's view-projection against the origin, from its eye where the frame noted one.</summary>
    public Matrix4x4 Placed(in Matrix4x4 viewProjection)
    {
        if (_placedHeld && _placedFrom == viewProjection) return _lastPlaced;
        Lookups++;
        if (!_placed.TryGetValue(viewProjection, out var placed))
            _placed[viewProjection] = placed = _origin.Seen(viewProjection, _draws?.EyeOf(viewProjection));
        (_placedHeld, _placedFrom, _lastPlaced) = (true, viewProjection, placed);
        return placed;
    }

    /// <summary>
    /// The push of a view's view-projection against the origin, from its eye where the frame noted
    /// one, with a move of clip space after it where one is given, and the origin.
    /// </summary>
    public ref readonly ViewPush Push(in Matrix4x4 viewProjection, Matrix4x4? after = null)
    {
        if (!_pushHeld || _pushFrom != viewProjection || _pushAfter != after)
        {
            Lookups++;
            if (!_pushes.TryGetValue((viewProjection, after), out var push))
                _pushes[(viewProjection, after)] = push = _origin.Push(viewProjection, _draws?.EyeOf(viewProjection), after);
            (_pushHeld, _pushFrom, _pushAfter, _lastPush) = (true, viewProjection, after, push);
        }
        return ref _lastPush;
    }

    /// <summary>The push of a view-projection that takes a place against the origin already, a light's fitted against it, as it is.</summary>
    public ViewPush Pushed(in Matrix4x4 viewProjection) => new() { ViewProjection = viewProjection, Origin = new Vector4(_origin.AsVector3, 0) };
}
