using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

// The order the model pass draws a view's batches in, those the view sees planned with the pipeline
// each draws with before any is drawn.
internal sealed partial class ModelRenderer
{
    // Each view's batches, the window's at 0 and each render target's by its id, with where their
    // instances are and the blocks they are culled by, made once a frame by whichever of its
    // shadow and model passes comes first and drawn by both, since the shadow reads the same
    // instances through each light.
    private sealed class View
    {
        public readonly List<Batch> Batches = [];
        public IBuffer? Ring;
        public ulong Offset;
        public Block[] Blocks = new Block[64];
        public long Frame = -1;
        // How many of the batches are opaque, the translucent ones after them.
        public int Opaque;
    }

    /// <summary>How many times the model passes of the frame's views bound a pipeline, the window's and each render target's.</summary>
    internal int PipelinesBound { get; private set; }

    // A batch the view sees, by its index in the view's batches, with what it is drawn with.
    private readonly record struct Planned(int Batch, IPipeline Pipeline, ShaderProgram? Program, Streams Streams, StreamFeed? Feed, bool Positions);

    private readonly List<Planned> _planned = [];
    private readonly Dictionary<IPipeline, int> _pipelineRanks = new(ReferenceEqualityComparer.Instance);
    private long[] _orderKeys = [];
    private Planned[] _orderRun = [];
    // How many batches the last gather made before the translucent ones.
    private int _opaqueBatches;

    // Orders the opaque batches planned by the pipeline each draws with, in the order each pipeline
    // first comes, and within a pipeline the nearest first, each run between two batches that write
    // no depth apart, so a sky drawn first with depth writes off stays under what follows it, and the
    // translucent batches after them as they were recorded. A voxel game's sections, each drawn
    // through DrawMesh with its solid faces, its faces cut out and its lava by turns, bound its
    // terrain shader's pipelines 315 times a frame in its cherry grove at 1920 by 1080, where the
    // same sections as entities bound them 7 times, and the scene pass took 13.1 to 14.1 ms against
    // 9.3 to 10.8; bound by their pipeline the sections took 9.2 to 10.6 ms, and nearest first within
    // it 8.4 to 9.0, the GPU's early depth test leaving more behind. A thousand meshes of one
    // pipeline pay 0.03 to 0.15 ms of the pass's recording for the order.
    private void OrderOpaque(ReadOnlySpan<Batch> batches, int opaque, Block[] blocks, Matrix4x4? viewProjection)
    {
        var planned = CollectionsMarshal.AsSpan(_planned);
        for (int start = 0; start < planned.Length && planned[start].Batch < opaque;)
        {
            if (!batches[planned[start].Batch].Depth)
            {
                start++;
                continue;
            }
            var end = start + 1;
            while (end < planned.Length && planned[end].Batch < opaque && batches[planned[end].Batch].Depth) end++;
            Order(planned[start..end], batches, blocks, viewProjection);
            start = end;
        }
    }

    private void Order(Span<Planned> run, ReadOnlySpan<Batch> batches, Block[] blocks, Matrix4x4? viewProjection)
    {
        if (run.Length < 2 || run.Length > 0xFFFFF) return;
        if (_orderKeys.Length < run.Length)
        {
            _orderKeys = new long[Math.Max(run.Length, _orderKeys.Length * 2)];
            _orderRun = new Planned[_orderKeys.Length];
        }
        _pipelineRanks.Clear();
        // Each key the pipeline's rank in its top bits, the distance in the next 32, ordered as its
        // float is, and the batch's place in the run in the last 20, so equal keys keep their order.
        var (last, rank) = (default(IPipeline), 0);
        for (int i = 0; i < run.Length; i++)
        {
            if (!ReferenceEquals(run[i].Pipeline, last) && !_pipelineRanks.TryGetValue(last = run[i].Pipeline, out rank))
                _pipelineRanks[last] = rank = _pipelineRanks.Count;
            var bits = BitConverter.SingleToUInt32Bits(Nearest(batches[run[i].Batch], blocks, viewProjection));
            var near = (bits & 0x80000000u) != 0 ? ~bits : bits | 0x80000000u;
            _orderKeys[i] = (long)Math.Min(rank, 0x7FF) << 52 | (long)near << 20 | (uint)i;
        }
        Array.Sort(_orderKeys, 0, run.Length);
        run.CopyTo(_orderRun);
        for (int i = 0; i < run.Length; i++) run[i] = _orderRun[(int)(_orderKeys[i] & 0xFFFFF)];
    }

    // How far along the view the middle of a batch's nearest block lies, or minus infinity for a
    // batch with no blocks, which comes first among its pipeline's in its recorded place.
    private static float Nearest(in Batch batch, Block[] blocks, Matrix4x4? viewProjection)
    {
        if (batch.BlockCount == 0) return float.NegativeInfinity;
        var through = viewProjection ?? batch.ViewProjection;
        var w = new Vector3(through.M14, through.M24, through.M34);
        var near = float.PositiveInfinity;
        for (int i = batch.BlockStart; i < batch.BlockStart + batch.BlockCount; i++)
            near = MathF.Min(near, Vector3.Dot((blocks[i].Min + blocks[i].Max) * 0.5f, w));
        return near + through.M44;
    }
}
