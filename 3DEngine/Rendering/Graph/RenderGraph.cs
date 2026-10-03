namespace Engine;

/// <summary>
/// render graph with typed slot edges, node ordering edges, sub-graph support,
/// and automatic image layout barrier insertion between nodes.
/// </summary>
/// <seealso cref="INode"/>
/// <seealso cref="SlotInfo"/>
public sealed class RenderGraph : IDisposable
{
    private readonly Dictionary<string, INode> _nodes = new();
    private readonly List<string> _nodeOrder = new(); // insertion order for stable iteration
    private readonly HashSet<(string From, string To)> _nodeEdges = new();
    private readonly List<SlotEdge> _slotEdges = new();
    private readonly Dictionary<string, RenderGraph> _subGraphs = new();

    /// <summary>Describes a data-carrying edge between an output slot and an input slot.</summary>
    private readonly record struct SlotEdge(string OutputNode, int OutputSlot, string InputNode, int InputSlot);

    /// <summary>Adds a named node to the graph.</summary>
    /// <param name="label">Unique label identifying this node.</param>
    /// <param name="node">The node implementation.</param>
    /// <exception cref="InvalidOperationException">A node with the same label already exists.</exception>
    public void AddNode(string label, INode node)
    {
        if (_nodes.ContainsKey(label))
            throw new InvalidOperationException($"Node '{label}' already exists.");
        _nodes[label] = node;
        _nodeOrder.Add(label);
    }

    /// <summary>Returns whether a node with the given label exists in the graph.</summary>
    public bool ContainsNode(string label) => _nodes.ContainsKey(label);

    /// <summary>Adds an ordering-only edge: <paramref name="from"/> must execute before <paramref name="to"/>.</summary>
    public void AddNodeEdge(string from, string to)
    {
        if (!_nodes.ContainsKey(from)) throw new ArgumentException($"Node '{from}' not found.", nameof(from));
        if (!_nodes.ContainsKey(to)) throw new ArgumentException($"Node '{to}' not found.", nameof(to));
        _nodeEdges.Add((from, to));
    }

    /// <summary>Adds a data-carrying slot edge. Implies ordering (output node before input node).</summary>
    /// <param name="outputNode">Label of the producing node.</param>
    /// <param name="outputSlot">Output slot index on the producing node.</param>
    /// <param name="inputNode">Label of the consuming node.</param>
    /// <param name="inputSlot">Input slot index on the consuming node.</param>
    public void AddSlotEdge(string outputNode, int outputSlot, string inputNode, int inputSlot)
    {
        if (!_nodes.ContainsKey(outputNode)) throw new ArgumentException($"Node '{outputNode}' not found.", nameof(outputNode));
        if (!_nodes.ContainsKey(inputNode)) throw new ArgumentException($"Node '{inputNode}' not found.", nameof(inputNode));

        var outSlots = _nodes[outputNode].Output();
        var inSlots = _nodes[inputNode].Input();
        if (outputSlot < 0 || outputSlot >= outSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(outputSlot), $"Node '{outputNode}' has {outSlots.Length} output slot(s).");
        if (inputSlot < 0 || inputSlot >= inSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(inputSlot), $"Node '{inputNode}' has {inSlots.Length} input slot(s).");
        if (outSlots[outputSlot].Type != inSlots[inputSlot].Type)
            throw new InvalidOperationException(
                $"Slot type mismatch: '{outputNode}'[{outputSlot}] is {outSlots[outputSlot].Type} " +
                $"but '{inputNode}'[{inputSlot}] is {inSlots[inputSlot].Type}.");

        _slotEdges.Add(new SlotEdge(outputNode, outputSlot, inputNode, inputSlot));
        _nodeEdges.Add((outputNode, inputNode));
    }

    /// <summary>Registers a named sub-graph that can be invoked via <see cref="RenderGraphContext.RunSubGraph"/>.</summary>
    public void AddSubGraph(string name, RenderGraph subGraph) => _subGraphs[name] = subGraph;

    /// <summary>Returns nodes in topological order using Kahn's algorithm. Throws on cycles.</summary>
    /// <returns>Labels and nodes in dependency-first execution order.</returns>
    /// <exception cref="InvalidOperationException">The graph contains a cycle.</exception>
    public IReadOnlyList<(string Label, INode Node)> TopologicalOrder()
    {
        // Build in-degree map from all edges (node edges + implied slot edges)
        var inDegree = _nodeOrder.ToDictionary(n => n, _ => 0);
        foreach (var (from, to) in _nodeEdges)
        {
            if (inDegree.ContainsKey(from) && inDegree.ContainsKey(to))
                inDegree[to]++;
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<(string, INode)>();

        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            result.Add((n, _nodes[n]));

            foreach (var (from, to) in _nodeEdges)
            {
                if (from == n && inDegree.ContainsKey(to))
                {
                    inDegree[to]--;
                    if (inDegree[to] == 0)
                        queue.Enqueue(to);
                }
            }
        }

        if (result.Count != _nodes.Count)
            throw new InvalidOperationException("Cycle detected in render graph.");

        return result;
    }

    /// <summary>Gathers input slot values for a node from connected output nodes' stored outputs.</summary>
    internal SlotValue[] GatherInputs(string nodeLabel, Dictionary<string, SlotValue[]> outputStore)
    {
        var node = _nodes[nodeLabel];
        var inputSlots = node.Input();
        if (inputSlots.Length == 0) return Array.Empty<SlotValue>();

        var inputs = new SlotValue[inputSlots.Length];
        foreach (var edge in _slotEdges)
        {
            if (edge.InputNode == nodeLabel && outputStore.TryGetValue(edge.OutputNode, out var outputs))
                inputs[edge.InputSlot] = outputs[edge.OutputSlot];
        }
        return inputs;
    }

    /// <summary>Returns the sub-graph with the given name, or null if not found.</summary>
    internal RenderGraph? GetSubGraph(string name) => _subGraphs.GetValueOrDefault(name);

    /// <summary>Disposes all render nodes that implement <see cref="IDisposable"/>.</summary>
    public void Dispose()
    {
        foreach (var node in _nodes.Values)
        {
            if (node is IDisposable disposable)
                disposable.Dispose();
        }
        _nodes.Clear();
        _nodeOrder.Clear();
        _nodeEdges.Clear();
        _slotEdges.Clear();
        foreach (var sg in _subGraphs.Values)
            sg.Dispose();
        _subGraphs.Clear();
    }
}
