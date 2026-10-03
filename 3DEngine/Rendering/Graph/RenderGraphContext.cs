namespace Engine;

/// <summary>
/// Context passed to <see cref="INode.Run"/> for accessing input slot values,
/// producing output slot values, and running sub-graphs.
/// </summary>
/// <seealso cref="INode"/>
/// <seealso cref="SlotValue"/>
public sealed class RenderGraphContext
{
    private readonly SlotValue[] _inputs;
    private readonly SlotValue[] _outputs;
    private readonly Action<string, SlotValue[]>? _runSubGraph;

    /// <summary>Creates a new render graph context for a single node execution.</summary>
    /// <param name="inputs">Input slot values gathered from connected upstream nodes.</param>
    /// <param name="outputCount">Number of output slots declared by the node.</param>
    /// <param name="runSubGraph">Delegate to invoke sub-graph execution.</param>
    internal RenderGraphContext(SlotValue[] inputs, int outputCount, Action<string, SlotValue[]>? runSubGraph = null)
    {
        _inputs = inputs;
        _outputs = new SlotValue[outputCount];
        _runSubGraph = runSubGraph;
    }

    /// <summary>Gets the slot value at the given input index.</summary>
    public SlotValue GetInput(int index) => _inputs[index];

    /// <summary>Gets the input texture view at the given index (convenience for TextureView slots).</summary>
    public IImageView GetInputTexture(int index) => _inputs[index].AsTextureView();

    /// <summary>Gets the input buffer at the given index (convenience for Buffer slots).</summary>
    public IBuffer GetInputBuffer(int index) => _inputs[index].AsBuffer();

    /// <summary>Sets an output slot value at the given index.</summary>
    public void SetOutput(int index, SlotValue value) => _outputs[index] = value;

    /// <summary>Returns the output slot values after node execution.</summary>
    internal SlotValue[] GetOutputs() => _outputs;

    /// <summary>Runs a named sub-graph with the given input values.</summary>
    /// <param name="name">The sub-graph name.</param>
    /// <param name="inputs">Input slot values forwarded to the sub-graph.</param>
    public void RunSubGraph(string name, SlotValue[]? inputs = null) =>
        _runSubGraph?.Invoke(name, inputs ?? Array.Empty<SlotValue>());
}
