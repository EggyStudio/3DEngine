using FluentAssertions;
using Xunit;

namespace Engine.Tests.Renderer;

[Trait("Category", "Unit")]
public class RenderGraphTests
{
    // -- Helpers

    private sealed class TestNode : INode
    {
        private readonly SlotInfo[] _inputs;
        private readonly SlotInfo[] _outputs;
        public TestNode(SlotInfo[]? inputs = null, SlotInfo[]? outputs = null)
        {
            _inputs = inputs ?? Array.Empty<SlotInfo>();
            _outputs = outputs ?? Array.Empty<SlotInfo>();
        }
        public SlotInfo[] Input() => _inputs;
        public SlotInfo[] Output() => _outputs;
        public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) { }
    }

    private sealed class DisposableTestNode : INode, IDisposable
    {
        public bool IsDisposed;
        public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) { }
        public void Dispose() => IsDisposed = true;
    }

    // -- Topological order

    [Fact]
    public void TopologicalOrder_Linear_Dependencies()
    {
        using var graph = new RenderGraph();
        graph.AddNode("A", new TestNode());
        graph.AddNode("B", new TestNode());
        graph.AddNode("C", new TestNode());
        graph.AddNodeEdge("A", "B");
        graph.AddNodeEdge("B", "C");

        var order = graph.TopologicalOrder().Select(n => n.Label).ToArray();

        order.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void TopologicalOrder_Diamond_Dependencies_Are_Resolved()
    {
        using var graph = new RenderGraph();
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D
        graph.AddNode("A", new TestNode());
        graph.AddNode("B", new TestNode());
        graph.AddNode("C", new TestNode());
        graph.AddNode("D", new TestNode());
        graph.AddNodeEdge("A", "B");
        graph.AddNodeEdge("A", "C");
        graph.AddNodeEdge("B", "D");
        graph.AddNodeEdge("C", "D");

        var order = graph.TopologicalOrder().Select(n => n.Label).ToList();

        order.First().Should().Be("A");
        order.Last().Should().Be("D");
        order.IndexOf("B").Should().BeLessThan(order.IndexOf("D"));
        order.IndexOf("C").Should().BeLessThan(order.IndexOf("D"));
    }

    [Fact]
    public void TopologicalOrder_Single_Node_Returns_That_Node()
    {
        using var graph = new RenderGraph();
        graph.AddNode("OnlyNode", new TestNode());

        var order = graph.TopologicalOrder().Select(n => n.Label).ToArray();

        order.Should().Equal("OnlyNode");
    }

    [Fact]
    public void TopologicalOrder_Independent_Nodes_All_Appear()
    {
        using var graph = new RenderGraph();
        graph.AddNode("X", new TestNode());
        graph.AddNode("Y", new TestNode());
        graph.AddNode("Z", new TestNode());

        var order = graph.TopologicalOrder().Select(n => n.Label).ToArray();

        order.Should().HaveCount(3);
        order.Should().Contain("X");
        order.Should().Contain("Y");
        order.Should().Contain("Z");
    }

    // -- Cycle detection

    [Fact]
    public void TopologicalOrder_Detects_Cycle()
    {
        var graph = new RenderGraph();
        graph.AddNode("A", new TestNode());
        graph.AddNode("B", new TestNode());
        graph.AddNodeEdge("A", "B");
        graph.AddNodeEdge("B", "A");

        var act = () => graph.TopologicalOrder();

        act.Should().Throw<InvalidOperationException>();
        graph.Dispose();
    }

    // -- Duplicate names

    [Fact]
    public void AddNode_Duplicate_Name_Throws()
    {
        var graph = new RenderGraph();
        graph.AddNode("A", new TestNode());

        var act = () => graph.AddNode("A", new TestNode());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'A'*");
        graph.Dispose();
    }

    // -- Slot edges

    [Fact]
    public void AddSlotEdge_Mismatched_Types_Throws()
    {
        var graph = new RenderGraph();
        graph.AddNode("A", new TestNode(outputs: new[] { new SlotInfo("tex", SlotType.TextureView) }));
        graph.AddNode("B", new TestNode(inputs: new[] { new SlotInfo("buf", SlotType.Buffer) }));

        var act = () => graph.AddSlotEdge("A", 0, "B", 0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Slot type mismatch*");
        graph.Dispose();
    }

    [Fact]
    public void AddSlotEdge_Valid_Types_Succeeds()
    {
        using var graph = new RenderGraph();
        graph.AddNode("A", new TestNode(outputs: new[] { new SlotInfo("tex", SlotType.TextureView) }));
        graph.AddNode("B", new TestNode(inputs: new[] { new SlotInfo("tex", SlotType.TextureView) }));

        var act = () => graph.AddSlotEdge("A", 0, "B", 0);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddSlotEdge_Implies_Ordering()
    {
        using var graph = new RenderGraph();
        graph.AddNode("A", new TestNode(outputs: new[] { new SlotInfo("tex", SlotType.TextureView) }));
        graph.AddNode("B", new TestNode(inputs: new[] { new SlotInfo("tex", SlotType.TextureView) }));
        graph.AddSlotEdge("A", 0, "B", 0);

        var order = graph.TopologicalOrder().Select(n => n.Label).ToArray();

        order.Should().Equal("A", "B");
    }

    // -- Dispose

    [Fact]
    public void Dispose_Disposes_Disposable_Nodes()
    {
        var node = new DisposableTestNode();
        var graph = new RenderGraph();
        graph.AddNode("A", node);

        graph.Dispose();

        node.IsDisposed.Should().BeTrue();
    }
}
