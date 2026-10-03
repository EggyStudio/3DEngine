using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>
/// Loggers are made from static initializers, which xUnit runs on many threads at once, so the
/// factory is used concurrently from the first moment of a test run.
/// </summary>
[Trait("Category", "Unit")]
public class LoggerFactoryTests
{
    [Fact]
    public void Loggers_Made_On_Many_Threads_At_Once_Are_One_Per_Category()
    {
        for (int round = 0; round < 20; round++)
        {
            var factory = new LoggerFactory();
            var made = new System.Collections.Concurrent.ConcurrentBag<(string Category, Logger Logger)>();

            Parallel.For(0, 4000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var category = $"Category.{i % 500}";
                made.Add((category, factory.CreateLogger(category)));
            });

            made.GroupBy(m => m.Category)
                .Should().HaveCount(500)
                .And.OnlyContain(g => g.Select(m => m.Logger).Distinct().Count() == 1, "each category gets one logger");
        }
    }
}
