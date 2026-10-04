using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Core;

[Trait("Category", "Unit")]
public class ExceptionsPluginTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Explode() => throw new InvalidOperationException("the reactor overheated");

    [Fact]
    public void A_Crash_Names_Its_Message_And_The_Method_That_Threw()
    {
        Exception thrown;
        try
        {
            Explode();
            return;
        }
        catch (InvalidOperationException ex)
        {
            thrown = ex;
        }

        var text = new StringBuilder();
        ExceptionsPlugin.FormatExceptionChain(text, thrown, 0);

        text.ToString().Should().Contain("the reactor overheated")
            .And.Contain($"at {typeof(ExceptionsPluginTests).FullName}.{nameof(Explode)}");
    }
}
